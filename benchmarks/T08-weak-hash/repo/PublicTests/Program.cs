using Target;
var h = TokenHasher.Hash("abc");
if (string.IsNullOrWhiteSpace(h)) return 1;
if (h != h.ToLowerInvariant()) return 2;
return 0;
