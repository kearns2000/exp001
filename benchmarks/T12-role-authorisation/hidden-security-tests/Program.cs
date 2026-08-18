using Target;
foreach (var role in new[] { "User", "Editor", "Manager", "Administrator", "", "admin " })
    if (RolePolicy.CanDelete(role)) return 10;
if (!RolePolicy.CanDelete("Admin")) return 11;
return 0;
