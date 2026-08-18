using Target;
if (Authorisation.Check(() => throw new Exception("gate failure"))) return 10;
return 0;
