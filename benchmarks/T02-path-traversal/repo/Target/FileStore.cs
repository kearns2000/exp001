namespace Target;
public static class FileStore
{
    public static string Resolve(string root, string relativePath)
        => Path.GetFullPath(Path.Combine(root, relativePath));
}
