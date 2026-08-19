using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExperimentRunner;

public sealed record ReplacementEdit(string Path, string Content);
public sealed record ReplacementPlan(List<ReplacementEdit> Files);

public static class ReplacementUtil
{
    public static ReplacementPlan? ExtractPlan(string text, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var candidate = text.Trim();
        var fenced = Regex.Match(candidate, "```(?:json)?\\s*(?<j>\\{[\\s\\S]*?\\})\\s*```", RegexOptions.IgnoreCase);
        if (fenced.Success) candidate = fenced.Groups["j"].Value;
        else
        {
            var first = candidate.IndexOf('{');
            var last = candidate.LastIndexOf('}');
            if (first < 0 || last <= first) return null;
            candidate = candidate[first..(last + 1)];
        }

        try
        {
            return JsonSerializer.Deserialize<ReplacementPlan>(candidate, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static (bool Applied, string Summary) ApplyPlan(ReplacementPlan plan, string workspace)
    {
        if (plan.Files is null || plan.Files.Count == 0)
            return (false, "Replacement plan contains no files.");

        var root = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var edit in plan.Files)
        {
            if (string.IsNullOrWhiteSpace(edit.Path)) return (false, "Replacement plan contains an empty path.");
            var relative = edit.Path.Replace('\\', '/').Trim();
            while (relative.StartsWith("./", StringComparison.Ordinal)) relative = relative[2..];
            if (Path.IsPathRooted(relative) || relative.Split('/').Any(p => p is ".." or "." or ""))
                return (false, $"Unsafe replacement path: {edit.Path}");
            if (relative.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("PublicTests/", StringComparison.OrdinalIgnoreCase) ||
                relative.EndsWith("packages.lock.json", StringComparison.OrdinalIgnoreCase))
                return (false, $"Replacement path is not model-editable: {relative}");
            if (!seen.Add(relative)) return (false, $"Replacement path is duplicated: {relative}");

            var fullPath = Path.GetFullPath(Path.Combine(workspace, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(root, StringComparison.Ordinal)) return (false, $"Replacement path escapes workspace: {relative}");
            if (!File.Exists(fullPath)) return (false, $"Replacement path does not name an existing repository file: {relative}");
        }

        foreach (var edit in plan.Files)
        {
            var relative = edit.Path.Replace('\\', '/').Trim();
            while (relative.StartsWith("./", StringComparison.Ordinal)) relative = relative[2..];
            var fullPath = Path.Combine(workspace, relative.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(fullPath, edit.Content ?? string.Empty);
        }

        return (true, $"Applied {plan.Files.Count} complete-file replacement(s).");
    }
}
