using System.Security.Cryptography;
using System.Text;
using Target;
var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("abc"))).ToLowerInvariant();
if (TokenHasher.Hash("abc") != expected) return 10;
if (TokenHasher.Hash("abc").Length != 64) return 11;
return 0;
