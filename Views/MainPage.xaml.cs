using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Data;
using ControlInventarioMovil.Helpers;
using ControlInventarioMovil.Services;
using ControlInventarioMovil.Utilities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ControlInventarioMovil.Views
{
    public partial class MainPage : ContentPage
    {
        private readonly ApiService _apiService;
        // ==========================================
        // CONFIGURACIÓN PREMIUM DEL DIAL ORBITAL
        // ==========================================
        private const double RadioOrbita = 105;
        private const double EscalaZoomZenit = 1.25;
        private const int TiempoInactividadSegundos = 5;
        private const int DuracionGiroMs = 1200;
        private const int TiempoExposicionMs = 5000;
        private const int VelocidadRadarLineaMs = 2500;

        private int _pasoActual = 0;
        private double _anguloAcumuladoRad = 0;
        private bool _estaAnimando = false;
        private bool _faseDeMovimientoActiva = false;
        private bool _solicitudDetenerDespuesDelPaso = false;
        private bool _estaNavegando = false;
        private Article? _articuloRapidoEncontrado;

        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _radarCts;
        private IDispatcherTimer? _inactivityTimer;
        private readonly List<Grid> _botonesOrbitales;
        private readonly List<Label> _textosOrbitales;
        private List<Inventory> _almacenesDisponibles = [];

        public MainPage()
        {
            InitializeComponent();
            _botonesOrbitales = [OrbitaRegistros, OrbitaInventario, OrbitaReportes, OrbitaConfig];
            _textosOrbitales = [TxtRegistros, TxtInventario, TxtReportes, TxtConfig];
            ConfigurarEventosDeToque();

            _anguloAcumuladoRad = _pasoActual * (Math.PI / 2);
            ActualizarPosicionesNodalesOnly(_anguloAcumuladoRad);

            _cts = new CancellationTokenSource();
            SetupInactivityTimer();

            _apiService = new ApiService();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            _estaNavegando = false;

            // 1. ESCUDO DE SEGURIDAD 
            if (UserSession.CurrentUser == null)
            {
                Debug.WriteLine("[SEGURIDAD] Sesión vacía. Restableciendo LoginPage como raíz.");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (Application.Current?.Windows.Count > 0)
                    {
                        Application.Current.Windows[0].Page = new Views.LoginPage();
                    }
                });
                return;
            }

            // 2. ANIMACIÓN DEL RADAR ENERGÉTICO
            if (_radarCts == null || _radarCts.IsCancellationRequested)
            {
                _radarCts = new CancellationTokenSource();
                _ = AnimateAroEnergiaInfiniteSmooth(_radarCts.Token);
            }

            // 3. FORMATEO DE BIENVENIDA AL USUARIO 
            string firstName = UserSession.CurrentUser.Employee?.FirstName?.Trim() ?? "";
            string lastName = UserSession.CurrentUser.Employee?.LastName?.Trim() ?? "";
            string userRole = UserSession.CurrentUser.Role?.Name?.Trim() ?? "Usuario";

            string apellido = "";
            string nombre = UserSession.CurrentUser.Username ?? "Móvil";

            if (!string.IsNullOrEmpty(firstName))
            {
                var parteDelNombre = firstName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parteDelNombre.Length > 0) nombre = parteDelNombre[0];
            }

            if (!string.IsNullOrWhiteSpace(lastName))
            {
                var partesDelApellido = lastName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (partesDelApellido.Length >= 2)
                    apellido = $"{partesDelApellido[0][0]}.{partesDelApellido[1][0]}.";
                else if (partesDelApellido.Length == 1)
                    apellido = $"{partesDelApellido[0][0]}.";
            }

            lblNombre.Text = $"Hola, {nombre} {apellido}".Trim();
            lblRol.Text = $"Rol: {userRole}";

            // 4. VERIFICACIÓN Y CARGA DEL INVENTARIO ACTIVO
            if (UserSession.CurrentInventory == null)
            {
                try
                {
                    var listaInventarios = await _apiService.GetInventoriesAsync();
                    if (listaInventarios != null && listaInventarios.Any())
                    {
                        UserSession.CurrentInventory = listaInventarios.FirstOrDefault();
                        Debug.WriteLine($"[WORKSPACE] Entorno activo establecido: {UserSession.CurrentInventory?.InventoryName}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error al inicializar entornos: {ex.Message}");
                }
            }

            // 5. REFRESH DE INTERFAZ Y PROCESAMIENTO DE STOCK TOTAL
            await CargarAmbientesDeTrabajoAsync();
            await ActualizarStockCircularAsync();
            await CalcularAlertasGlobalesAsync();
            await SincronizarUnidadesMedidaAsync();

            _ = Task.Run(async () =>
            {
                if (UserSession.TodayExchangeRateUSD == null)
                {
                    var usd = await _apiService.GetTodayExchangeRateAsync("USD");
                    if (usd != null) UserSession.TodayExchangeRateUSD = usd;
                }

                if (UserSession.TodayExchangeRateEUR == null)
                {
                    var eur = await _apiService.GetTodayExchangeRateAsync("EUR");
                    if (eur != null) UserSession.TodayExchangeRateEUR = eur;
                }
            });

            OrbitaReportes.IsVisible = SecurityHelper.HasPermission("VIEW_REPORTS");
            OrbitaConfig.IsVisible = SecurityHelper.HasPermission("MANAGE_SETTINGS");
            
            BtnCompartirInventario.IsVisible = UserSession.CurrentProfile?.SharedActivity == true;

            bool tieneSmtp = !string.IsNullOrEmpty(UserSession.CurrentProfile?.SmtpEmail);
            if (!tieneSmtp)
            {
                BtnEmpleados.Opacity = 0.4;
            }
            else
            {
                BtnEmpleados.Opacity = 1.0;
            }

            ResetInactivityTimer();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            ContenedorOrbital.Opacity = 0;

            this.AbortAnimation("GiroOrbital");
            int pasoMasCercano = (int)Math.Round(_anguloAcumuladoRad / (Math.PI / 2));
            _pasoActual = (pasoMasCercano % 4 + 4) % 4;
            _anguloAcumuladoRad = _pasoActual * (Math.PI / 2);
            ActualizarPosicionesNodalesOnly(_anguloAcumuladoRad);
            ContenedorOrbital.Opacity = 1;

            if (_radarCts != null && !_radarCts.IsCancellationRequested)
            {
                _radarCts.Cancel();
                _radarCts.Dispose();
                _radarCts = null;
            }

            StopOrbitalAnimation();
            this.AbortAnimation("GiroOrbital");
            this.CancelAnimations();

            AroEnergia.CancelAnimations();
            foreach (var boton in _botonesOrbitales) boton.CancelAnimations();
        }

        private async Task CargarAmbientesDeTrabajoAsync()
        {
            try
            {
                PkrAmbienteTrabajo.SelectedIndexChanged -= OnAmbienteTrabajoChanged;

                var apiService = new ControlInventarioMovil.Services.ApiService();
                var lista = await apiService.GetInventoriesAsync();

                if (lista != null)
                {
                    PkrAmbienteTrabajo.Items.Clear();

                    _almacenesDisponibles = [.. lista.Where(i => i.Id != 0 && i.IsActive)];

                    _almacenesDisponibles.ForEach(inv =>
                        PkrAmbienteTrabajo.Items.Add(string.IsNullOrWhiteSpace(inv.Alias) ? inv.InventoryName : inv.Alias));

                    if (UserSession.CurrentInventory != null && UserSession.CurrentInventory.Id != 0)
                    {
                        int index = _almacenesDisponibles.FindIndex(i => i.Id == UserSession.CurrentInventory.Id);
                        if (index >= 0) PkrAmbienteTrabajo.SelectedIndex = index;
                    }
                    else if (_almacenesDisponibles.Count != 0)
                    {
                        PkrAmbienteTrabajo.SelectedIndex = 0;
                        UserSession.CurrentInventory = _almacenesDisponibles.First();
                    }

                    PkrAmbienteTrabajo.SelectedIndexChanged += OnAmbienteTrabajoChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WORKSPACE_ERROR] {ex.Message}");

                PkrAmbienteTrabajo.Items.Clear();
                PkrAmbienteTrabajo.Items.Add("Error de conexión");
                PkrAmbienteTrabajo.SelectedIndex = 0;
            }
        }

        // Evento que se dispara al cambiar de Almacén/Bodega en el Picker
        private async void OnAmbienteTrabajoChanged(object? sender, EventArgs? e)
        {
            if (PkrAmbienteTrabajo.SelectedIndex == -1) return;

            UserSession.CurrentInventory = _almacenesDisponibles[PkrAmbienteTrabajo.SelectedIndex];
            _ = string.IsNullOrWhiteSpace(UserSession.CurrentInventory.Alias)
                ? UserSession.CurrentInventory.InventoryName
                : UserSession.CurrentInventory.Alias;

            await ActualizarStockCircularAsync();
        }

        // Método elástico para crear un nuevo inventario usando prompts nativos
        private async void OnCrearNuevoAlmacenClicked(object sender, EventArgs e)
        {
            string nombreIngresado = await DisplayPromptAsync("Nuevo Ambiente", "Escribe el nombre de la nueva Bodega o Almacén corporativo:", "Guardar", "Cancelar", "Ej. Almacén del Norte");

            if (string.IsNullOrWhiteSpace(nombreIngresado)) return;

            var now = DateTime.Now;
            string username = UserSession.CurrentUser?.Username ?? "Admin";
            string mmss = now.ToString("mmss");

            var nuevoInventario = new Inventory
            {
                InventoryName = $"{username}_Invent_{mmss}",
                CreationDate = now.ToString("yyyy-MM-dd HH:mm:ss"),
                UserId = UserSession.CurrentUser?.Id ?? 1,
                Username = username,
                Alias = nombreIngresado.Trim()
            };

            var apiService = new ControlInventarioMovil.Services.ApiService();
            bool creado = await apiService.CreateInventoryAsync(nuevoInventario);

            if (creado)
            {
                // 👇 ASOCIACIÓN DINÁMICA DE MONEDA BASE EN SEGUNDO PLANO
                try
                {
                    // Sincronizamos la lista local para pescar el ID autogenerado del nuevo almacén
                    var listaActualizada = await apiService.GetInventoriesAsync();
                    var almacenRegistrado = listaActualizada.FirstOrDefault(i => i.InventoryName == nuevoInventario.InventoryName);

                    if (almacenRegistrado != null)
                    {
                        // Registramos un parámetro de configuración financiera amarrado a este almacén
                        var parametroMoneda = new Parameters
                        {
                            InventoryId = almacenRegistrado.Id,
                            ParameterType = "MonedaBase",
                            Name = "1", // Inicializa de forma predeterminada en Soles (Id = 1)
                            Description = $"Moneda base operativa del almacén: {almacenRegistrado.Alias}"
                        };
                        await apiService.CreateParameterAsync(parametroMoneda);
                    }
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MONEDA_ALMACEN_FAIL] {ex.Message}"); }

                await DisplayAlertAsync("Éxito", $"El ambiente '{nuevoInventario.Alias}' ha sido creado con éxito.", "OK");
                await CargarAmbientesDeTrabajoAsync();
                PkrAmbienteTrabajo.SelectedIndex = PkrAmbienteTrabajo.Items.Count - 1;
            }
            else
            {
                await DisplayAlertAsync("Error", "No se pudo registrar el nuevo inventario en el servidor.", "OK");
            }
        }

        // ==========================================
        // SELECCIÓN, RUTA MÁS CORTA Y ENRUTAMIENTO
        // ==========================================
        private void ConfigurarEventosDeToque()
        {
            for (int i = 0; i < _botonesOrbitales.Count; i++)
            {
                int indexCapturado = i;
                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += (s, e) => OnBotonOrbitalTapped(indexCapturado);
                _botonesOrbitales[i].GestureRecognizers.Add(tapGesture);
            }
        }
        private async void OnBotonOrbitalTapped(int index)
        {
            if (_estaNavegando) return;
            _estaNavegando = true;

            StopOrbitalAnimation();
            _inactivityTimer?.Stop();

            if (_botonesOrbitales[index].Children[0] is VerticalStackLayout vsl && vsl.Children[0] is Border borde)
            {
                borde.Stroke = Colors.Black;
                await Task.Delay(150);
                borde.Stroke = Color.FromArgb("#d3d3d3");
            }

            int pasoBase = (int)Math.Round(_anguloAcumuladoRad / (Math.PI / 2));
            int posFisica = ((index + pasoBase) % 4 + 4) % 4;

            int offsetPasos = 0;
            if (posFisica == 1) offsetPasos = -1;
            else if (posFisica == 2) offsetPasos = 2;
            else if (posFisica == 3) offsetPasos = 1;

            int pasoObjetivo = pasoBase + offsetPasos;
            double anguloInicial = _anguloAcumuladoRad;
            double anguloObjetivo = pasoObjetivo * (Math.PI / 2);
            double distancia = anguloObjetivo - anguloInicial;

            // 🎯 NUEVO: Hacemos que la animación sea "awaitable"
            if (Math.Abs(distancia) > 0.01)
            {
                var tcs = new TaskCompletionSource<bool>();
                int duracionVelozMs = Math.Abs(offsetPasos) == 2 ? 800 : 600;

                var animacionOrbital = new Animation(v =>
                {
                    double progresoConEasing = v < 0.5 ? 4 * Math.Pow(v, 3) : 1 - Math.Pow(-2 * v + 2, 3) / 2;
                    _anguloAcumuladoRad = anguloInicial + (distancia * progresoConEasing);
                    ActualizarPosicionesNodalesOnly(_anguloAcumuladoRad);
                });

                animacionOrbital.Commit(this, "GiroOrbital", length: (uint)duracionVelozMs, easing: Easing.Linear,
                    finished: (v, c) => tcs.SetResult(true)); // Avisa cuando termine

                await tcs.Task; // Espera estrictamente a que el botón llegue al Zenit (12 en punto)
            }

            _pasoActual = pasoObjetivo;
            _anguloAcumuladoRad = pasoObjetivo * (Math.PI / 2);
            ActualizarPosicionesNodalesOnly(_anguloAcumuladoRad);

            string rutaDestino = index switch
            {
                0 => "RegistrosPage",
                1 => "InventoryPage",
                2 => "ReportsPage",
                3 => "ConfiguracionPage",
                _ => ""
            };

            await Task.Delay(500); // Pausa solicitada antes de viajar

            if (!string.IsNullOrEmpty(rutaDestino))
            {
                await Shell.Current.GoToAsync(rutaDestino);
            }
        }

        // ==========================================
        // GATILLO DE INACTIVIDAD
        // ==========================================
        private void SetupInactivityTimer()
        {
            _inactivityTimer = Dispatcher.CreateTimer();
            _inactivityTimer.Interval = TimeSpan.FromSeconds(TiempoInactividadSegundos);
            _inactivityTimer.Tick += OnInactivityTimeout;
            ResetInactivityTimer();
        }
        private void OnPageInteraction(object sender, TappedEventArgs e)
        {
            if (!_estaNavegando) ResetInactivityTimer();
        }
        private void ResetInactivityTimer()
        {
            _inactivityTimer?.Stop();

            if (_estaAnimando)
            {
                if (_faseDeMovimientoActiva)
                    _solicitudDetenerDespuesDelPaso = true;
                else
                {
                    StopOrbitalAnimation();
                    _inactivityTimer?.Start();
                }
            }
            else
                _inactivityTimer?.Start();
        }
        private void OnInactivityTimeout(object? sender, EventArgs e)
        {
            _inactivityTimer?.Stop();
            if (!_estaAnimando && !_estaNavegando)
            {
                _solicitudDetenerDespuesDelPaso = false;
                StartOrbitalAnimation();
            }
        }

        // ==========================================
        // MOTOR DE TRANSICIÓN ELÁSTICA POR INACTIVIDAD
        // ==========================================
        private async void StartOrbitalAnimation()
        {
            _estaAnimando = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var stepStopwatch = new Stopwatch();

            try
            {
                while (!token.IsCancellationRequested && !_solicitudDetenerDespuesDelPaso)
                {
                    double anguloInicial = _pasoActual * (Math.PI / 2);
                    double anguloObjetivo = (_pasoActual + 1) * (Math.PI / 2);

                    _faseDeMovimientoActiva = true;
                    stepStopwatch.Restart();
                    double tiempoPasado = 0;

                    while (tiempoPasado < DuracionGiroMs)
                    {
                        // 🛡️ CORTACIRCUITOS: Detiene el bucle interno inmediatamente si se hace click
                        if (token.IsCancellationRequested) break;

                        tiempoPasado = stepStopwatch.Elapsed.TotalMilliseconds;
                        double progresoLineal = Math.Min(tiempoPasado / DuracionGiroMs, 1.0);
                        double progresoConEasing = 1 - Math.Pow(1 - progresoLineal, 3);

                        _anguloAcumuladoRad = anguloInicial + (anguloObjetivo - anguloInicial) * progresoConEasing;
                        ActualizarPosicionesNodalesOnly(_anguloAcumuladoRad);

                        // El token permite cancelar este milisegundo de pausa
                        await Task.Delay(16, token);
                    }

                    if (token.IsCancellationRequested) break;

                    _pasoActual++;
                    _anguloAcumuladoRad = _pasoActual * (Math.PI / 2);
                    ActualizarPosicionesNodalesOnly(_anguloAcumuladoRad);
                    _faseDeMovimientoActiva = false;

                    if (_solicitudDetenerDespuesDelPaso) break;
                    await Task.Delay(TiempoExposicionMs, token);
                }
            }
            catch (OperationCanceledException) { } // Captura silenciosa si cortas la animación
            finally
            {
                stepStopwatch.Stop();
                _estaAnimando = false;
                _faseDeMovimientoActiva = false;

                Dispatcher.Dispatch(() => {
                    if (!_estaNavegando)
                    {
                        _inactivityTimer?.Stop();
                        _inactivityTimer?.Start();
                    }
                });
            }
        }
        private void StopOrbitalAnimation()
        {
            _estaAnimando = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        // ==========================================
        // MOTOR DE GIRO INFINITO DEL RADAR
        // ==========================================
        private async Task AnimateAroEnergiaInfiniteSmooth(CancellationToken token)
        {
            var radarStopwatch = new Stopwatch();
            radarStopwatch.Start();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    double tiempoPasadoMs = radarStopwatch.Elapsed.TotalMilliseconds;
                    double progreso = (tiempoPasadoMs % VelocidadRadarLineaMs) / VelocidadRadarLineaMs;
                    AroEnergia.Rotation = progreso * 360;
                    await Task.Delay(16, token);
                }
            }
            catch (Exception) { }
            finally { radarStopwatch.Stop(); }
        }

        // ==========================================
        // RENDERIZADO GRÁFICO (CON OPACIDAD FÍSICA)
        // ==========================================
        private void ActualizarPosicionesNodalesOnly(double anguloRotacionAdicionalRad)
        {
            try
            {
                double anguloZenitRad = -Math.PI / 2;
                double umbralZenitRad = Math.PI / 4;

                for (int i = 0; i < _botonesOrbitales.Count; i++)
                {
                    double anguloBaseBotonRad = anguloZenitRad + (i * Math.PI / 2);
                    double anguloTotalRad = anguloBaseBotonRad + anguloRotacionAdicionalRad;

                    double posX = RadioOrbita * Math.Cos(anguloTotalRad);
                    double posY = RadioOrbita * Math.Sin(anguloTotalRad);

                    _botonesOrbitales[i].TranslationX = posX;
                    _botonesOrbitales[i].TranslationY = posY;

                    double anguloBotonCalculadoRad = Math.Atan2(posY, posX);
                    double diferenciaRad = Math.Abs(anguloBotonCalculadoRad - (-Math.PI / 2));
                    if (diferenciaRad > Math.PI) diferenciaRad = 2 * Math.PI - diferenciaRad;

                    // 🚀 SOLUCIÓN AL BUG DE DESAPARICIÓN: Asignación directa, sin usar ScaleToAsync
                    if (diferenciaRad < umbralZenitRad)
                    {
                        _botonesOrbitales[i].Scale = EscalaZoomZenit;
                        double opacidadCalculada = 1.0 - (diferenciaRad / umbralZenitRad);
                        _textosOrbitales[i].Opacity = Math.Clamp(Math.Pow(opacidadCalculada, 2), 0, 1);
                    }
                    else
                    {
                        _botonesOrbitales[i].Scale = 1.0;
                        _textosOrbitales[i].Opacity = 0;
                    }
                }
            }
            catch (Exception)
            {
                // Se ignora silenciosamente porque la app se está cerrando o el objeto ya se desechó
            }
        }

        public async Task ActualizarStockCircularAsync()
        {
            try
            {
                int idInventarioSeleccionado = UserSession.CurrentInventory?.Id ?? 1;
                int idEmpresa = UserSession.CurrentUser?.CompanyId ?? 1;

                var listaArticulos = await _apiService.GetArticlesAsync();

                int totalSKUsDisponibles = 0;

                if (listaArticulos != null)
                {
                    bool isOffline = Connectivity.Current.NetworkAccess != NetworkAccess.Internet;

                    totalSKUsDisponibles = listaArticulos.Count(a =>
                        a.InventoryId == idInventarioSeleccionado &&
                        a.Stock > 0 &&
                        (isOffline || a.CompanyId == idEmpresa));
                }

                // 4. Reflejamos el número real en la pantalla
                MainThread.BeginInvokeOnMainThread(() => {
                    LblTotalArticulos.Text = $"{totalSKUsDisponibles:N0} artículos";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al actualizar stock circular: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() => {
                    LblTotalArticulos.Text = "0 artículos";
                });
            }
        }

        private async void OnNavigateToCustomersClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("CustomersPage");
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo abrir Clientes: {ex.Message}", "OK");
            }
        }

        private async void OnNavigateToEmployeesClicked(object sender, EventArgs e)
        {
            bool tieneSmtp = !string.IsNullOrEmpty(UserSession.CurrentProfile?.SmtpEmail);
            if (!tieneSmtp)
            {
                await DisplayAlertAsync("Configuración Requerida", "Para gestionar el personal, primero debes configurar y probar el envío de correos (SMTP) en la pantalla de Configuración.", "Entendido");
                return;
            }

            try
            {
                await Shell.Current.GoToAsync("EmployeesPage");
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Error", $"No se pudo abrir Personal: {ex.Message}", "OK");
            }
        }

        private async void OnCompartirInventarioClicked(object sender, EventArgs e)
        {
            if (UserSession.CurrentInventory == null || UserSession.CurrentInventory.Id == 0)
            {
                await DisplayAlertAsync("Aviso", "Primero debes seleccionar o crear un almacén para poder compartirlo.", "Entendido");
                return;
            }

            await Shell.Current.GoToAsync("ShareInventoryPage");
        }

        private async Task SincronizarUnidadesMedidaAsync()
        {
            try
            {
                using var context = new LocalDbContext();
                var unidadesLocales = await context.MeasurementUnits.ToListAsync();

                UserSession.UnidadesMedidaCache.Clear();
                foreach (var u in unidadesLocales)
                {
                    UserSession.UnidadesMedidaCache[u.UnitName] = u.Abbreviation;
                }

                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    var unidadesNube = await _apiService.GetMeasurementUnitsAsync();
                    if (unidadesNube != null && unidadesNube.Count != 0)
                    {
                        UserSession.UnidadesMedidaCache.Clear();
                        foreach (var u in unidadesNube)
                        {
                            UserSession.UnidadesMedidaCache[u.UnitName] = u.Abbreviation;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al cargar unidades: {ex.Message}");
            }
        }

        private async Task CalcularAlertasGlobalesAsync()
        {
            try
            {
                // Leemos el dato si ya se calculó en el AnalyticsPage recientemente
                int alertasAlmacenadas = Preferences.Default.Get("AlertasGlobalesNegocio", -1);

                if (alertasAlmacenadas >= 0)
                {
                    LblBadgeReportes.Text = alertasAlmacenadas == 1 ? "1 Reporte" : $"{alertasAlmacenadas} Reportes";
                }
                else
                {
                    // Si nunca ha entrado al AnalyticsPage, hacemos un cálculo ultra rápido en segundo plano
                    int idInventario = UserSession.CurrentInventory?.Id ?? 1;
                    var listaArticulos = await _apiService.GetArticlesAsync();

                    if (listaArticulos != null)
                    {
                        var activos = listaArticulos.Where(a => a.InventoryId == idInventario && a.IsActive).ToList();
                        int restock = activos.Count(a => a.Stock <= 5);
                        int precios = activos.Count(a => a.AcquisitionPrice > 0 && a.SalePrice > 0 && ((a.SalePrice - a.AcquisitionPrice) / a.AcquisitionPrice) < 0.15m);

                        int totalFast = restock + precios;
                        LblBadgeReportes.Text = totalFast == 1 ? "1 Reporte" : $"{totalFast} Reportes";
                    }
                }

                // Efecto visual: Si hay alertas, lo ponemos rojo; si no, verde.
                if (LblBadgeReportes.Parent is Border bordePadre)
                {
                    bool hayAlertas = LblBadgeReportes.Text != "0 Reportes";
                    bordePadre.BackgroundColor = hayAlertas ? Color.FromArgb("#FCE8E6") : (Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E3A1E") : Color.FromArgb("#D4E6D1"));
                    LblBadgeReportes.TextColor = hayAlertas ? Color.FromArgb("#D32F2F") : (Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#A2D149") : Color.FromArgb("#4F7942"));
                }
            }
            catch { }
        }

        private async void ProcesarEscaneoFooterAsync(string codigoBarras)
        {
            try
            {
                if (UserSession.CurrentInventory == null)
                {
                    await DisplayAlertAsync("Aviso", "No hay un almacén o inventario activo seleccionado.", "OK");
                    return;
                }

                using var context = new LocalDbContext();

                // Buscamos si existe en la base de datos local
                var articuloExistente = await context.Articles.FirstOrDefaultAsync(a =>
                    a.Barcode == codigoBarras &&
                    a.InventoryId == UserSession.CurrentInventory.Id &&
                    a.IsActive);

                if (articuloExistente == null)
                {
                    // 🚀 1. MOSTRAR EL TEXTO DE AVISO ANTES DE REDIRIGIR
                    await DisplayAlertAsync(
                        "Artículo No Registrado",
                        "El artículo no existe, se te va a redirigir al formulario del artículo.",
                        "Entendido");

                    // 🚀 2. REDIRIGIR AL FORMULARIO PASANDO EL CÓDIGO POR URL
                    UserSession.CurrentArticleToEdit = null;
                    await Shell.Current.GoToAsync($"ArticleFormPage?scannedCode={codigoBarras}");
                }
                else
                {
                    // REGLA 1: Sí existe. Verificamos su tipo.
                    string tracking = articuloExistente.Tracking.ToString();
                    bool esSerializado = tracking.Equals("Serialized", StringComparison.OrdinalIgnoreCase) ||
                                         tracking.Equals("Serializado", StringComparison.OrdinalIgnoreCase);

                    if (esSerializado)
                    {
                        await DisplayAlertAsync("Acción Denegada", "Este artículo es Serializado. No se puede agregar stock rápido masivo porque cada unidad requiere un IMEI/N° de Serie único. Ve al módulo de Inventario.", "Entendido");
                    }
                    else
                    {
                        // Es Estándar o Granel: Mostramos el Overlay
                        _articuloRapidoEncontrado = articuloExistente;
                        LblOverlayQuickNombre.Text = articuloExistente.Name;
                        LblOverlayQuickStock.Text = $"Stock Actual: {articuloExistente.Stock}";
                        TxtQuickStockToAdd.Text = "";

                        OverlayStockRapido.IsVisible = true;
                        await OverlayStockRapido.FadeToAsync(1, 200);
                    }
                }
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "MainPage - ProcesarEscaneoFooterAsync");
                await DisplayAlertAsync("Error de Escaneo", "Hubo un problema al procesar el código.", "OK");
            }
        }

        private async void OnCerrarStockRapidoClicked(object sender, EventArgs e)
        {
            await OverlayStockRapido.FadeToAsync(0, 150);
            OverlayStockRapido.IsVisible = false;
            _articuloRapidoEncontrado = null;
        }

        private async void OnGuardarStockRapidoClicked(object sender, EventArgs e)
        {
            if (_articuloRapidoEncontrado == null) return;

            if (!decimal.TryParse(TxtQuickStockToAdd.Text, out decimal stockAAgregar) || stockAAgregar <= 0)
            {
                await DisplayAlertAsync("Validación", "Ingresa una cantidad válida mayor a 0.", "OK");
                return;
            }

            try
            {
                // Mostramos un Loading si tienes uno, si no, bloqueamos el botón
                if (sender is Button btn) btn.IsEnabled = false;

                _articuloRapidoEncontrado.Stock += stockAAgregar;
                _articuloRapidoEncontrado.IsSynced = false; // Marcamos para sincronización

                string nombreEmpleado = Preferences.Get("UserName", "Usuario Móvil");
                int empleadoId = Preferences.Get("UserId", 1);

                // Creamos el registro de movimiento
                var movimientoIngreso = new Movement
                {
                    ArticleId = _articuloRapidoEncontrado.Id,
                    EmployeeId = empleadoId,
                    ActionId = 1, // Ingreso
                    MovementDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Observation = $"Ingreso Rápido desde Footer (Por {nombreEmpleado})",
                    Amount = stockAAgregar,
                    SalePrice = 0,
                    PaymentMethod = "N/A",
                    Recipient = "Almacén Local",
                    IsSynced = false
                };

                // Guardado Local (Offline First)
                using var context = new LocalDbContext();
                context.Articles.Update(_articuloRapidoEncontrado);
                context.Movements.Add(movimientoIngreso);
                await context.SaveChangesAsync();

                // Intento a la nube (Opcional, si falla se queda en SQLite para sincronizar luego)
                try
                {
                    var apiService = new Services.ApiService();
                    bool exitoArt = await apiService.UpdateArticleAsync(_articuloRapidoEncontrado.Id, _articuloRapidoEncontrado);
                    bool exitoMov = await apiService.CreateMovementAsync(movimientoIngreso);

                    if (exitoArt && exitoMov)
                    {
                        _articuloRapidoEncontrado.IsSynced = true;
                        movimientoIngreso.IsSynced = true;
                        context.Articles.Update(_articuloRapidoEncontrado);
                        context.Movements.Update(movimientoIngreso);
                        await context.SaveChangesAsync();
                    }
                }
                catch
                {
                    // Se ignora silenciosamente, ya se guardó en local
                }

                await DisplayAlertAsync("Éxito", $"Se ingresaron {stockAAgregar} unidades a {_articuloRapidoEncontrado.Name} correctamente.", "OK");
                OnCerrarStockRapidoClicked(sender, e);
            }
            catch (Exception ex)
            {
                CrashLogger.LogHandledException(ex, "MainPage - OnGuardarStockRapidoClicked");
                await DisplayAlertAsync("Error", "No se pudo actualizar el stock.", "OK");
            }
            finally
            {
                if (sender is Button btnRestaurar) btnRestaurar.IsEnabled = true;
            }
        }
    }
}
