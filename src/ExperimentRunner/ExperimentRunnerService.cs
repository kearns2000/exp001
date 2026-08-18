using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace ExperimentRunner;

public sealed class ExperimentRunnerService(string repositoryRoot, ExperimentConfig config, JsonSerializerOptions jsonOptions)
{
    private readonly TimeSpan _timeout = TimeSpan.FromMinutes(config.TimeoutMinutes);

    public static List<TaskSpec> LoadTasks(string benchmarkRoot, JsonSerializerOptions options)
    {
        return Directory.GetDirectories(benchmarkRoot)
            .Select(d => Path.Combine(d, "task.json"))
            .Where(File.Exists)
            .Select(p => JsonSerializer.Deserialize<TaskSpec>(File.ReadAllText(p), options)!)
            .OrderBy(t => t.Id, StringComparer.Ordinal)
            .ToList();
    }

    public static List<CandidatePlan> BuildPlan(ExperimentConfig config, IReadOnlyList<TaskSpec> tasks)
    {
        var plan = new List<CandidatePlan>();
        foreach (var task in tasks)
        foreach (var model in config.Models)
        for (var r = 1; r <= model.Repetitions; r++)
        {
            var id = $"{task.Id}__{model.Id}__r{r:00}";
            plan.Add(new(id, task.Id, model.Id, r, $"{config.ExperimentId}:{id}"));
        }
        return plan;
    }

    public async Task RunAsync()
    {
        var benchmarkRoot = Path.Combine(repositoryRoot, config.BenchmarkRoot);
        var tasks = LoadTasks(benchmarkRoot, jsonOptions);
        var plan = BuildPlan(config, tasks);
        var resultRoot = Path.Combine(repositoryRoot, config.ResultsRoot, config.ExperimentId);
        Directory.CreateDirectory(resultRoot);
        await File.WriteAllTextAsync(Path.Combine(resultRoot, "plan.json"), JsonSerializer.Serialize(plan, jsonOptions));
        var failures = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(plan, new ParallelOptions { MaxDegreeOfParallelism = config.MaxParallelism }, async (candidate, stopToken) =>
        {
            var resultPath = Path.Combine(resultRoot, "candidates", candidate.CandidateId, "result.json");
            if (File.Exists(resultPath))
            {
                Console.WriteLine($"SKIP {candidate.CandidateId}");
                return;
            }
            try
            {
                var task = tasks.Single(t => t.Id == candidate.TaskId);
                var model = config.Models.Single(m => m.Id == candidate.ModelId);
                Console.WriteLine($"RUN  {candidate.CandidateId}");
                var result = await RunCandidateAsync(benchmarkRoot, resultRoot, task, model, candidate, stopToken);
                Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
                await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result, jsonOptions), stopToken);
                Console.WriteLine($"DONE {candidate.CandidateId} joint={result.JointCorrectness} decision={result.AutomaticDecision}");
            }
            catch (Exception ex)
            {
                failures.Add($"{candidate.CandidateId}: {ex.Message}");
                Console.Error.WriteLine($"FAIL {candidate.CandidateId}: {ex.Message}");
            }
        });

        if (!failures.IsEmpty)
            await File.WriteAllLinesAsync(Path.Combine(resultRoot, "run-failures.txt"), failures.OrderBy(x => x));
        await Aggregator.WriteOutputsAsync(resultRoot, jsonOptions);
    }

    private async Task<CandidateResult> RunCandidateAsync(string benchmarkRoot, string resultRoot, TaskSpec task, ModelConfig model, CandidatePlan candidate, CancellationToken stopToken)
    {
        var candidateDir = Path.Combine(resultRoot, "candidates", candidate.CandidateId);
        var workspace = Path.Combine(candidateDir, "workspace");
        Directory.CreateDirectory(candidateDir);
        CopyDirectory(Path.Combine(benchmarkRoot, task.Id, "repo"), workspace);
        await InitialiseGitAsync(workspace, stopToken);

        var prompt = BuildPrompt(task, workspace);
        await File.WriteAllTextAsync(Path.Combine(candidateDir, "prompt.txt"), prompt, stopToken);
        var provider = ModelProviderFactory.Create(model.Provider);
        var started = DateTimeOffset.UtcNow;
        var generation = await provider.GenerateAsync(model, prompt, stopToken);
        await File.WriteAllTextAsync(Path.Combine(candidateDir, "raw-model-output.txt"), generation.Text, stopToken);
        await File.WriteAllTextAsync(Path.Combine(candidateDir, "raw-provider-response.txt"), generation.RawResponse, stopToken);
        var patch = PatchUtil.ExtractUnifiedDiff(generation.Text);
        var patchPath = Path.Combine(candidateDir, "candidate.diff");
        await File.WriteAllTextAsync(patchPath, patch, stopToken);
        var counts = PatchUtil.CountDiff(patch);

        var gates = new List<GateResult>();
        var patchApplied = false;
        if (string.IsNullOrWhiteSpace(patch))
        {
            gates.Add(new("patch", GateOutcome.Reject, 0, "No unified diff was produced."));
        }
        else
        {
            var apply = await ProcessUtil.RunAsync("git", $"apply --whitespace=error \"{patchPath}\"", workspace, _timeout, stopToken);
            patchApplied = apply.ExitCode == 0;
            gates.Add(new("patch", patchApplied ? GateOutcome.Pass : GateOutcome.Reject, apply.DurationMs, Trim(apply.StdErr + apply.StdOut), "candidate.diff"));
        }

        if (patchApplied)
        {
            gates.Add(await RunPolicyGateAsync(task, workspace, counts, stopToken));
            gates.Add(await RunDotnetGateAsync("restore", "restore --locked-mode --force-evaluate", workspace, stopToken));
            gates.Add(await RunDotnetGateAsync("build", "build -c Release --no-restore", workspace, stopToken));
            gates.Add(await RunDotnetGateAsync("public-tests", "run -c Release --project PublicTests/PublicTests.csproj --no-restore", workspace, stopToken));
            gates.Add(await RunNuGetAuditGateAsync(workspace, stopToken));
            InstallGateSecurityTest(benchmarkRoot, task.Id, workspace);
            gates.Add(await RunDotnetGateAsync("security-proof-tests", "run -c Release --project GateSecurity/GateSecurity.csproj", workspace, stopToken));
            Directory.Delete(Path.Combine(workspace, "GateSecurity"), recursive: true);
            gates.Add(config.RunCodeQl ? await RunCodeQlGateAsync(workspace, candidateDir, stopToken) : new("codeql", GateOutcome.NotRun, 0, "Disabled by configuration."));

            // Ground-truth oracles are deliberately evaluated after the automatic gate stack.
            // They are not included in AutomaticDecision and are never exposed to the model.
            InstallOracleTests(benchmarkRoot, task.Id, workspace);
            gates.Add(await RunDotnetGateAsync("oracle-functional", "run -c Release --project OracleFunctional/OracleFunctional.csproj", workspace, stopToken));
            gates.Add(await RunDotnetGateAsync("oracle-security", "run -c Release --project OracleSecurity/OracleSecurity.csproj", workspace, stopToken));
        }

        var functionalOracle = gates.FirstOrDefault(g => g.Gate == "oracle-functional");
        var securityOracle = gates.FirstOrDefault(g => g.Gate == "oracle-security");
        var functional = functionalOracle?.Outcome == GateOutcome.Pass;
        var security = securityOracle?.Outcome == GateOutcome.Pass;
        var decision = AggregateAutomaticDecision(gates);
        return new CandidateResult
        {
            ExperimentId = config.ExperimentId,
            CandidateId = candidate.CandidateId,
            TaskId = task.Id,
            Cwe = task.Cwe,
            ModelId = model.Id,
            Provider = model.Provider,
            Model = model.Model,
            Repetition = candidate.Repetition,
            StartedAt = started,
            GenerationDurationMs = generation.DurationMs,
            InputTokens = generation.InputTokens,
            OutputTokens = generation.OutputTokens,
            PatchProduced = !string.IsNullOrWhiteSpace(patch),
            PatchApplied = patchApplied,
            PatchSha256 = PatchUtil.Sha256(patch),
            PatchPath = Path.GetRelativePath(resultRoot, patchPath),
            Gates = gates,
            AutomaticDecision = decision,
            FunctionalCorrectness = functional,
            SecurityCorrectness = security,
            JointCorrectness = functional && security,
            FirstRejectingGate = gates.FirstOrDefault(g => g.Outcome == GateOutcome.Reject)?.Gate ?? "",
            ChangedFiles = counts.Files,
            AddedLines = counts.Added,
            DeletedLines = counts.Deleted,
            ProviderRequestId = generation.ProviderRequestId
        };
    }

    private async Task InitialiseGitAsync(string workspace, CancellationToken stopToken)
    {
        await ProcessUtil.RunAsync("git", "init -q", workspace, _timeout, stopToken);
        await ProcessUtil.RunAsync("git", "config user.email experiment@example.invalid", workspace, _timeout, stopToken);
        await ProcessUtil.RunAsync("git", "config user.name Experiment", workspace, _timeout, stopToken);
        await ProcessUtil.RunAsync("git", "add .", workspace, _timeout, stopToken);
        await ProcessUtil.RunAsync("git", "commit -qm baseline", workspace, _timeout, stopToken);
    }

    private string BuildPrompt(TaskSpec task, string workspace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are repairing a security defect in a .NET 10 repository.");
        sb.AppendLine("Return ONLY a unified git diff beginning with 'diff --git'. Do not include prose or commands.");
        sb.AppendLine("Do not weaken or delete tests. Do not add network calls, secrets, or broad suppressions.");
        sb.AppendLine("Make the smallest change that fixes the issue while preserving intended behaviour.");
        sb.AppendLine();
        sb.AppendLine($"Issue: {task.Issue}");
        sb.AppendLine($"Security property: {task.ExpectedSecurityProperty}");
        sb.AppendLine();
        sb.AppendLine("Repository files:");
        foreach (var file in Directory.GetFiles(workspace, "*", SearchOption.AllDirectories)
                     .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
                     .Where(p => !p.EndsWith("packages.lock.json", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(workspace, file).Replace('\\', '/');
            sb.AppendLine($"\n--- FILE: {relative} ---");
            sb.AppendLine(File.ReadAllText(file));
        }
        return sb.ToString();
    }

    private async Task<GateResult> RunPolicyGateAsync(TaskSpec task, string workspace, (int Files, int Added, int Deleted) counts, CancellationToken stopToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var diff = await ProcessUtil.RunAsync("git", "diff --name-only", workspace, _timeout, stopToken);
        var changedPaths = diff.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var testTouched = changedPaths.Any(p => p.StartsWith("PublicTests/", StringComparison.OrdinalIgnoreCase));
        var sensitive = changedPaths.Any(p => task.SensitivePaths.Any(s => p.StartsWith(s, StringComparison.OrdinalIgnoreCase)));
        var tooLarge = counts.Files > task.MaxChangedFiles || counts.Added > task.MaxAddedLines;
        sw.Stop();
        if (testTouched) return new("policy", GateOutcome.Reject, sw.ElapsedMilliseconds, "Patch changes visible tests.");
        if (tooLarge) return new("policy", GateOutcome.Reject, sw.ElapsedMilliseconds, $"Diff exceeds task limits ({counts.Files} files, {counts.Added} added lines).");
        if (sensitive) return new("policy", GateOutcome.Escalate, sw.ElapsedMilliseconds, "Patch touches a sensitive path and requires code-owner review.");
        return new("policy", GateOutcome.Pass, sw.ElapsedMilliseconds, "Diff scope is within task policy.");
    }

    private async Task<GateResult> RunDotnetGateAsync(string gate, string args, string workspace, CancellationToken stopToken)
    {
        try
        {
            var result = await ProcessUtil.RunAsync("dotnet", args, workspace, _timeout, stopToken);
            return new(gate, result.ExitCode == 0 ? GateOutcome.Pass : GateOutcome.Reject, result.DurationMs, Trim(result.StdOut + result.StdErr));
        }
        catch (Exception ex)
        {
            return new(gate, GateOutcome.Indeterminate, 0, ex.Message);
        }
    }

    private async Task<GateResult> RunNuGetAuditGateAsync(string workspace, CancellationToken stopToken)
    {
        try
        {
            var result = await ProcessUtil.RunAsync("dotnet", "restore --force-evaluate", workspace, _timeout, stopToken);
            var output = result.StdOut + result.StdErr;
            var hasHigh = output.Contains("NU1903", StringComparison.OrdinalIgnoreCase) || output.Contains("NU1904", StringComparison.OrdinalIgnoreCase);
            return new("nuget-audit", hasHigh ? GateOutcome.Reject : result.ExitCode == 0 ? GateOutcome.Pass : GateOutcome.Indeterminate, result.DurationMs, Trim(output));
        }
        catch (Exception ex) { return new("nuget-audit", GateOutcome.Indeterminate, 0, ex.Message); }
    }

    private async Task<GateResult> RunCodeQlGateAsync(string workspace, string candidateDir, CancellationToken stopToken)
    {
        var database = Path.Combine(candidateDir, "codeql-db");
        var sarif = Path.Combine(candidateDir, "codeql.sarif");
        try
        {
            var create = await ProcessUtil.RunAsync(config.CodeQlExecutable, $"database create \"{database}\" --language=csharp --source-root=\"{workspace}\" --command=\"dotnet build -c Release\" --overwrite", workspace, _timeout, stopToken);
            if (create.ExitCode != 0) return new("codeql", GateOutcome.Indeterminate, create.DurationMs, Trim(create.StdOut + create.StdErr));
            var analyse = await ProcessUtil.RunAsync(config.CodeQlExecutable, $"database analyze \"{database}\" \"{config.CodeQlSuite}\" --format=sarifv2.1.0 --output=\"{sarif}\"", workspace, _timeout, stopToken);
            if (analyse.ExitCode != 0) return new("codeql", GateOutcome.Indeterminate, create.DurationMs + analyse.DurationMs, Trim(analyse.StdOut + analyse.StdErr));
            await using var stream = File.OpenRead(sarif);
            using var sarifDoc = await JsonDocument.ParseAsync(stream, cancellationToken: stopToken);
            var hasResults = sarifDoc.RootElement.GetProperty("runs").EnumerateArray()
                .Any(run => run.TryGetProperty("results", out var results) && results.GetArrayLength() > 0);
            return new("codeql", hasResults ? GateOutcome.Reject : GateOutcome.Pass, create.DurationMs + analyse.DurationMs, hasResults ? "CodeQL reported one or more findings." : "No CodeQL findings reported.", "codeql.sarif");
        }
        catch (Exception ex) { return new("codeql", GateOutcome.Indeterminate, 0, ex.Message); }
    }

    private static GateOutcome AggregateAutomaticDecision(IEnumerable<GateResult> gates)
    {
        var automatic = gates.Where(g => !g.Gate.StartsWith("oracle-", StringComparison.Ordinal));
        if (automatic.Any(g => g.Outcome == GateOutcome.Reject)) return GateOutcome.Reject;
        if (automatic.Any(g => g.Outcome == GateOutcome.Indeterminate)) return GateOutcome.Indeterminate;
        if (automatic.Any(g => g.Outcome == GateOutcome.Escalate)) return GateOutcome.Escalate;
        return GateOutcome.Pass;
    }

    private static void InstallGateSecurityTest(string benchmarkRoot, string taskId, string workspace) =>
        CopyDirectory(Path.Combine(benchmarkRoot, taskId, "gate-security-tests"), Path.Combine(workspace, "GateSecurity"));

    private static void InstallOracleTests(string benchmarkRoot, string taskId, string workspace)
    {
        CopyDirectory(Path.Combine(benchmarkRoot, taskId, "hidden-functional-tests"), Path.Combine(workspace, "OracleFunctional"));
        CopyDirectory(Path.Combine(benchmarkRoot, taskId, "hidden-security-tests"), Path.Combine(workspace, "OracleSecurity"));
    }

    private static string Trim(string value)
    {
        value = value.Trim();
        return value.Length <= 4000 ? value : value[^4000..];
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
