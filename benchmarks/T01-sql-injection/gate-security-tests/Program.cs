using Target;
var payload = "admin'--";
var q = UserQuery.Build(payload);
if (q.CommandText.Contains(payload, StringComparison.Ordinal)) return 10;
if (!q.Parameters.Values.Any(v => Equals(v, payload))) return 11;
return 0;
