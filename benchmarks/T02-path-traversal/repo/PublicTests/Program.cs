using Target;
var root = Path.Combine(Path.GetTempPath(), "store");
var p = FileStore.Resolve(root, "docs/a.txt");
if (!p.EndsWith(Path.Combine("docs", "a.txt"), StringComparison.Ordinal)) return 1;
return 0;
