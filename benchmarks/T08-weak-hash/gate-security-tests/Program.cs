using System.Security.Cryptography;
using System.Text;
using Target;
var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("gate"))).ToLowerInvariant();
if (TokenHasher.Hash("gate") != expected) return 10;
return 0;
