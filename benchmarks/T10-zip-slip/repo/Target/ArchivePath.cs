namespace Target;
public static class ArchivePath
{
    public static string ResolveEntryPath(string extractionRoot, string entryName)
        => Path.GetFullPath(Path.Combine(extractionRoot, entryName));
}
