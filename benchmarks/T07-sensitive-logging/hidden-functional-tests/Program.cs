using Target;
var msg = AuditFormatter.FormatLoginAudit("a@example.com", "x");
if (!msg.Contains("a@example.com", StringComparison.Ordinal)) return 1;
if (!msg.Contains("Login", StringComparison.OrdinalIgnoreCase)) return 2;
return 0;
