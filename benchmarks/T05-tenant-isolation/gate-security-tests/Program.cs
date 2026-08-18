using Target;
var rows = new[] { new Record("tenant-a", "a", true), new Record("tenant-b", "b", true) };
var visible = RecordQuery.GetVisible(rows, "tenant-a");
if (visible.Any(x => x.TenantId != "tenant-a")) return 10;
return 0;
