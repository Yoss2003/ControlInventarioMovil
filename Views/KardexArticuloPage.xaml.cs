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
        public double AverageCost { get; set; }

        // Lista para el Historial (La Línea de Tiempo)
        public ObservableCollection<KardexItemDto> TimelineList { get; set; } = new();

        public KardexArticuloPage()
        {
            InitializeComponent();

            // 1. Llenamos la cabecera con datos falsos para ver el diseño
            ArticleName = "Poco X7 Pro";
            Sku = "POC-12345";
            CurrentStock = "145";
            AverageCost = 1215.50;

            // 2. Llenamos la línea de tiempo con 2 movimientos de prueba
            TimelineList.Add(new KardexItemDto
            {
                ActionId = 1, // Entrada
                MovementDate = "25/08/2026 10:30",
                Observation = "Compra Fra. F001-992",
                OperationDisplay = "+ 50 un.",
                OperationTotalValue = 60000.00,
                BalanceStock = 145,
                BalanceCost = 1215.50
            });

            TimelineList.Add(new KardexItemDto
            {
                ActionId = 2, // Salida
                MovementDate = "24/08/2026 15:45",
                Observation = "Venta a Público General",
                OperationDisplay = "- 5 un.",
                OperationTotalValue = 7500.00,
                BalanceStock = 95,
                BalanceCost = 1200.00
            });

            // 3. Conectamos los datos a la pantalla
            BindingContext = this;
            listKardexTimeline.ItemsSource = TimelineList;
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
        public double OperationTotalValue { get; set; }
        public int BalanceStock { get; set; }
        public double BalanceCost { get; set; }
    }
}