using Target;
var a = TokenHasher.Hash("abc");
var b = TokenHasher.Hash("abc");
if (a != b || a != a.ToLowerInvariant()) return 1;
if (TokenHasher.Hash("abcd") == a) return 2;
return 0;
