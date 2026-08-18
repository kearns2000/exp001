using Target;
if (!ToolInputPolicy.IsSafeInput("report_2026-08.json")) return 1;
if (ToolInputPolicy.IsSafeInput("two words.txt")) return 2;
return 0;
