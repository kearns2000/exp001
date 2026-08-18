using Target;
var msg = AuditFormatter.FormatLoginAudit("a@example.com", "secret");
if (!msg.Contains("a@example.com", StringComparison.Ordinal)) return 1;
return 0;
