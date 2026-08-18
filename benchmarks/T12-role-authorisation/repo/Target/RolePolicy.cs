namespace Target;
public static class RolePolicy
{
    public static bool CanDelete(string role)
        => !string.Equals(role, "Guest", StringComparison.OrdinalIgnoreCase);
}
