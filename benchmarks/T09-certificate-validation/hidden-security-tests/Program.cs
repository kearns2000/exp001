using System.Net.Security;
using Target;
var bad = new[] { SslPolicyErrors.RemoteCertificateNameMismatch, SslPolicyErrors.RemoteCertificateChainErrors, SslPolicyErrors.RemoteCertificateNotAvailable };
if (bad.Any(TlsPolicy.ShouldAccept)) return 10;
return 0;
