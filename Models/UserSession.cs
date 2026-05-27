using ControlInventario.Shared.Models;

namespace ControlInventario.Models
{
    public static class UserSession
    {
        public static User? CurrentUser { get; set; }
        public static bool IsAdmin => CurrentUser?.Role?.Name.ToLower() == "administrador";
    }
}
