using Target;
var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "unzip"));
foreach (var bad in new[] { "../evil.txt", "a/../../evil.txt", Path.GetFullPath(Path.Combine(root, "..", "evil.txt")) })
{
    try { _ = ArchivePath.ResolveEntryPath(root, bad); return 10; }
    catch (ArgumentException) { }
}
return 0;
