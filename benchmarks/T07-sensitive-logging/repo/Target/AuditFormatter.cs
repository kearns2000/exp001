namespace Target;
public static class AuditFormatter
{
    public static string FormatLoginAudit(string email, string password)
        => $"Login attempt email={email} password={password}";
}
