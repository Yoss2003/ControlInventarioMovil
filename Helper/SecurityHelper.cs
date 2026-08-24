using ControlInventario.Models;

namespace ControlInventarioMovil.Helpers
{
    public static class SecurityHelper
    {
        public static bool HasPermission(string systemCode)
        {
            var role = UserSession.CurrentUser?.Role;
            if (role == null) return false;

            if (role.Name == "SuperAdmin" || UserSession.CurrentUser?.RoleId == 1)
                return true;

            return role.RolePermissions?.Any(rp => rp.Permission?.SystemCode == systemCode) == true;
        }
    }
}