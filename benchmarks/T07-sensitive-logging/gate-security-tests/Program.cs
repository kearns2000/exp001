using Target;
var pwd = "gate-secret-123";
if (AuditFormatter.FormatLoginAudit("gate@example.com", pwd).Contains(pwd, StringComparison.Ordinal)) return 10;
return 0;
