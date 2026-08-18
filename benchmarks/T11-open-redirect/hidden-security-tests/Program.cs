using Target;
var bad = new[] { "//evil.example/x", "/\evil.example/x", "\evil", "https://evil.example", "http://evil.example" };
if (bad.Any(RedirectPolicy.IsLocal)) return 10;
if (!RedirectPolicy.IsLocal("/a?next=b")) return 11;
return 0;
