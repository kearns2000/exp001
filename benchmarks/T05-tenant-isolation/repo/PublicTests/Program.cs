using Target;
var rows = new[] { new Record("t1", "a", true), new Record("t1", "b", false) };
var visible = RecordQuery.GetVisible(rows, "t1");
if (visible.Count != 1 || visible[0].Id != "a") return 1;
return 0;
