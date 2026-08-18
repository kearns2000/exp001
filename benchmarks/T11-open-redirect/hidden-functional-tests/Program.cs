using Target;
if (!RedirectPolicy.IsLocal("/account/home?x=1")) return 1;
if (RedirectPolicy.IsLocal("https://example.com")) return 2;
return 0;
