using Target;
var rows = new[] { new Record("t1", "a", true), new Record("t1", "b", false), new Record("t1", "c", true) };
var visible = RecordQuery.GetVisible(rows, "t1");
if (visible.Select(x => x.Id).Order().SequenceEqual(new[]{"a","c"}) is false) return 1;
return 0;
