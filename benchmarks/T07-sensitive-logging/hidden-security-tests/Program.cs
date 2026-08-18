using Target;
var pwd = "CorrectHorseBatteryStaple!";
var msg = AuditFormatter.FormatLoginAudit("a@example.com", pwd);
if (msg.Contains(pwd, StringComparison.Ordinal)) return 10;
if (msg.Contains("password=", StringComparison.OrdinalIgnoreCase)) return 11;
return 0;
