using Target;
var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "unzip"));
var p = ArchivePath.ResolveEntryPath(root, "images/./logo.png");
if (!p.EndsWith(Path.Combine("images", "logo.png"), StringComparison.Ordinal)) return 1;
return 0;
