namespace Target;
public static class RedirectPolicy
{
    public static bool IsLocal(string url)
        => !string.IsNullOrWhiteSpace(url) && url.StartsWith('/');
}
