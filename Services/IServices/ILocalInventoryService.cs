using ControlInventario.Shared.Models;

namespace ControlInventarioMovil.Services.IServices
{
    public interface ILocalInventoryService
    {
        Task<List<Article>> GetPendingArticlesAsync(int inventoryId);
        Task<List<ArticleDetails>> GetActiveArticleDetailsAsync();
        Task SaveGeneratedSerialsAsync(List<ArticleDetails> newDetails);
        Task<bool> RegisterStockWithdrawalAsync(Article article, Movement withdrawalMovement);
        Task<bool> RegisterStockAdditionAsync(Article article, Movement additionMovement, List<ArticleDetails>? newSerials = null);
        Task<bool> UpdateSerialNumberAsync(int detailId, string newSerialNumber);
        Task<bool> DeactivateSerialAsync(int detailId);
    }
}