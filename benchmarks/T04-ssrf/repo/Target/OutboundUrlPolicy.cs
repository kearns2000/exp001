namespace Target;
public static class OutboundUrlPolicy
{
    public static bool IsAllowed(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
