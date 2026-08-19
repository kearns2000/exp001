using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ExperimentRunner;

public static class Aggregator
{
    private static readonly HashSet<string> OracleGates = ["oracle-functional", "oracle-security"];

    public static async Task WriteOutputsAsync(string resultRoot, JsonSerializerOptions options)
    {
        var results = LoadResults(resultRoot, options);
        Directory.CreateDirectory(resultRoot);
        await File.WriteAllTextAsync(Path.Combine(resultRoot, "candidate-results.csv"), CandidateCsv(results));
        await File.WriteAllTextAsync(Path.Combine(resultRoot, "model-summary.csv"), ModelSummaryCsv(results));
        await File.WriteAllTextAsync(Path.Combine(resultRoot, "task-summary.csv"), TaskSummaryCsv(results));
        await File.WriteAllTextAsync(Path.Combine(resultRoot, "gate-summary.csv"), GateSummaryCsv(results));
        await File.WriteAllTextAsync(Path.Combine(resultRoot, "detection-summary.csv"), DetectionSummaryCsv(results));
        await File.WriteAllTextAsync(Path.Combine(resultRoot, "verification-only-summary.csv"), VerificationOnlySummaryCsv(results));
        await File.WriteAllTextAsync(Path.Combine(resultRoot, "first-flagged-gate.csv"), FirstFlaggedGateCsv(results));
        await File.WriteAllTextAsync(Path.Combine(resultRoot, "leave-one-gate-out.csv"), LeaveOneGateOutCsv(results));
        await File.WriteAllTextAsync(Path.Combine(resultRoot, "paper-table-study2.md"), PaperTable(results));
        Console.WriteLine($"Aggregated {results.Count} candidate results into {resultRoot}.");
    }

    public static async Task WriteBlindReviewPackAsync(string resultRoot, JsonSerializerOptions options)
    {
        var results = LoadResults(resultRoot, options).Where(r => r.PatchApplied).ToList();
        var random = new Random(8397);
        var shuffled = results.OrderBy(_ => random.Next()).ToList();
        var rows = shuffled.Select((r, i) => new ReviewRow
        {
            BlindId = $"B{i + 1:000}", CandidateId = r.CandidateId, TaskId = r.TaskId, Cwe = r.Cwe, PatchPath = r.PatchPath
        }).ToList();
        var privateMap = string.Join(Environment.NewLine, rows.Select(r => $"{r.BlindId},{Csv(r.CandidateId)},{Csv(r.PatchPath)}"));
        var publicSheet = new StringBuilder("blind_id,task_id,cwe,reviewer_a,reviewer_b,adjudicated,notes\n");
        foreach (var row in rows) publicSheet.AppendLine($"{row.BlindId},{row.TaskId},{row.Cwe},,,," );
        await File.WriteAllTextAsync(Path.Combine(resultRoot, "review-blinded.csv"), publicSheet.ToString());
        await File.WriteAllTextAsync(Path.Combine(resultRoot, "review-private-map.csv"), "blind_id,candidate_id,patch_path\n" + privateMap + "\n");
    }

    private static List<CandidateResult> LoadResults(string resultRoot, JsonSerializerOptions options) =>
        Directory.Exists(Path.Combine(resultRoot, "candidates"))
            ? Directory.GetFiles(Path.Combine(resultRoot, "candidates"), "result.json", SearchOption.AllDirectories)
                .Select(f => JsonSerializer.Deserialize<CandidateResult>(File.ReadAllText(f), options)!)
                .OrderBy(r => r.CandidateId).ToList()
            : [];

    private static string CandidateCsv(IEnumerable<CandidateResult> results)
    {
        var sb = new StringBuilder("candidate_id,task_id,cwe,model_id,provider,model,repetition,provider_request_id,edit_plan_produced,edit_applied,automatic_decision,functional_correctness,security_correctness,joint_correctness,automatic_rejected,automatic_escalated,first_rejecting_gate,first_escalating_gate,generation_ms,input_tokens,output_tokens,changed_files,added_lines,deleted_lines\n");
        foreach (var r in results)
        {
            var firstRejecting = AutomaticGates(r).FirstOrDefault(g => g.Outcome == GateOutcome.Reject)?.Gate ?? "";
            var firstEscalating = AutomaticGates(r).FirstOrDefault(g => g.Outcome == GateOutcome.Escalate)?.Gate ?? "";
            sb.AppendLine(Join(Csv(r.CandidateId), Csv(r.TaskId), Csv(r.Cwe), Csv(r.ModelId), Csv(r.Provider), Csv(r.Model), r.Repetition, Csv(r.ProviderRequestId), r.PatchProduced, r.PatchApplied, r.AutomaticDecision, r.FunctionalCorrectness, r.SecurityCorrectness, r.JointCorrectness, IsRejected(r.AutomaticDecision), IsEscalated(r.AutomaticDecision), Csv(firstRejecting), Csv(firstEscalating), r.GenerationDurationMs, r.InputTokens, r.OutputTokens, r.ChangedFiles, r.AddedLines, r.DeletedLines));
        }
        return sb.ToString();
    }

    private static string ModelSummaryCsv(IEnumerable<CandidateResult> results)
    {
        var sb = new StringBuilder("model_id,n,edit_apply_rate,applied_n,functional_rate,security_rate,joint_correctness_rate,applied_joint_correctness_rate,reject_rate,escalation_rate,false_negative_count,applied_false_negative_count,median_generation_ms\n");
        foreach (var g in results.GroupBy(r => r.ModelId).OrderBy(g => g.Key))
        {
            var a = g.ToList();
            var applied = a.Where(r => r.PatchApplied).ToList();
            var fn = a.Count(r => !r.JointCorrectness && !IsRejected(r.AutomaticDecision));
            var appliedFn = applied.Count(r => !r.JointCorrectness && !IsVerificationRejected(r));
            sb.AppendLine(Join(Csv(g.Key), a.Count,
                Rate(applied.Count, a.Count), applied.Count,
                Rate(a.Count(r => r.FunctionalCorrectness), a.Count),
                Rate(a.Count(r => r.SecurityCorrectness), a.Count),
                Rate(a.Count(r => r.JointCorrectness), a.Count),
                Rate(applied.Count(r => r.JointCorrectness), applied.Count),
                Rate(a.Count(r => IsRejected(r.AutomaticDecision)), a.Count),
                Rate(a.Count(r => IsEscalated(r.AutomaticDecision)), a.Count), fn, appliedFn,
                Median(a.Select(r => r.GenerationDurationMs))));
        }
        return sb.ToString();
    }

    private static string TaskSummaryCsv(IEnumerable<CandidateResult> results)
    {
        var sb = new StringBuilder("task_id,cwe,n,edit_applied,applied_joint_correct,applied_defective,verification_rejected,verification_escalated,applied_false_negatives\n");
        foreach (var g in results.GroupBy(r => new { r.TaskId, r.Cwe }).OrderBy(g => g.Key.TaskId))
        {
            var a = g.ToList();
            var applied = a.Where(r => r.PatchApplied).ToList();
            sb.AppendLine(Join(Csv(g.Key.TaskId), Csv(g.Key.Cwe), a.Count, applied.Count,
                applied.Count(r => r.JointCorrectness), applied.Count(r => !r.JointCorrectness),
                applied.Count(IsVerificationRejected), applied.Count(IsVerificationEscalated),
                applied.Count(r => !r.JointCorrectness && !IsVerificationRejected(r))));
        }
        return sb.ToString();
    }

    private static string GateSummaryCsv(IEnumerable<CandidateResult> results)
    {
        var sb = new StringBuilder("gate,observed,pass,reject,escalate,indeterminate,not_run,median_ms,p95_ms\n");
        foreach (var g in results.SelectMany(r => r.Gates).GroupBy(g => g.Gate).OrderBy(g => g.Key))
        {
            var a = g.ToList();
            sb.AppendLine(Join(Csv(g.Key), a.Count, a.Count(x => x.Outcome == GateOutcome.Pass), a.Count(x => x.Outcome == GateOutcome.Reject), a.Count(x => x.Outcome == GateOutcome.Escalate), a.Count(x => x.Outcome == GateOutcome.Indeterminate), a.Count(x => x.Outcome == GateOutcome.NotRun), Median(a.Select(x => x.DurationMs)), Percentile(a.Select(x => x.DurationMs), 0.95)));
        }
        return sb.ToString();
    }

    private static string DetectionSummaryCsv(IEnumerable<CandidateResult> results)
    {
        var a = results.ToList();
        var tp = a.Count(r => !r.JointCorrectness && IsRejected(r.AutomaticDecision));
        var fn = a.Count(r => !r.JointCorrectness && !IsRejected(r.AutomaticDecision));
        var fp = a.Count(r => r.JointCorrectness && IsRejected(r.AutomaticDecision));
        var tn = a.Count(r => r.JointCorrectness && !IsRejected(r.AutomaticDecision));
        var escalated = a.Count(r => IsEscalated(r.AutomaticDecision));
        var sb = new StringBuilder("n,tp,fn,fp,tn,rejected,escalated,sensitivity,specificity,ppv,npv,escalation_rate\n");
        sb.AppendLine(Join(a.Count, tp, fn, fp, tn, a.Count(r => IsRejected(r.AutomaticDecision)), escalated,
            Ratio(tp, tp + fn), Ratio(tn, tn + fp), Ratio(tp, tp + fp), Ratio(tn, tn + fn), Ratio(escalated, a.Count)));
        return sb.ToString();
    }


    private static string VerificationOnlySummaryCsv(IEnumerable<CandidateResult> results)
    {
        var a = results.Where(r => r.PatchApplied).ToList();
        var defective = a.Where(r => !r.JointCorrectness).ToList();
        var correct = a.Where(r => r.JointCorrectness).ToList();
        var tp = defective.Count(IsVerificationRejected);
        var fn = defective.Count - tp;
        var fp = correct.Count(IsVerificationRejected);
        var tn = correct.Count - fp;
        var escalated = a.Count(IsVerificationEscalated);
        var correctEscalated = correct.Count(IsVerificationEscalated);
        var defectiveEscalated = defective.Count(IsVerificationEscalated);
        var sb = new StringBuilder("applied_n,defective_applied,correct_applied,tp,fn,fp,tn,rejected,escalated,correct_escalated,defective_escalated,sensitivity,specificity,ppv,npv,escalation_rate\n");
        sb.AppendLine(Join(a.Count, defective.Count, correct.Count, tp, fn, fp, tn,
            a.Count(IsVerificationRejected), escalated, correctEscalated, defectiveEscalated,
            Ratio(tp, tp + fn), Ratio(tn, tn + fp), Ratio(tp, tp + fp), Ratio(tn, tn + fn), Ratio(escalated, a.Count)));
        return sb.ToString();
    }

    private static string FirstFlaggedGateCsv(IEnumerable<CandidateResult> results)
    {
        var rejects = results.Select(r => AutomaticGates(r).FirstOrDefault(g => g.Outcome == GateOutcome.Reject)?.Gate ?? "none")
            .GroupBy(x => x).OrderByDescending(g => g.Count()).ThenBy(g => g.Key);
        var escalations = results.Select(r => AutomaticGates(r).FirstOrDefault(g => g.Outcome == GateOutcome.Escalate)?.Gate ?? "none")
            .GroupBy(x => x).OrderByDescending(g => g.Count()).ThenBy(g => g.Key);
        var sb = new StringBuilder("outcome,gate,count\n");
        foreach (var g in rejects) sb.AppendLine($"reject,{Csv(g.Key)},{g.Count()}");
        foreach (var g in escalations) sb.AppendLine($"escalate,{Csv(g.Key)},{g.Count()}");
        return sb.ToString();
    }

    private static string LeaveOneGateOutCsv(IEnumerable<CandidateResult> results)
    {
        var list = results.Where(r => r.PatchApplied).ToList();
        var gates = list.SelectMany(VerificationGates).Select(g => g.Gate).Distinct().OrderBy(x => x).ToList();
        var sb = new StringBuilder("removed_gate,defective_rejected,total_defective,sensitivity,correct_rejected,total_correct,specificity,escalated,total_applied,escalation_rate\n");
        foreach (var removed in gates)
        {
            var defective = list.Where(r => !r.JointCorrectness).ToList();
            var correct = list.Where(r => r.JointCorrectness).ToList();
            bool RejectWithout(CandidateResult r) => VerificationGates(r).Where(g => g.Gate != removed).Any(g => g.Outcome == GateOutcome.Reject);
            bool EscalateWithout(CandidateResult r) => VerificationGates(r).Where(g => g.Gate != removed).Any(g => g.Outcome == GateOutcome.Escalate);
            var defectReject = defective.Count(RejectWithout);
            var correctReject = correct.Count(RejectWithout);
            var escalated = list.Count(EscalateWithout);
            sb.AppendLine(Join(Csv(removed), defectReject, defective.Count, Ratio(defectReject, defective.Count), correctReject, correct.Count,
                Ratio(correct.Count - correctReject, correct.Count), escalated, list.Count, Ratio(escalated, list.Count)));
        }
        return sb.ToString();
    }

    private static string PaperTable(IEnumerable<CandidateResult> results)
    {
        var list = results.ToList();
        if (list.Count == 0) return "Study 2 has not been run yet.\n";
        var sb = new StringBuilder("| Model | Candidates | Edits applied | Applied jointly correct | Applied defective | Verification rejected | Verification escalated | Applied false negatives |\n|---|---:|---:|---:|---:|---:|---:|---:|\n");
        foreach (var g in list.GroupBy(r => r.ModelId).OrderBy(g => g.Key))
        {
            var all = g.ToList();
            var applied = all.Where(r => r.PatchApplied).ToList();
            sb.AppendLine($"| {g.Key} | {all.Count} | {applied.Count} ({Rate(applied.Count, all.Count)}) | {applied.Count(r => r.JointCorrectness)} ({Rate(applied.Count(r => r.JointCorrectness), applied.Count)}) | {applied.Count(r => !r.JointCorrectness)} | {applied.Count(IsVerificationRejected)} | {applied.Count(IsVerificationEscalated)} | {applied.Count(r => !r.JointCorrectness && !IsVerificationRejected(r))} |");
        }
        return sb.ToString();
    }

    private static IEnumerable<GateResult> AutomaticGates(CandidateResult r) => r.Gates.Where(g => !OracleGates.Contains(g.Gate));
    private static IEnumerable<GateResult> VerificationGates(CandidateResult r) =>
        AutomaticGates(r).Where(g => g.Gate is not "edit-application" and not "patch");
    private static bool IsVerificationRejected(CandidateResult r) => VerificationGates(r).Any(g => g.Outcome == GateOutcome.Reject);
    private static bool IsVerificationEscalated(CandidateResult r) => VerificationGates(r).Any(g => g.Outcome == GateOutcome.Escalate);
    private static bool IsRejected(GateOutcome outcome) => outcome == GateOutcome.Reject;
    private static bool IsEscalated(GateOutcome outcome) => outcome == GateOutcome.Escalate;

    private static string Join(params object?[] values) =>
        string.Join(",", values.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""));

    private static string Csv(object? value)
    {
        var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return '"' + s.Replace("\"", "\"\"") + '"';
    }

    private static string Rate(int n, int d) => d == 0 ? "0.0%" : ((double)n / d).ToString("P1", CultureInfo.InvariantCulture);
    private static string Ratio(int n, int d) => d == 0 ? "NA" : ((double)n / d).ToString("0.000", CultureInfo.InvariantCulture);

    private static long Median(IEnumerable<long> values) => Percentile(values, 0.5);

    private static long Percentile(IEnumerable<long> values, double p)
    {
        var a = values.Order().ToArray();
        if (a.Length == 0) return 0;
        var index = (int)Math.Ceiling(p * a.Length) - 1;
        return a[Math.Clamp(index, 0, a.Length - 1)];
    }
}
