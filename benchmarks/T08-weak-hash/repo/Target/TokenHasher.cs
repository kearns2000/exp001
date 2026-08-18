using System.Security.Cryptography;
using System.Text;
namespace Target;
public static class TokenHasher
{
    public static string Hash(string token)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
