using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ExperimentRunner;

public static class PatchUtil
{
    public static string ExtractUnifiedDiff(string text)
    {
        var fenced = Regex.Match(text, "```(?:diff)?\\s*(?<d>diff --git[\\s\\S]*?)```", RegexOptions.IgnoreCase);
        if (fenced.Success) return fenced.Groups["d"].Value.Trim() + Environment.NewLine;
        var index = text.IndexOf("diff --git ", StringComparison.Ordinal);
        return index >= 0 ? text[index..].Trim() + Environment.NewLine : "";
    }

    public static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public static (int Files, int Added, int Deleted) CountDiff(string diff)
    {
        var files = Regex.Matches(diff, "^diff --git ", RegexOptions.Multiline).Count;
        var added = 0;
        var deleted = 0;
        foreach (var line in diff.Split('\n'))
        {
            if (line.StartsWith("+++", StringComparison.Ordinal) || line.StartsWith("---", StringComparison.Ordinal)) continue;
            if (line.StartsWith('+')) added++;
            else if (line.StartsWith('-')) deleted++;
        }
        return (files, added, deleted);
    }
}
