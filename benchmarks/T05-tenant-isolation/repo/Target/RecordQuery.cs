namespace Target;
public sealed record Record(string TenantId, string Id, bool Active);
public static class RecordQuery
{
    public static IReadOnlyList<Record> GetVisible(IEnumerable<Record> records, string tenantId)
        => records.Where(x => x.Active).ToList();
}
