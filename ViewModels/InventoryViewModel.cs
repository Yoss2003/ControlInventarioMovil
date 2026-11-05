using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.Services;
using ControlInventarioMovil.Services.IServices;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ControlInventarioMovil.ViewModels
{
    public partial class InventoryViewModel : INotifyPropertyChanged
    {
        private readonly ApiService _apiService;
        private readonly ILocalInventoryService _localService;
        private List<ArticleUI> _allArticlesCached = [];

        // --- ESTADOS GLOBALES ---
        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }

        public static string WarehouseName => UserSession.CurrentInventory != null
            ? (string.IsNullOrWhiteSpace(UserSession.CurrentInventory.Alias) ? UserSession.CurrentInventory.InventoryName.ToUpper() : UserSession.CurrentInventory.Alias.ToUpper())
            : "SINOPSIS: ALMACÉN INDEFINIDO";

        public static bool IsNuevoArticuloVisible => SecurityHelper.HasPermission("CREATE_ARTICLES");

        // --- FILTROS Y TABS ---
        private ArticleType _currentTab = ArticleType.Standard;
        private bool _mostrarStockCero = false;

        public ObservableCollection<ArticleUI> Articulos { get; } = [];

        private bool _isEmptyVisible;
        public bool IsEmptyVisible
        {
            get => _isEmptyVisible;
            set
            {
                _isEmptyVisible = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsListVisible));
            }
        }

        public bool IsListVisible => !IsEmptyVisible;

        private string _toggleStockButtonText = "Ver Agotados (Stock 0)";
        public string ToggleStockButtonText { get => _toggleStockButtonText; set { _toggleStockButtonText = value; OnPropertyChanged(); } }

        private Color _toggleStockButtonColor = Color.FromArgb("#2E3842");
        public Color ToggleStockButtonColor { get => _toggleStockButtonColor; set { _toggleStockButtonColor = value; OnPropertyChanged(); } }

        // Colores de las Pestañas (Tabs)
        public Color TabStandardBg => _currentTab == ArticleType.Standard ? Color.FromArgb("#8A2BE2") : Colors.Transparent;
        public Color TabStandardText => _currentTab == ArticleType.Standard ? Colors.White : Colors.Gray;
        public int TabStandardBorder => _currentTab == ArticleType.Standard ? 0 : 1;

        public Color TabBulkBg => _currentTab == ArticleType.Bulk ? Color.FromArgb("#8A2BE2") : Colors.Transparent;
        public Color TabBulkText => _currentTab == ArticleType.Bulk ? Colors.White : Colors.Gray;
        public int TabBulkBorder => _currentTab == ArticleType.Bulk ? 0 : 1;

        public Color TabSerialBg => _currentTab == ArticleType.Serialized ? Color.FromArgb("#8A2BE2") : Colors.Transparent;
        public Color TabSerialText => _currentTab == ArticleType.Serialized ? Colors.White : Colors.Gray;
        public int TabSerialBorder => _currentTab == ArticleType.Serialized ? 0 : 1;

        // --- OVERLAYS Y POPUPS ---
        // 1. Visor de Fotos
        private bool _isPhotoViewerVisible;
        public bool IsPhotoViewerVisible { get => _isPhotoViewerVisible; set { _isPhotoViewerVisible = value; OnPropertyChanged(); } }
        private ArticleUI? _articuloEnVisor;
        public string PhotoViewerTitle => _articuloEnVisor?.Name ?? "Vista Previa";
        public string? PhotoViewerSource => _articuloEnVisor?.MainPhotoPath;

        // 2. Múltiples Series
        private bool _isMultipleSeriesVisible;
        public bool IsMultipleSeriesVisible { get => _isMultipleSeriesVisible; set { _isMultipleSeriesVisible = value; OnPropertyChanged(); } }
        public string MultipleSeriesTitle { get; private set; } = "";
        public ObservableCollection<SerieIngresoItem> SeriesDinamicas { get; } = [];
        private ArticleUI? _articuloParaMultiplesSeries;

        // 3. Retiro de Stock
        private bool _isRetireOverlayVisible;
        public bool IsRetireOverlayVisible { get => _isRetireOverlayVisible; set { _isRetireOverlayVisible = value; OnPropertyChanged(); } }
        private ArticleUI? _articuloARetirar;
        private string? _rutaFotoRetiro;
        public string RetireName => _articuloARetirar?.Name ?? "";
        public string RetireStockText => $"Stock Actual: {_articuloARetirar?.Stock ?? 0}";

        private int _retireTypeIndex = 0;
        public int RetireTypeIndex
        {
            get => _retireTypeIndex;
            set { _retireTypeIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsRetireQuantityVisible)); }
        }
        public bool IsRetireQuantityVisible => RetireTypeIndex == 0;

        private string _retireQuantity = "";
        public string RetireQuantity { get => _retireQuantity; set { _retireQuantity = value; OnPropertyChanged(); } }

        private string _retireReason = "";
        public string RetireReason { get => _retireReason; set { _retireReason = value; OnPropertyChanged(); } }

        public string RetirePhotoStatus => string.IsNullOrEmpty(_rutaFotoRetiro) ? "(Opcional) Sin foto" : "✅ Foto adjuntada";
        public Color RetirePhotoColor => string.IsNullOrEmpty(_rutaFotoRetiro) ? Colors.Gray : (Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#A2D149") : Color.FromArgb("#2E7D32"));

        // --- COMANDOS PRINCIPALES ---
        public ICommand LoadDataCommand { get; }
        public ICommand ChangeTabCommand { get; }
        public ICommand ToggleStockZeroCommand { get; }
        public ICommand GoBackCommand { get; }
        public ICommand OpenConfigCommand { get; }
        public ICommand AddArticleCommand { get; }
        public ICommand EditArticleCommand { get; }

        // Comandos de Interacción por Artículo
        public ICommand ToggleExpansionCommand { get; }
        public ICommand IncreasePendingStockCommand { get; }
        public ICommand DecreasePendingStockCommand { get; }
        public ICommand SmartActionCommand { get; }
        public ICommand DeleteStockCommand { get; }

        // Comandos de Popups
        public ICommand OpenPhotoViewerCommand { get; }
        public ICommand ClosePhotoViewerCommand { get; }
        public ICommand ChangePhotoViewerCommand { get; }
        public ICommand DeletePhotoViewerCommand { get; }
        public ICommand CancelRetireCommand { get; }
        public ICommand ConfirmRetireCommand { get; }
        public ICommand TakeRetirePhotoCommand { get; }
        public ICommand EditSerialCommand { get; }
        public ICommand DeleteSerialCommand { get; }
        public ICommand CancelMultipleSeriesCommand { get; }
        public ICommand SaveMultipleSeriesCommand { get; }

        public ILocalInventoryService LocalService => _localService;

        public InventoryViewModel()
        {
            _apiService = new ApiService();
            _localService = new LocalInventoryService(); // Inyectamos el servicio SQLite extraído

            LoadDataCommand = new Command(async () => await SincronizarListadoArticulosAsync());
            ChangeTabCommand = new Command<string>(OnChangeTab);
            ToggleStockZeroCommand = new Command(OnToggleStockZero);

            GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
            OpenConfigCommand = new Command(async () => await Shell.Current.GoToAsync("CategoriasPage"));
            AddArticleCommand = new Command(async () => { UserSession.CurrentArticleToEdit = null; await Shell.Current.GoToAsync("ArticleFormPage", false); });
            EditArticleCommand = new Command<ArticleUI>(async (ui) => { if (ui != null) { UserSession.CurrentArticleToEdit = ClonarAArticleBase(ui); await Shell.Current.GoToAsync("ArticleFormPage", false); } });

            ToggleExpansionCommand = new Command<ArticleUI>(ui => ui?.ToggleExpansion());
            IncreasePendingStockCommand = new Command<ArticleUI>(ui => { if (ui != null) ui.PendingStock++; });
            DecreasePendingStockCommand = new Command<ArticleUI>(ui => { if (ui != null && ui.PendingStock > 0) ui.PendingStock--; });
            SmartActionCommand = new Command<ArticleUI>(async (ui) => await OnSmartActionAsync(ui));
            DeleteStockCommand = new Command<ArticleUI>(async (ui) => await OnDeleteStockRequestedAsync(ui));

            OpenPhotoViewerCommand = new Command<ArticleUI>(async (ui) => await OnOpenPhotoViewerAsync(ui));
            ClosePhotoViewerCommand = new Command(() => { IsPhotoViewerVisible = false; _articuloEnVisor = null; });
            ChangePhotoViewerCommand = new Command(async () => await OnChangePhotoViewerAsync());
            DeletePhotoViewerCommand = new Command(async () => await OnDeletePhotoViewerAsync());

            CancelRetireCommand = new Command(() => { IsRetireOverlayVisible = false; _articuloARetirar = null; _rutaFotoRetiro = null; });
            ConfirmRetireCommand = new Command(async () => await OnConfirmRetireAsync());
            TakeRetirePhotoCommand = new Command(async () => await OnTakeRetirePhotoAsync());

            EditSerialCommand = new Command<ArticleSerialDto>(async (dto) => await OnEditSerialAsync(dto));
            DeleteSerialCommand = new Command<ArticleSerialDto>(async (dto) => await OnDeleteSerialAsync(dto));
            CancelMultipleSeriesCommand = new Command(() => { IsMultipleSeriesVisible = false; SeriesDinamicas.Clear(); });
            SaveMultipleSeriesCommand = new Command(async () => await OnSaveMultipleSeriesAsync());
        }

        private static async Task MostrarAlertaAsync(string titulo, string mensaje, string boton)
        {
            if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page is Page page)
                await page.DisplayAlertAsync(titulo, mensaje, boton);
        }

        private static async Task<bool> MostrarConfirmacionAsync(string titulo, string mensaje, string aceptar, string cancelar)
        {
            if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page is Page page)
                return await page.DisplayAlertAsync(titulo, mensaje, aceptar, cancelar);
            return false;
        }

        private static async Task<string> MostrarPromptAsync(string titulo, string mensaje, string aceptar, string cancelar, string inicial = "")
        {
            if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page is Page page)
                return await page.DisplayPromptAsync(titulo, mensaje, aceptar, cancelar, initialValue: inicial);
            return "";
        }

        private async Task SincronizarListadoArticulosAsync()
        {
            var almacenActivo = UserSession.CurrentInventory;
            if (almacenActivo == null)
            {
                IsEmptyVisible = true;
                return;
            }

            IsBusy = true;
            try
            {
                // 1. Obtener datos locales y de la nube usando los servicios
                var articulosPendientes = await LocalService.GetPendingArticlesAsync(almacenActivo.Id);
                var resultApi = await _apiService.GetArticlesAsync() ?? [];
                var articulosNube = resultApi.Where(a => a.InventoryId == almacenActivo.Id).ToList();

                var articulosUnicos = articulosNube
                    .Concat(articulosPendientes.Where(p => !articulosNube.Any(n => n.Code == p.Code)))
                    .ToList();

                _allArticlesCached = [.. articulosUnicos.OrderByDescending(a => a.Id).Select(a =>
                {
                    var ui = new ArticleUI(a);
                    string trackingStr = a.Tracking.ToString().Trim();
                    bool esSerializado = trackingStr.Equals("Serialized", StringComparison.OrdinalIgnoreCase) || trackingStr.Equals("Serializado", StringComparison.OrdinalIgnoreCase) || (ui.Name != null && ui.Name.Contains("Poco", StringComparison.OrdinalIgnoreCase));
                    bool esBulk = trackingStr.Equals("Bulk", StringComparison.OrdinalIgnoreCase) || trackingStr.Equals("A Granel", StringComparison.OrdinalIgnoreCase) || a.MeasurementUnit == "MTS" || a.MeasurementUnit == "LTS" || a.MeasurementUnit == "KG";

                    if (esSerializado) ui.Type = ArticleType.Serialized;
                    else if (esBulk) ui.Type = ArticleType.Bulk;
                    else ui.Type = ArticleType.Standard;
                    return ui;
                })];

                // 2. Procesar detalles serializados con SQLite
                var todosLosDetallesActivos = await LocalService.GetActiveArticleDetailsAsync();
                var detallesAgrupados = todosLosDetallesActivos.GroupBy(d => d.ArticleId).ToDictionary(g => g.Key, g => g.ToList());
                var nuevosDetallesAGuardar = new List<ArticleDetails>();

                foreach (var ui in _allArticlesCached.Where(x => x.Type == ArticleType.Serialized))
                {
                    var detallesReales = detallesAgrupados.GetValueOrDefault(ui.Id) ?? [];
                    if (detallesReales.Count == 0 && ui.Stock > 0)
                    {
                        int cantidadACrear = (int)ui.Stock;
                        for (int i = 0; i < cantidadACrear; i++)
                        {
                            string serieAutogenerada = i == 0 && !string.IsNullOrWhiteSpace(ui.SerialNumber) ? ui.SerialNumber.Trim() : $"IMEI-AUTO-{ui.Id}-{i + 1}";
                            var nuevaHija = new ArticleDetails { ArticleId = ui.Id, SerialNumber = serieAutogenerada, StatusId = ui.StatusId ?? 1, IsActive = true, RegistrationDate = DateTime.UtcNow };
                            nuevosDetallesAGuardar.Add(nuevaHija);
                            detallesReales.Add(nuevaHija);
                        }
                    }

                    ui.Serials.Clear();
                    foreach (var detalle in detallesReales)
                    {
                        ui.Serials.Add(new ArticleSerialDto { Id = detalle.Id, SerialNumber = detalle.SerialNumber, Status = "Disponible", Location = "Almacén Principal" });
                    }
                    if (detallesReales.Count > 0) ui.Stock = ui.Serials.Count;
                }

                if (nuevosDetallesAGuardar.Count > 0)
                    await LocalService.SaveGeneratedSerialsAsync(nuevosDetallesAGuardar);

                // 3. Procesar clonaciones pendientes
                int pendingCloneStock = Preferences.Default.Get("PendingCloneStock", 0);
                if (pendingCloneStock > 0 && _allArticlesCached.Count > 0)
                {
                    var articuloRecienCreado = _allArticlesCached.OrderByDescending(a => a.Id).FirstOrDefault();
                    if (articuloRecienCreado != null)
                    {
                        articuloRecienCreado.PendingStock = pendingCloneStock;
                        if (articuloRecienCreado.IsSerialized)
                        {
                            await AbrirOverlayParaMultiplesSeries(articuloRecienCreado, pendingCloneStock, esClonacion: true);
                        }
                        else
                        {
                            articuloRecienCreado.Stock += pendingCloneStock;
                            articuloRecienCreado.PendingStock = 0;
                            var articuloUpdate = ClonarAArticleBase(articuloRecienCreado);
                            await _apiService.UpdateArticleAsync(articuloUpdate.Id, articuloUpdate);
                            // La recarga se hará en la siguiente iteración
                        }
                    }
                    Preferences.Default.Remove("PendingCloneStock");
                }

                FiltrarInventario();
            }
            finally { IsBusy = false; }
        }

        private void OnChangeTab(string tabName)
        {
            _currentTab = tabName switch
            {
                "Bulk" => ArticleType.Bulk,
                "Serialized" => ArticleType.Serialized,
                _ => ArticleType.Standard
            };
            OnPropertyChanged(nameof(TabStandardBg)); OnPropertyChanged(nameof(TabStandardText)); OnPropertyChanged(nameof(TabStandardBorder));
            OnPropertyChanged(nameof(TabBulkBg)); OnPropertyChanged(nameof(TabBulkText)); OnPropertyChanged(nameof(TabBulkBorder));
            OnPropertyChanged(nameof(TabSerialBg)); OnPropertyChanged(nameof(TabSerialText)); OnPropertyChanged(nameof(TabSerialBorder));
            FiltrarInventario();
        }

        private void OnToggleStockZero()
        {
            _mostrarStockCero = !_mostrarStockCero;
            ToggleStockButtonText = _mostrarStockCero ? "Ver Disponibles" : "Ver Agotados (Stock 0)";
            ToggleStockButtonColor = _mostrarStockCero ? Color.FromArgb("#EFA72F") : Color.FromArgb("#2E3842");
            FiltrarInventario();
        }

        private void FiltrarInventario()
        {
            var filtrados = _allArticlesCached
                .Where(a => a.Type == _currentTab)
                .Where(a => _mostrarStockCero ? a.Stock == 0 : a.Stock > 0)
                .ToList();

            Articulos.Clear();
            foreach (var item in filtrados) Articulos.Add(item);

            IsEmptyVisible = Articulos.Count == 0;
        }

        // --- LÓGICA DE BOTÓN INTELIGENTE (ADD STOCK / CLONE) ---
        private async Task OnSmartActionAsync(ArticleUI ui)
        {
            if (ui == null) return;
            if (ui.PendingStock < 0)
            {
                await InventoryViewModel.MostrarAlertaAsync("Acción Denegada", "Utiliza el botón de la papelera (eliminar) para registrar mermas.", "Entendido");
                ui.PendingStock = 0;
                return;
            }
            if (ui.PendingStock == 0) return;

            if (ui.Type != ArticleType.Bulk && ui.PendingStock % 1 != 0)
            {
                await InventoryViewModel.MostrarAlertaAsync("Formato Inválido", "Los productos empaquetados solo aceptan números enteros.", "Entendido");
                return;
            }

            if (ui.IsSerialized)
            {
                await AbrirOverlayParaClonacion(ui, ui.PendingStock);
            }
            else
            {
                IsBusy = true;
                decimal cantidadAgregada = ui.PendingStock;
                ui.Stock += ui.PendingStock;
                ui.PendingStock = 0;

                int empleadoIdReal = Preferences.Get("UserId", 1);
                var articuloUpdate = ClonarAArticleBase(ui);
                articuloUpdate.CurrentEmployeeId = empleadoIdReal;

                var mov = new Movement { ArticleId = articuloUpdate.Id, EmployeeId = empleadoIdReal, ActionId = 1, MovementDate = DateTime.UtcNow, Observation = "Ajuste manual", Amount = cantidadAgregada };

                bool localSuccess = await LocalService.RegisterStockAdditionAsync(articuloUpdate, mov);
                if (localSuccess)
                {
                    bool nubeSuccess = await _apiService.UpdateArticleAsync(articuloUpdate.Id, articuloUpdate);
                    if (!nubeSuccess) await InventoryViewModel.MostrarAlertaAsync("Modo Offline", $"Se agregaron +{cantidadAgregada} unidades localmente.", "OK");
                }
                IsBusy = false;
            }
        }

        private async Task AbrirOverlayParaClonacion(ArticleUI original, decimal cantidadInicial)
        {
            IsBusy = true;
            Category? cat = null;
            try { cat = (await _apiService.GetCategoriesAsync())?.FirstOrDefault(c => c.Id == original.CategoryId); } catch { }
            IsBusy = false;

            string labelUnico = "";
            if (cat != null)
            {
                if (cat.IsUnique1) labelUnico = cat.Label1 ?? "";
                else if (cat.IsUnique2) labelUnico = cat.Label2 ?? "";
                else if (cat.IsUnique3) labelUnico = cat.Label3 ?? "";
                else if (cat.IsUnique4) labelUnico = cat.Label4 ?? "";
                else if (cat.IsUnique5) labelUnico = cat.Label5 ?? "";
            }

            string valorUnicoResultado = "";
            if (!string.IsNullOrEmpty(labelUnico))
            {
                valorUnicoResultado = await InventoryViewModel.MostrarPromptAsync("Atributo", $"Ingresa el valor para '{labelUnico}':", "Continuar", "Cancelar");
                if (string.IsNullOrWhiteSpace(valorUnicoResultado)) return;
            }

            var clon = ClonarAArticleBase(original);
            clon.Id = 0; clon.Barcode = string.Empty; clon.Stock = 0;
            if (labelUnico == cat?.Label1) clon.Characteristics = valorUnicoResultado;

            IsBusy = true;
            bool creado = await _apiService.CreateArticleAsync(clon);
            IsBusy = false;
            if (!creado) { await InventoryViewModel.MostrarAlertaAsync("Error", "No se pudo crear la base de la variante.", "OK"); return; }

            await SincronizarListadoArticulosAsync();
            var recienCreado = _allArticlesCached.OrderByDescending(a => a.Id).FirstOrDefault();
            if (recienCreado != null)
            {
                recienCreado.PendingStock = cantidadInicial;
                await AbrirOverlayParaMultiplesSeries(recienCreado, cantidadInicial, esClonacion: true, valorUnicoFijo: valorUnicoResultado);
            }
        }

        private async Task AbrirOverlayParaMultiplesSeries(ArticleUI articulo, decimal cantidad, bool esClonacion = false, string valorUnicoFijo = "")
        {
            _articuloParaMultiplesSeries = articulo;
            MultipleSeriesTitle = $"{articulo.Name}\n(+{cantidad} unidades)";
            OnPropertyChanged(nameof(MultipleSeriesTitle));

            bool s1 = false, s2 = false, s3 = false, s4 = false, s5 = false, s6 = false;
            string l1 = "", l2 = "", l3 = "", l4 = "", l5 = "", l6 = "";

            if (esClonacion)
            {
                IsBusy = true;
                try
                {
                    var cat = (await _apiService.GetCategoriesAsync())?.FirstOrDefault(c => c.Id == articulo.CategoryId);
                    if (cat != null)
                    {
                        s1 = cat.IsUnique1; l1 = cat.Label1 ?? "";
                        s2 = cat.IsUnique2; l2 = cat.Label2 ?? "";
                        s3 = cat.IsUnique3; l3 = cat.Label3 ?? "";
                        s4 = cat.IsUnique4; l4 = cat.Label4 ?? "";
                        s5 = cat.IsUnique5; l5 = cat.Label5 ?? "";
                        s6 = cat.IsUnique6; l6 = cat.Label6 ?? "";
                    }
                }
                finally { IsBusy = false; }
            }

            SeriesDinamicas.Clear();
            for (int i = 1; i <= cantidad; i++)
            {
                SeriesDinamicas.Add(new SerieIngresoItem { Index = i, ShowAttr1 = s1, LabelAttr1 = l1, ValAttr1 = valorUnicoFijo, ShowAttr2 = s2, LabelAttr2 = l2, ShowAttr3 = s3, LabelAttr3 = l3, ShowAttr4 = s4, LabelAttr4 = l4, ShowAttr5 = s5, LabelAttr5 = l5, ShowAttr6 = s6, LabelAttr6 = l6 });
            }
            IsMultipleSeriesVisible = true;
        }

        private async Task OnSaveMultipleSeriesAsync()
        {
            if (_articuloParaMultiplesSeries == null) return;
            if (SeriesDinamicas.Any(x => string.IsNullOrWhiteSpace(x.SerialNumber)))
            {
                await InventoryViewModel.MostrarAlertaAsync("Datos Incompletos", "Todas las unidades deben tener un Número de Serie/IMEI asignado.", "Entendido");
                return;
            }

            IsBusy = true;
            bool falloLote = false;
            int agregadosConExito = 0;
            var nuevosDetalles = new List<ArticleDetails>();

            foreach (var item in SeriesDinamicas)
            {
                var nuevaSerie = new ArticleDetails
                {
                    ArticleId = _articuloParaMultiplesSeries.Id,
                    SerialNumber = item.SerialNumber,
                    Attr1 = item.ShowAttr1 ? item.ValAttr1 : null,
                    Attr2 = item.ShowAttr2 ? item.ValAttr2 : null,
                    Attr3 = item.ShowAttr3 ? item.ValAttr3 : null,
                    StatusId = _articuloParaMultiplesSeries.StatusId ?? 1,
                    IsActive = true,
                    RegistrationDate = DateTime.UtcNow
                };
                if (await _apiService.AddArticleDetailAsync(nuevaSerie)) agregadosConExito++;
                else falloLote = true;
            }

            if (agregadosConExito > 0)
            {
                _articuloParaMultiplesSeries.Stock += agregadosConExito;
                _articuloParaMultiplesSeries.PendingStock = 0;
                var articuloUpdate = ClonarAArticleBase(_articuloParaMultiplesSeries);
                articuloUpdate.CurrentEmployeeId = Preferences.Get("UserId", 1);

                var mov = new Movement { ArticleId = articuloUpdate.Id, EmployeeId = articuloUpdate.CurrentEmployeeId, ActionId = 1, MovementDate = DateTime.UtcNow, Observation = "Ingreso manual serializado", Amount = agregadosConExito };

                await LocalService.RegisterStockAdditionAsync(articuloUpdate, mov);
                await _apiService.UpdateArticleAsync(articuloUpdate.Id, articuloUpdate);
                await _apiService.CreateMovementAsync(mov);
                await SincronizarListadoArticulosAsync();
            }

            IsBusy = false;
            if (falloLote) await InventoryViewModel.MostrarAlertaAsync("Advertencia", $"Se agregaron {agregadosConExito} unidades, pero hubo errores con las demás.", "OK");
            else IsMultipleSeriesVisible = false;
        }

        // --- LÓGICA DE RETIRO DE STOCK ---
        private async Task OnDeleteStockRequestedAsync(ArticleUI ui)
        {
            if (!SecurityHelper.HasPermission("DELETE_RECORDS"))
            {
                await InventoryViewModel.MostrarAlertaAsync("Acceso Denegado", "Tu rol no tiene permisos para eliminar registros o vaciar stock.", "Entendido");
                return;
            }
            if (ui.IsSerialized)
            {
                await InventoryViewModel.MostrarAlertaAsync("Aviso", "Para artículos serializados, abre el acordeón y elimina cada Número de Serie individualmente.", "Entendido");
                return;
            }

            _articuloARetirar = ui;
            _rutaFotoRetiro = null;
            RetireTypeIndex = 0;
            RetireQuantity = "";
            RetireReason = "";
            OnPropertyChanged(nameof(RetireName)); OnPropertyChanged(nameof(RetireStockText)); OnPropertyChanged(nameof(RetirePhotoStatus)); OnPropertyChanged(nameof(RetirePhotoColor));
            IsRetireOverlayVisible = true;
        }

        private async Task OnConfirmRetireAsync()
        {
            if (_articuloARetirar == null) return;
            if (string.IsNullOrWhiteSpace(RetireReason) || RetireReason.Length < 5)
            {
                await InventoryViewModel.MostrarAlertaAsync("Validación", "Debes ingresar un motivo válido (Mínimo 5 caracteres).", "Entendido");
                return;
            }

            bool esVaciadoTotal = RetireTypeIndex == 1;
            decimal cantidadRetirada;
            if (esVaciadoTotal) cantidadRetirada = _articuloARetirar.Stock;
            else
            {
                if (!decimal.TryParse(RetireQuantity, out cantidadRetirada) || cantidadRetirada <= 0) { await InventoryViewModel.MostrarAlertaAsync("Validación", "Ingresa una cantidad válida mayor a 0.", "OK"); return; }
                if (cantidadRetirada > _articuloARetirar.Stock) { await InventoryViewModel.MostrarAlertaAsync("Validación", $"El stock máximo es {_articuloARetirar.Stock}.", "OK"); return; }
            }

            IsBusy = true;
            _articuloARetirar.Stock -= cantidadRetirada;
            if (_articuloARetirar.Stock <= 0) { _articuloARetirar.Stock = 0; _articuloARetirar.IsActive = false; }

            var articuloUpdate = ClonarAArticleBase(_articuloARetirar);
            var mov = new Movement { ArticleId = articuloUpdate.Id, EmployeeId = Preferences.Get("UserId", 1), ActionId = 2, MovementDate = DateTime.UtcNow, Observation = $"Retiro: {RetireReason}", Amount = cantidadRetirada, PhotoPath = _rutaFotoRetiro, IsSynced = false };

            bool localSuccess = await LocalService.RegisterStockWithdrawalAsync(articuloUpdate, mov);
            if (localSuccess)
            {
                bool api1 = await _apiService.UpdateArticleAsync(articuloUpdate.Id, articuloUpdate);
                bool api2 = await _apiService.CreateMovementAsync(mov);
                if (api1 && api2) {}
                await InventoryViewModel.MostrarAlertaAsync("Éxito", $"Se retiraron {cantidadRetirada} unidades.", "OK");
            }

            IsBusy = false;
            IsRetireOverlayVisible = false;
            await SincronizarListadoArticulosAsync();
        }

        private async Task OnTakeRetirePhotoAsync()
        {
            if (MediaPicker.Default.IsCaptureSupported)
            {
                var foto = await MediaPicker.Default.CapturePhotoAsync();
                if (foto != null)
                {
                    _rutaFotoRetiro = foto.FullPath;
                    OnPropertyChanged(nameof(RetirePhotoStatus)); OnPropertyChanged(nameof(RetirePhotoColor));
                }
            }
        }

        // --- LÓGICA DE EDICIÓN DE SERIES ---
        private async Task OnEditSerialAsync(ArticleSerialDto serie)
        {
            string nuevoImei = await InventoryViewModel.MostrarPromptAsync("Editar Serie", "Modifica el número de serie:", "Guardar", "Cancelar", serie.SerialNumber);
            if (string.IsNullOrWhiteSpace(nuevoImei) || nuevoImei == serie.SerialNumber) return;
            if (await LocalService.UpdateSerialNumberAsync(serie.Id, nuevoImei))
            {
                await _apiService.UpdateArticleDetailAsync(serie.Id, new ArticleDetails { Id = serie.Id, SerialNumber = nuevoImei });
                await SincronizarListadoArticulosAsync();
            }
        }

        private async Task OnDeleteSerialAsync(ArticleSerialDto serie)
        {
            bool conf = await InventoryViewModel.MostrarConfirmacionAsync("Retirar Unidad", $"¿Retirar físicamente la serie {serie.SerialNumber}?", "Sí", "Cancelar");
            if (!conf) return;
            if (await LocalService.DeactivateSerialAsync(serie.Id))
            {
                await _apiService.DeleteArticleDetailAsync(serie.Id);
                await SincronizarListadoArticulosAsync();
            }
        }

        // --- VISOR DE FOTOS (Cerebro) ---
        private async Task OnOpenPhotoViewerAsync(ArticleUI ui)
        {
            if (ui == null) return;
            if (!string.IsNullOrWhiteSpace(ui.MainPhotoPath))
            {
                _articuloEnVisor = ui;
                OnPropertyChanged(nameof(PhotoViewerTitle)); OnPropertyChanged(nameof(PhotoViewerSource));
                IsPhotoViewerVisible = true;
            }
            else await ProcederCargaDeFoto(ui);
        }

        private async Task OnChangePhotoViewerAsync() { IsPhotoViewerVisible = false; await ProcederCargaDeFoto(_articuloEnVisor); }
        private async Task OnDeletePhotoViewerAsync() { IsPhotoViewerVisible = false; await EjecutarActualizacionDeFoto(_articuloEnVisor, null); }

        private async Task ProcederCargaDeFoto(ArticleUI? ui)
        {
            if (ui == null) return;
            if (Application.Current?.Windows.Count == 0 || Application.Current!.Windows[0].Page is not Page page) return;
            string accion = await page.DisplayActionSheetAsync("Foto", "Cancelar", null, "Tomar con Cámara", "Elegir de Galería");
            FileResult? foto = null;
            if (accion == "Tomar con Cámara" && MediaPicker.Default.IsCaptureSupported) foto = await MediaPicker.Default.CapturePhotoAsync();
            else if (accion == "Elegir de Galería") foto = (await MediaPicker.Default.PickPhotosAsync())?.FirstOrDefault();

            if (foto != null) await EjecutarActualizacionDeFoto(ui, foto.FullPath);
        }

        private async Task EjecutarActualizacionDeFoto(ArticleUI? ui, string? nuevaRuta)
        {
            if (ui == null) return;
            ui.MainPhotoPath = nuevaRuta;
            var artUpdate = ClonarAArticleBase(ui);
            await _apiService.UpdateArticleAsync(artUpdate.Id, artUpdate);
            await SincronizarListadoArticulosAsync();
        }

        public static Article ClonarAArticleBase(Article a)
        {
            return new Article
            {
                Id = a.Id,
                InventoryId = a.InventoryId,
                Code = a.Code,
                Barcode = a.Barcode,
                Name = a.Name,
                Model = a.Model,
                CategoryId = a.CategoryId,
                BrandId = a.BrandId,
                Tracking = a.Tracking,
                MeasurementUnit = a.MeasurementUnit,
                Stock = a.Stock,
                SerialNumber = a.SerialNumber,
                AcquisitionPrice = a.AcquisitionPrice,
                SalePrice = a.SalePrice,
                AcquisitionCurrency = a.AcquisitionCurrency,
                SaleCurrency = a.SaleCurrency,
                AcquisitionDate = a.AcquisitionDate,
                UsefulLifeMonths = a.UsefulLifeMonths,
                WarrantyEndDate = a.WarrantyEndDate,
                Characteristics = a.Characteristics,
                Observation = a.Observation,
                StatusId = a.StatusId,
                LocationId = a.LocationId,
                ConditionId = a.ConditionId,
                SupplierId = a.SupplierId,
                MainPhotoPath = a.MainPhotoPath,
                MainVoucherPath = a.MainVoucherPath,
                ActionId = a.ActionId,
                RegistrationDate = a.RegistrationDate,
                ModificationDate = a.ModificationDate,
                DecommissionDate = a.DecommissionDate,
                DepartureDate = a.DepartureDate,
                Presentation = a.Presentation,
                AcquisitionUnit = a.AcquisitionUnit,
                SaleUnit = a.SaleUnit,
                ConversionFactor = a.ConversionFactor,
                CurrentEmployeeId = a.CurrentEmployeeId,
                PreviousEmployeeId = a.PreviousEmployeeId,
                FixedAsset = a.FixedAsset
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // =========================================================================
    // CLASES AUXILIARES (Migradas del Code-Behind original)
    // =========================================================================
    public enum ArticleType { Standard, Bulk, Serialized }

    public class ArticleSerialDto
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    public class SerieIngresoItem
    {
        public int Index { get; set; }
        public string Titulo => $"📦 UNIDAD FÍSICA #{Index}";
        public string SerialNumber { get; set; } = string.Empty;
        public bool ShowAttr1 { get; set; }
        public string LabelAttr1 { get; set; } = string.Empty;
        public string ValAttr1 { get; set; } = string.Empty;
        public bool ShowAttr2 { get; set; }
        public string LabelAttr2 { get; set; } = string.Empty;
        public string ValAttr2 { get; set; } = string.Empty;
        public bool ShowAttr3 { get; set; }
        public string LabelAttr3 { get; set; } = string.Empty;
        public string ValAttr3 { get; set; } = string.Empty;
        public bool ShowAttr4 { get; set; }
        public string LabelAttr4 { get; set; } = string.Empty;
        public string ValAttr4 { get; set; } = string.Empty;
        public bool ShowAttr5 { get; set; }
        public string LabelAttr5 { get; set; } = string.Empty;
        public string ValAttr5 { get; set; } = string.Empty;
        public bool ShowAttr6 { get; set; }
        public string LabelAttr6 { get; set; } = string.Empty;
        public string ValAttr6 { get; set; } = string.Empty;
    }

    public partial class ArticleUI : Article
    {
        public ArticleType Type { get; set; } = ArticleType.Standard;
        public ObservableCollection<ArticleSerialDto> Serials { get; set; } = [];
        public bool IsSerialized => Type == ArticleType.Serialized;

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); OnPropertyChanged(nameof(ExpansionIcon)); } }
        }
        public string ExpansionIcon => IsExpanded ? "🔼" : "🔽";

        public void ToggleExpansion() { if (IsSerialized) IsExpanded = !IsExpanded; }

        public string AcquisitionDisplay => $"{((string.IsNullOrWhiteSpace(AcquisitionCurrency)) ? "S/." : AcquisitionCurrency.Trim())} {(AcquisitionPrice ?? 0):F2}";
        public string OriginalSaleDisplay => $"{((string.IsNullOrWhiteSpace(SaleCurrency)) ? "S/." : SaleCurrency.Trim())} {(SalePrice ?? 0):F2}";

        public new string? MainPhotoPath { get => base.MainPhotoPath; set { if (base.MainPhotoPath != value) { base.MainPhotoPath = value; OnPropertyChanged(nameof(MainPhotoPath)); } } }

        public string ConvertedSaleDisplay
        {
            get
            {
                if (SalePrice == null) return "S/. 0.00";
                string symbol = string.IsNullOrWhiteSpace(SaleCurrency) ? "S/." : SaleCurrency.Trim();
                if (symbol == "S/.") return $"S/. {SalePrice.Value:F2}";
                decimal tipoCambioVenta = 0;
                if (symbol == "$" && UserSession.TodayExchangeRateUSD != null) tipoCambioVenta = UserSession.TodayExchangeRateUSD.SellPrice;
                else if (symbol == "€" && UserSession.TodayExchangeRateEUR != null) tipoCambioVenta = UserSession.TodayExchangeRateEUR.SellPrice;
                if (tipoCambioVenta > 0) return $"S/. {(SalePrice.Value * tipoCambioVenta):F2}";
                return $"S/. {SalePrice.Value:F2}";
            }
        }

        public bool IsConversionVisible => (!string.IsNullOrWhiteSpace(SaleCurrency) && SaleCurrency.Trim() != "S/.");
        public static bool ShowThumbnail => Preferences.Default.Get("UI_ShowThumbnails", true);

        public ArticleUI(Article a)
        {
            if (a == null) return;
            Id = a.Id; InventoryId = a.InventoryId; Code = a.Code; Barcode = a.Barcode; Name = a.Name; Model = a.Model; CategoryId = a.CategoryId; BrandId = a.BrandId; Tracking = a.Tracking; MeasurementUnit = a.MeasurementUnit; Stock = a.Stock; SerialNumber = a.SerialNumber; AcquisitionPrice = a.AcquisitionPrice; SalePrice = a.SalePrice; AcquisitionCurrency = a.AcquisitionCurrency; SaleCurrency = a.SaleCurrency; AcquisitionDate = a.AcquisitionDate; UsefulLifeMonths = a.UsefulLifeMonths; WarrantyEndDate = a.WarrantyEndDate; Characteristics = a.Characteristics; Observation = a.Observation; StatusId = a.StatusId; LocationId = a.LocationId; ConditionId = a.ConditionId; SupplierId = a.SupplierId; MainPhotoPath = a.MainPhotoPath; MainVoucherPath = a.MainVoucherPath; ActionId = a.ActionId; RegistrationDate = a.RegistrationDate; ModificationDate = a.ModificationDate; DecommissionDate = a.DecommissionDate; DepartureDate = a.DepartureDate; Presentation = a.Presentation; AcquisitionUnit = a.AcquisitionUnit; SaleUnit = a.SaleUnit; ConversionFactor = a.ConversionFactor; CurrentEmployeeId = a.CurrentEmployeeId; PreviousEmployeeId = a.PreviousEmployeeId; FixedAsset = a.FixedAsset;
        }

        private decimal _pendingStock;
        public decimal PendingStock
        {
            get => _pendingStock;
            set { if (_pendingStock != value) { _pendingStock = value; OnPropertyChanged(nameof(PendingStock)); OnPropertyChanged(nameof(DisplayStock)); OnPropertyChanged(nameof(ActionColor)); OnPropertyChanged(nameof(ActionIcon)); } }
        }

        public decimal DisplayStock
        {
            get => Stock + _pendingStock;
            set { decimal diferencia = value - Stock; if (_pendingStock != diferencia) PendingStock = diferencia; }
        }

        public Color ActionColor => PendingStock > 0 ? Color.FromArgb("#EFA72F") : Color.FromArgb("#A2D149");
        public string ActionIcon => PendingStock > 0 ? "save_icon.png" : "clone_icon.png";

        public string MeasurementUnitShort
        {
            get
            {
                if (string.IsNullOrWhiteSpace(MeasurementUnit)) return "";
                string unit = MeasurementUnit.Trim();
                if (UserSession.UnidadesMedidaCache.TryGetValue(unit, out string? abbreviation)) return abbreviation;
                return unit.Length <= 3 ? unit.ToUpper() : unit[..3].ToUpper();
            }
        }
    }
}