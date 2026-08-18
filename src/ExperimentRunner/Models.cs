using System.Text.Json.Serialization;

namespace ExperimentRunner;

public sealed record ExperimentConfig
{
    public required string ExperimentId { get; init; }
    public int MaxParallelism { get; init; } = 1;
    public int TimeoutMinutes { get; init; } = 10;
    public string BenchmarkRoot { get; init; } = "benchmarks";
    public string ResultsRoot { get; init; } = "results";
    public bool RunCodeQl { get; init; } = true;
    public string CodeQlExecutable { get; init; } = "codeql";
    public string CodeQlSuite { get; init; } = "csharp-security-extended.qls";
    public required List<ModelConfig> Models { get; init; }
}

public sealed record ModelConfig
{
    public required string Id { get; init; }
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public int Repetitions { get; init; } = 1;
    public string? BaseUrl { get; init; }
    public string? ApiKeyEnvironmentVariable { get; init; }
    public string? Command { get; init; }
    public string? Arguments { get; init; }
    public double? Temperature { get; init; }
    public int MaxOutputTokens { get; init; } = 6000;
}

public sealed record TaskSpec
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Cwe { get; init; }
    public required string Issue { get; init; }
    public required string ExpectedSecurityProperty { get; init; }
    public required string[] SensitivePaths { get; init; }
    public int MaxChangedFiles { get; init; } = 5;
    public int MaxAddedLines { get; init; } = 120;
}

public sealed record CandidatePlan(
    string CandidateId,
    string TaskId,
    string ModelId,
    int Repetition,
    string SeedLabel);

public enum GateOutcome { Pass, Reject, Escalate, Indeterminate, NotRun }

public sealed record GateResult(
    string Gate,
    GateOutcome Outcome,
    long DurationMs,
    string Summary,
    string? ArtifactPath = null);

public sealed record CandidateResult
{
    public required string ExperimentId { get; init; }
    public required string CandidateId { get; init; }
    public required string TaskId { get; init; }
    public required string Cwe { get; init; }
    public required string ModelId { get; init; }
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public required int Repetition { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required long GenerationDurationMs { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public required bool PatchProduced { get; init; }
    public required bool PatchApplied { get; init; }
    public required string PatchSha256 { get; init; }
    public required string PatchPath { get; init; }
    public required List<GateResult> Gates { get; init; }
    public required GateOutcome AutomaticDecision { get; init; }
    public required bool FunctionalCorrectness { get; init; }
    public required bool SecurityCorrectness { get; init; }
    public required bool JointCorrectness { get; init; }
    public required string FirstRejectingGate { get; init; }
    public required int ChangedFiles { get; init; }
    public required int AddedLines { get; init; }
    public required int DeletedLines { get; init; }
    public string? ProviderRequestId { get; init; }
    public string? Error { get; init; }
}

public sealed record GenerationResult(
    string Text,
    string RawResponse,
    string? ProviderRequestId,
    long DurationMs,
    int? InputTokens,
    int? OutputTokens);

public sealed record ReviewRow
{
    public required string BlindId { get; init; }
    public required string CandidateId { get; init; }
    public required string TaskId { get; init; }
    public required string Cwe { get; init; }
    public required string PatchPath { get; init; }
    public string ReviewerA { get; init; } = "";
    public string ReviewerB { get; init; } = "";
    public string Adjudicated { get; init; } = "";
    public string Notes { get; init; } = "";
}
