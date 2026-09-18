using ControlInventarioMovil.Data;
using ControlInventario.Shared.Models.Interfaces;

namespace ControlInventarioMovil.Helper
{
    public static class SyncedOffHelper
    {
        public static async Task<bool> SaveLocallyAsync<T>(T entity) where T : class, ISyncable
        {
            using var context = new LocalDbContext();
            entity.IsSynced = false;
            context.Set<T>().Add(entity);
            await context.SaveChangesAsync();
            return true;
        }

        public static async Task<bool> UpdateLocallyAsync<T>(T entity) where T : class, ISyncable
        {
            using var context = new LocalDbContext();
            entity.IsSynced = false;
            context.Set<T>().Update(entity);
            await context.SaveChangesAsync();
            return true;
        }

        public static async Task<bool> DeleteLocallyAsync<T>(int id) where T : class, ISyncable
        {
            using var context = new LocalDbContext();
            var entity = await context.Set<T>().FindAsync(id);
            if (entity != null)
            {
                context.Set<T>().Remove(entity);
                await context.SaveChangesAsync();
            }
            return true;
        }
    }
}