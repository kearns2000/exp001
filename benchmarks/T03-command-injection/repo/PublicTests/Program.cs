using Target;
if (!ToolInputPolicy.IsSafeInput("report-2026.json")) return 1;
if (ToolInputPolicy.IsSafeInput("two words.txt")) return 2;
return 0;
