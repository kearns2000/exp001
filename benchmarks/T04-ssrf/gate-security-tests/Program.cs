using Target;
if (OutboundUrlPolicy.IsAllowed("https://127.0.0.1/admin")) return 10;
return 0;
