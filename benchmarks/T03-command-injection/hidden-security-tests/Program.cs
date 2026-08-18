using Target;
var bad = new[] { "a;rm", "a&&whoami", "$(id)", "../x", "a/b", "a\b", "a|b", "a>b", "a`id`" };
if (bad.Any(ToolInputPolicy.IsSafeInput)) return 10;
var good = new[] { "a.txt", "A_1-2.json", "file" };
if (good.Any(x => !ToolInputPolicy.IsSafeInput(x))) return 11;
return 0;
