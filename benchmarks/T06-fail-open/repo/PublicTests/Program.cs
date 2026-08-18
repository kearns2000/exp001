using Target;
if (!Authorisation.Check(() => true)) return 1;
if (Authorisation.Check(() => false)) return 2;
return 0;
