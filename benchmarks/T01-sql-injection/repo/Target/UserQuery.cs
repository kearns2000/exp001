namespace Target;
public sealed record SqlCommandSpec(string CommandText, IReadOnlyDictionary<string, object?> Parameters);
public static class UserQuery
{
    public static SqlCommandSpec Build(string userName)
        => new($"SELECT Id, UserName FROM Users WHERE UserName = '{userName}'", new Dictionary<string, object?>());
}
