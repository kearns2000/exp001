namespace Target;
public static class ToolInputPolicy
{
    public static bool IsSafeInput(string fileName)
        => !string.IsNullOrWhiteSpace(fileName) && !fileName.Contains(' ');
}
