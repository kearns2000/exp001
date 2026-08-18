using Target;
var q = UserQuery.Build("alice");
if (!q.CommandText.Contains("SELECT", StringComparison.OrdinalIgnoreCase)) return 1;
if (!q.CommandText.Contains("Users", StringComparison.OrdinalIgnoreCase)) return 2;
return 0;
