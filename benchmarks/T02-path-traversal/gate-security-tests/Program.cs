using Target;
var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "store"));
try { _ = FileStore.Resolve(root, "../outside.txt"); return 10; } catch (ArgumentException) { }
return 0;
