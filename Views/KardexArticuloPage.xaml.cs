using ControlInventario.Shared.Models;
using System.Collections.ObjectModel;

namespace ControlInventarioMovil.Views
{
    public partial class KardexArticuloPage : ContentPage
    {
        // Propiedades para la Cabecera
        public string ArticleName { get; set; }
        public string Sku { get; set; }
        public string CurrentStock { get; set; }
        public decimal AverageCost { get; set; }

        // Lista para el Historial (La Línea de Tiempo)
        public ObservableCollection<KardexItemDto> TimelineList { get; set; } = new();

        public KardexArticuloPage()
        {
            InitializeComponent();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }

    // Esta es la clase "Envoltorio" que usaremos para mostrar el Kárdex en pantalla
    public class KardexItemDto
    {
        public int ActionId { get; set; }
        public string? MovementDate { get; set; }
        public string? Observation { get; set; }
        public string? OperationDisplay { get; set; }
        public decimal OperationTotalValue { get; set; }
        public int BalanceStock { get; set; }
        public decimal BalanceCost { get; set; }
    }
}