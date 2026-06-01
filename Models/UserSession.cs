using ControlInventario.Shared.Models;

namespace ControlInventario.Models
{
    public static class UserSession
    {
        public static User? CurrentUser { get; set; }
        public static Inventory? CurrentInventory { get; set; }
        public static Article? CurrentArticleToEdit { get; set; }
        public static string? PreloadedBarcode { get; set; } = null;
        public static ExchangeRate? TodayExchangeRateUSD { get; set; }
        public static ExchangeRate? TodayExchangeRateEUR { get; set; }
        public static bool IsAdmin => CurrentUser?.Role?.Name.ToLower() == "admin";
    }
}
