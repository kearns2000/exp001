using Target;
if (!OutboundUrlPolicy.IsAllowed("https://example.com/api")) return 1;
if (OutboundUrlPolicy.IsAllowed("http://example.com/api")) return 2;
return 0;
