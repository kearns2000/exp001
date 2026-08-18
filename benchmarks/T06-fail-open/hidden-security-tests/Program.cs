using Target;
if (Authorisation.Check(() => throw new InvalidOperationException("policy unavailable"))) return 10;
return 0;
