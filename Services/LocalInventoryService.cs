using ControlInventario.Shared.Models;
using ControlInventarioMovil.Data;
using ControlInventarioMovil.Services.IServices;
using ControlInventarioMovil.Utilities;
using Microsoft.EntityFrameworkCore;

namespace ControlInventarioMovil.Services
{
    public class LocalInventoryService : ILocalInventoryService
    {
        public async Task<List<Article>> GetPendingArticlesAsync(int inventoryId)
        {
            try
            {
                using var context = new LocalDbContext();
                return await context.Articles
                    .Where(a => a.InventoryId == inventoryId && !a.IsSynced)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "LocalInventoryService - GetPendingArticlesAsync");
                return [];
            }
        }

        public async Task<List<ArticleDetails>> GetActiveArticleDetailsAsync()
        {
            try
            {
                using var context = new LocalDbContext();
                return await context.ArticleDetails.Where(d => d.IsActive).ToListAsync();
            }
            catch
            {
                return [];
            }
        }

        public async Task SaveGeneratedSerialsAsync(List<ArticleDetails> newDetails)
        {
            if (newDetails == null || newDetails.Count == 0) return;
            try
            {
                using var context = new LocalDbContext();
                await context.ArticleDetails.AddRangeAsync(newDetails);
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "LocalInventoryService - SaveGeneratedSerialsAsync");
            }
        }

        public async Task<bool> RegisterStockWithdrawalAsync(Article article, Movement withdrawalMovement)
        {
            try
            {
                using var context = new LocalDbContext();
                article.IsSynced = false;
                context.Articles.Update(article);
                await context.Movements.AddAsync(withdrawalMovement);
                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "LocalInventoryService - RegisterStockWithdrawalAsync");
                return false;
            }
        }

        public async Task<bool> RegisterStockAdditionAsync(Article article, Movement additionMovement, List<ArticleDetails>? newSerials = null)
        {
            try
            {
                using var context = new LocalDbContext();
                article.IsSynced = false;
                context.Articles.Update(article);
                await context.Movements.AddAsync(additionMovement);

                if (newSerials != null && newSerials.Count > 0)
                {
                    await context.ArticleDetails.AddRangeAsync(newSerials);
                }

                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "LocalInventoryService - RegisterStockAdditionAsync");
                return false;
            }
        }

        public async Task<bool> UpdateSerialNumberAsync(int detailId, string newSerialNumber)
        {
            try
            {
                using var context = new LocalDbContext();
                var registroDb = await context.ArticleDetails.FindAsync(detailId);
                if (registroDb != null)
                {
                    registroDb.SerialNumber = newSerialNumber.Trim();
                    context.ArticleDetails.Update(registroDb);
                    await context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "LocalInventoryService - UpdateSerialNumberAsync");
                return false;
            }
        }

        public async Task<bool> DeactivateSerialAsync(int detailId)
        {
            try
            {
                using var context = new LocalDbContext();
                var registroDb = await context.ArticleDetails.FindAsync(detailId);
                if (registroDb != null)
                {
                    registroDb.IsActive = false;
                    context.ArticleDetails.Update(registroDb);
                    await context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "LocalInventoryService - DeactivateSerialAsync");
                return false;
            }
        }
    }
}