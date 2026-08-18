using Target;
var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "unzip"));
try { _ = ArchivePath.ResolveEntryPath(root, "../gate.txt"); return 10; } catch (ArgumentException) { }
return 0;
