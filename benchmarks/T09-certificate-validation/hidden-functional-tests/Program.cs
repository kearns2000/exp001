using System.Net.Security;
using Target;
if (!TlsPolicy.ShouldAccept(SslPolicyErrors.None)) return 1;
return 0;
