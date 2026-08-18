using System.Net.Security;
using Target;
if (TlsPolicy.ShouldAccept(SslPolicyErrors.RemoteCertificateChainErrors)) return 10;
return 0;
