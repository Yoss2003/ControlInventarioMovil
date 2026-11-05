using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using ControlInventarioMovil.Utilities;

namespace ControlInventarioMovil.ViewModels
{
    public partial class SelectableUnit : INotifyPropertyChanged
    {
        public MeasurementUnit Unit { get; set; } = null!;
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public partial class CategoriaPadreUI : Category, INotifyPropertyChanged
    {
        public ObservableCollection<Category> Subcategorias { get; set; } = [];
        public event PropertyChangedEventHandler? PropertyChanged;
        public bool TieneHijas => Subcategorias != null && Subcategorias.Count > 0;
        private bool _isExpanded;
        public string ToggleText => IsExpanded ? "🔼 Ocultar Subcategorías" : "🔽 Ver Subcategorías";

        public CategoriaPadreUI(Category b)
        {
            Id = b.Id; InventoryId = b.InventoryId; ParentCategoryId = b.ParentCategoryId; Name = b.Name; Description = b.Description;
            TrackingMode = b.TrackingMode ?? ControlInventario.Shared.Models.TrackingMode.Standard.ToString();
            NamingMethod = b.NamingMethod; IsReturnable = b.IsReturnable; SelectedUnitIds = b.SelectedUnitIds; IsActive = b.IsActive;
            Label1 = b.Label1; IsUnique1 = b.IsUnique1; Label2 = b.Label2; IsUnique2 = b.IsUnique2;
            Label3 = b.Label3; IsUnique3 = b.IsUnique3; Label4 = b.Label4; IsUnique4 = b.IsUnique4;
            Label5 = b.Label5; IsUnique5 = b.IsUnique5; Label6 = b.Label6; IsUnique6 = b.IsUnique6;
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); OnPropertyChanged(nameof(ToggleText)); } }
        }
        public void ActualizarEstadoHijas() => OnPropertyChanged(nameof(TieneHijas));
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class CategoriasViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;

        private ObservableCollection<CategoriaPadreUI> _categoriasPadre = [];
        public ObservableCollection<CategoriaPadreUI> CategoriasPadre
        {
            get => _categoriasPadre;
            set { _categoriasPadre = value; OnPropertyChanged(); }
        }

        public CategoriasViewModel()
        {
            _apiService = new ApiService();
        }

        public async Task<List<Category>> ObtenerCategoriasBrutasAsync()
        {
            return await _apiService.GetCategoriesAsync() ?? [];
        }

        public async Task<bool> EliminarCategoriaAsync(int id)
        {
            return await _apiService.DeleteCategoryAsync(id);
        }

        public async Task<List<Article>> ObtenerArticulosVinculadosAsync(int categoriaId)
        {
            try
            {
                var todosLosArticulos = await _apiService.GetArticlesAsync();
                return todosLosArticulos?.Where(a => a.CategoryId == categoriaId && a.ActionId != 6).ToList() ?? [];
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "CategoriasViewModel - ObtenerArticulosVinculadosAsync");
                return [];
            }
        }

        public async Task<bool> GuardarCategoriaAsync(Category categoria, bool esEdicion)
        {
            return esEdicion
                ? await _apiService.UpdateCategoryAsync(categoria)
                : await _apiService.CreateCategoryAsync(categoria);
        }

        public async Task<List<MeasurementUnit>> ObtenerUnidadesDeMedidaAsync(string modoActual)
        {
            var todasLasUnidades = await _apiService.GetMeasurementUnitsAsync() ?? [];

            return [.. todasLasUnidades.Where(u =>
            {
                string nombre = (u.UnitName ?? "").ToLower();

                if (modoActual == "Serializado")
                {
                    return nombre.Contains("unidad") || nombre.Contains("pieza") ||
                           nombre.Contains("equipo") || nombre.Contains("kit");
                }
                else if (modoActual == "Estándar")
                {
                    return nombre.Contains("unidad") || nombre.Contains("caja") ||
                           nombre.Contains("paquete") || nombre.Contains("docena") ||
                           nombre.Contains("display") || nombre.Contains("par") ||
                           nombre.Contains("millar");
                }
                else if (modoActual == "A Granel")
                {
                    return nombre.Contains("kilo") || nombre.Contains("gramo") ||
                           nombre.Contains("litro") || nombre.Contains("mililitro") ||
                           nombre.Contains("metro") || nombre.Contains("galón") ||
                           nombre.Contains("galon") || nombre.Contains("tonelada") ||
                           nombre.Contains("onza");
                }
                return true;
            })];
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}