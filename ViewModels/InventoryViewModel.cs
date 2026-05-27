using System.Collections.ObjectModel;
using ControlInventario.Shared.Models;

namespace ControlInventarioMovil.ViewModels
{
    public class InventoryViewModel
    {
        // Esta es la lista reactiva que la pantalla escuchará
        public ObservableCollection<Article> Articulos { get; set; }

        public InventoryViewModel()
        {
            Articulos = new ObservableCollection<Article>();
            CargarDatosPrueba();
        }

        private void CargarDatosPrueba()
        {
            // Creamos datos ficticios basados en tu modelo Article
            Articulos.Add(new Article
            {
                Name = "Laptop HP Pavilion",
                Barcode = "750123456789",
                SalePrice = 1299.00m,
                Stock = 120
            });

            Articulos.Add(new Article
            {
                Name = "Teclado Mecánico Logitech",
                Barcode = "880123456789",
                SalePrice = 85.50m,
                Stock = 500
            });

            Articulos.Add(new Article
            {
                Name = "Monitor Samsung 24\"",
                Barcode = "990123456789",
                SalePrice = 450.00m,
                Stock = 75
            });
        }
    }
}