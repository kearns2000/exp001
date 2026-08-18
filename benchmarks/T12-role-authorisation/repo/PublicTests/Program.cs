using Target;
if (!RolePolicy.CanDelete("Admin")) return 1;
if (RolePolicy.CanDelete("Guest")) return 2;
return 0;
