using Microsoft.EntityFrameworkCore;
using ControlInventarioMovil.Data;
using ControlInventario.Shared.Models;

namespace ControlInventarioMovil.Services
{
    public class LocalDataService
    {
        public async Task GuardarArticuloLocalAsync(Article articulo)
        {
            using var context = new LocalDbContext();

            // Marcamos el registro como pendiente de sincronización
            articulo.IsSynced = false;
            articulo.LastModified = DateTime.Now;

            // Verificamos si ya existe localmente
            var localArticle = await context.Articles.FirstOrDefaultAsync(a => a.Id == articulo.Id);

            if (localArticle == null)
            {
                // Es nuevo: Lo creamos localmente
                await context.Articles.AddAsync(articulo);
            }
            else
            {
                // Ya existe: Actualizamos sus propiedades localmente
                context.Entry(localArticle).CurrentValues.SetValues(articulo);
            }

            // Guardamos de inmediato en el celular (Operación en milisegundos, 0 dependencia de red)
            await context.SaveChangesAsync();
        }
    }
}