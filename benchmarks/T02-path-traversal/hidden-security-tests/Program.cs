using Target;
var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "store"));
foreach (var bad in new[] { "../secret.txt", "../../x", Path.GetFullPath(Path.Combine(root, "..", "evil.txt")) })
{
    try { _ = FileStore.Resolve(root, bad); return 10; }
    catch (ArgumentException) { }
}
var ok = FileStore.Resolve(root, "a/../b.txt");
var rootWithSep = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
if (!ok.StartsWith(rootWithSep, StringComparison.Ordinal)) return 11;
return 0;
