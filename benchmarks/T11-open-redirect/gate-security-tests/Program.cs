using Target;
if (RedirectPolicy.IsLocal("//evil.example/gate")) return 10;
return 0;
