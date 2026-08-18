using Target;
var bad = new[] {
 "https://localhost/admin", "https://127.0.0.1/", "https://[::1]/", "https://10.0.0.4/", "https://192.168.1.10/", "https://172.16.0.1/", "https://169.254.169.254/latest/meta-data/"
};
if (bad.Any(OutboundUrlPolicy.IsAllowed)) return 10;
if (!OutboundUrlPolicy.IsAllowed("https://8.8.8.8/")) return 11;
return 0;
