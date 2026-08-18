using System.Net.Security;
namespace Target;
public static class TlsPolicy
{
    public static bool ShouldAccept(SslPolicyErrors errors) => true;
}
