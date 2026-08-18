using Target;
if (!OutboundUrlPolicy.IsAllowed("https://example.com/api?q=1")) return 1;
if (OutboundUrlPolicy.IsAllowed("http://example.com")) return 2;
return 0;
