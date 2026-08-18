using Target;
var payload = "x' OR 1=1 --";
var q = UserQuery.Build(payload);
if (q.CommandText.Contains(payload, StringComparison.Ordinal)) return 10;
if (!q.Parameters.Values.Any(v => Equals(v, payload))) return 11;
if (!q.CommandText.Contains("@", StringComparison.Ordinal)) return 12;
return 0;
