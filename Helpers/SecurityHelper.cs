using ControlInventario.Models;
using System.Security.Cryptography;
using System.Text;

namespace ControlInventarioMovil.Helpers
{
    public static class SecurityHelper
    {
        public static bool HasPermission(string systemCode)
        {
            var role = UserSession.CurrentUser?.Role;
            if (role == null) return false;

            if (UserSession.CurrentUser?.RoleId == 1 || UserSession.CurrentUser?.RoleId == 2)
                return true;

            return role.RolePermissions?.Any(rp => rp.Permission?.SystemCode == systemCode) == true;
        }
    }
}