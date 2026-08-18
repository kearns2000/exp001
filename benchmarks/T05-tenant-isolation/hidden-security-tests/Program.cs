using Target;
var rows = new[] { new Record("t1", "a", true), new Record("t2", "b", true), new Record("T1", "c", true) };
var visible = RecordQuery.GetVisible(rows, "t1");
if (visible.Count != 1 || visible[0].TenantId != "t1") return 10;
return 0;
