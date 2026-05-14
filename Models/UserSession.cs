namespace ControlInventarioMovil.Models
{
    public static class UserSession
    {
        public static User? CurrentUser { get; set; }
        public static bool IsAdmin => CurrentUser?.RoleId == 1;
    }
}
