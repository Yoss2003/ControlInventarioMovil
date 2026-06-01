using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using ControlInventarioMovil.Views.Controls;
using ControlInventarioMovil.Helpers;
using System.Diagnostics;

namespace ControlInventarioMovil.Views
{
    [QueryProperty(nameof(ScannedCodeResult), "scannedCode")]
    public partial class MainPage : ContentPage
    {
        private bool _isAnimating = false;
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

        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _radarCts;
        private IDispatcherTimer? _inactivityTimer;
        private List<Grid> _botonesOrbitales;
        private List<Label> _textosOrbitales;
        private List<Inventory> _almacenesDisponibles = new();

        public MainPage()
        {
            InitializeComponent();
            _botonesOrbitales = new List<Grid> { OrbitaRegistros, OrbitaInventario, OrbitaReportes, OrbitaConfig };
            _textosOrbitales = new List<Label> { TxtRegistros, TxtInventario, TxtReportes, TxtConfig };
            ConfigurarEventosDeToque();

            _anguloAcumuladoRad = _pasoActual * (Math.PI / 2);
            ActualizarPosicionesNodalesOnly(_anguloAcumuladoRad);

            _cts = new CancellationTokenSource();
            SetupInactivityTimer();

            _apiService = new ApiService();
        }

        public string ScannedCodeResult
        {
            set => Dispatcher.Dispatch(async () => await EntregarCódigoAlFooterAsync(value));
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
            string firstName = UserSession.CurrentUser.FirstName?.Trim() ?? "";
            string lastName = UserSession.CurrentUser.LastName?.Trim() ?? "";
            string userRole = UserSession.CurrentUser.Role?.Name?.Trim() ?? "Usuario";

            string apellido = "";
            string nombre = "";

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

            // 6. ENCENDIDO DE LA ANIMACIÓN DE FONDO
            _isAnimating = true;
            _ = AnimarFondo();

            // Reanuda el contador para que la animación empiece de nuevo
            ResetInactivityTimer();
        }

        private async Task AnimarFondo()
        {
            // El bucle verificará constantemente la bandera
            while (_isAnimating)
            {
                try
                {
                    // EJEMPLO: Efecto de "respiración" o "flotación" sutil en tu elemento de fondo.
                    // ⚠️ NOTA: Cambia "imgFondo" por el nombre exacto de la imagen o layout que quieras animar en tu XAML.
                    // Si no tienes una imagen de fondo específica a animar, puedes omitir estas líneas.

                    /* DESCOMENTAR SI TIENES UN ELEMENTO LLAMADO imgFondo:
                    await imgFondo.ScaleTo(1.02, 2000, Easing.SinInOut);
                    await imgFondo.ScaleTo(1.00, 2000, Easing.SinInOut);
                    */

                    // Pausa vital de rendimiento (evita que el bucle while consuma el 100% del procesador)
                    await Task.Delay(100);
                }
                catch (Exception)
                {
                    // Si el usuario cambia de pantalla a mitad de la animación, salimos del bucle sin crashear
                    break;
                }
            }
        }

        private async Task EntregarCódigoAlFooterAsync(string codigo)
        {
            // 'MiFooterComponente' debe ser el x:Name de tu FooterView en el XAML de MainPage
            if (!string.IsNullOrWhiteSpace(codigo))
            {
                await FooterView.ProcesarLógicaAvanzadaEscanerAsync(codigo);
            }
        }
        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            _isAnimating = false;

            // 1. Apagar el Radar
            if (_radarCts != null && !_radarCts.IsCancellationRequested)
            {
                _radarCts.Cancel();
                _radarCts.Dispose();
                _radarCts = null;
            }

            // 2. Apagar el motor del Dial Orbital (Esto detiene el giro)
            StopOrbitalAnimation();
            this.AbortAnimation("GiroOrbital");

            // 3. Limpiar página base
            this.CancelAnimations();

            // 4. Limpiar botones
            foreach (var boton in _botonesOrbitales)
            {
                boton.CancelAnimations();
            }
        }

        // Descarga de almacenes desde Somee al Picker de MAUI
        private async Task CargarAmbientesDeTrabajoAsync()
        {
            try
            {
                PkrAmbienteTrabajo.Title = "Cargando...";

                var apiService = new ControlInventarioMovil.Services.ApiService();
                var lista = await apiService.GetInventoriesAsync();

                if (lista != null)
                {
                    PkrAmbienteTrabajo.SelectedIndexChanged -= OnAmbienteTrabajoChanged; // Apagar evento temporalmente
                    PkrAmbienteTrabajo.Items.Clear();

                    // 👇 FILTRO SENIOR: Excluimos el inventario global de sistema (Id = 0) de la lista de memoria
                    _almacenesDisponibles = lista.Where(i => i.Id != 0).ToList();

                    // Llenamos el Picker mostrando el Alias limpio
                    _almacenesDisponibles.ForEach(inv =>
                        PkrAmbienteTrabajo.Items.Add(string.IsNullOrWhiteSpace(inv.Alias) ? inv.InventoryName : inv.Alias));

                    // Si ya hay un almacén activo en la sesión y es válido, lo pre-seleccionamos
                    if (UserSession.CurrentInventory != null && UserSession.CurrentInventory.Id != 0)
                    {
                        int index = _almacenesDisponibles.FindIndex(i => i.Id == UserSession.CurrentInventory.Id);
                        if (index >= 0) PkrAmbienteTrabajo.SelectedIndex = index;
                    }
                    else if (_almacenesDisponibles.Any())
                    {
                        // Si la sesión tenía el ID 0 o estaba en null, forzamos el primer almacén real de la sucursal
                        PkrAmbienteTrabajo.SelectedIndex = 0;
                        UserSession.CurrentInventory = _almacenesDisponibles.First();
                    }

                    PkrAmbienteTrabajo.SelectedIndexChanged += OnAmbienteTrabajoChanged; // Re-encender evento
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WORKSPACE_ERROR] {ex.Message}");
            }
            finally
            {
                PkrAmbienteTrabajo.Title = "";
            }
        }

        // Evento que se dispara al cambiar de Almacén/Bodega en el Picker
        private async void OnAmbienteTrabajoChanged(object? sender, EventArgs? e)
        {
            if (PkrAmbienteTrabajo.SelectedIndex == -1) return;

            // Cambiamos el contexto del entorno activo globalmente en la sesión
            UserSession.CurrentInventory = _almacenesDisponibles[PkrAmbienteTrabajo.SelectedIndex];

            string nombreVisual = string.IsNullOrWhiteSpace(UserSession.CurrentInventory.Alias)
                ? UserSession.CurrentInventory.InventoryName
                : UserSession.CurrentInventory.Alias;

            // 👇 Actualizado a .NET 10.0 utilizando la palabra Async al final
            //await DisplayAlertAsync("Entorno Activo",
            //    $"Cambiado a: {nombreVisual}\n\n(Todos los artículos ingresados ahora se guardarán con el ID real de este almacén: {UserSession.CurrentInventory.Id})",
            //    "OK");

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
                2 => "ReportesPage",
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
                    await Task.Delay(16);
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

                    if (diferenciaRad < umbralZenitRad)
                    {
                        if (_botonesOrbitales[i].Scale != EscalaZoomZenit)
                        {
                            if (this.Window != null)
                                _ = _botonesOrbitales[i].ScaleToAsync(EscalaZoomZenit, 100, Easing.Linear);
                            else
                                _botonesOrbitales[i].Scale = EscalaZoomZenit;
                        }

                        double opacidadCalculada = 1.0 - (diferenciaRad / umbralZenitRad);
                        _textosOrbitales[i].Opacity = Math.Clamp(Math.Pow(opacidadCalculada, 2), 0, 1);
                    }
                    else
                    {
                        if (_botonesOrbitales[i].Scale != 1.0)
                        {
                            if (this.Window != null)
                                _ = _botonesOrbitales[i].ScaleToAsync(1.0, 100, Easing.Linear);
                            else
                                _botonesOrbitales[i].Scale = 1.0;
                        }
                        _textosOrbitales[i].Opacity = 0;
                    }
                }
            }
            catch (Exception)
            {
                // Se ignora silenciosamente porque la app se está cerrando o el objeto ya se desechó
            }
        }

        private async void OnStockOkCardClicked(object sender, EventArgs e)
        {
            // Redirige al operador a un listado detallado filtrado por stock activo
            await Shell.Current.GoToAsync("ActiveStockReportPage");
        }

        private async void OnFooterAddClicked(object sender, EventArgs e)
        {
            // 1. Preguntar la acción al operador
            string accion = await DisplayActionSheetAsync("¿Qué deseas registrar?", "Cancelar", null, "📦 Agregar Producto Nuevo", "🔍 Escanear Código de Barras");

            if (accion == "📦 Agregar Producto Nuevo")
            {
                UserSession.CurrentArticleToEdit = null; // Modo Alta Nueva
                await Shell.Current.GoToAsync("ArticleFormPage");
            }
            else if (accion == "🔍 Escanear Código de Barras")
            {
                // 2. Disparar cámara de escaneo (Ej: usando ZXing o CommunityToolkit)
                string codigoEscaneado = await DispararEscanerCamaraAsync();

                if (string.IsNullOrWhiteSpace(codigoEscaneado)) return;

                // 3. Consultar a la API si el Barcode ya existía en este almacén
                var articuloExistente = await _apiService.GetArticleByBarcodeAsync(codigoEscaneado);

                if (articuloExistente != null)
                {
                    // 4. ¡Existe! Pedimos la cantidad a sumar usando un Prompt nativo
                    string cantidadTxt = await DisplayPromptAsync("Producto Detectado", $"El artículo '{articuloExistente.Name}' ya existe.\n\n¿Cuántas unidades vas a ingresar al stock?", "Aumentar Stock", "Cancelar", "1", -1, Keyboard.Numeric);

                    if (decimal.TryParse(cantidadTxt, out decimal cantidadASumar) && cantidadASumar > 0)
                    {
                        articuloExistente.Stock += cantidadASumar;
                        bool exito = await _apiService.UpdateArticleAsync(articuloExistente.Id, articuloExistente);

                        if (exito) await DisplayAlertAsync("Stock Actualizado", $"Se añadieron {cantidadASumar} unidades. Stock total actual: {articuloExistente.Stock}", "OK");
                    }
                }
                else
                {
                    // 5. NO EXISTE: Mandamos al formulario mandándole el código pre-cargado
                    bool crear = await DisplayAlertAsync("Código Nuevo", "El código escaneado no está registrado en el inventario. ¿Deseas crear su ficha técnica desde cero?", "Sí, registrar", "No");
                    if (crear)
                    {
                        UserSession.PreloadedBarcode = codigoEscaneado; // Guardamos en sesión para auto-rellenar
                        await Shell.Current.GoToAsync("ArticleFormPage");
                    }
                }
            }
        }
        
        private async Task<string> DispararEscanerCamaraAsync()
        {
            // Muestra un input box en la pantalla para simular la lectura de la pistola de barras
            string resultado = await DisplayPromptAsync("Escáner Simulado", "Digita o simula la lectura de un código de barras:", "Escanear", "Cancelar", "Ej. 7501000001", -1, Keyboard.Numeric);
            return resultado?.Trim() ?? "";
        }

        public async Task ActualizarStockCircularAsync()
        {
            try
            {
                // 1. Tomamos el ID del inventario que esté seleccionado en la sesión en este milisegundo
                int idInventarioSeleccionado = UserSession.CurrentInventory?.Id ?? 1;

                // 2. Consultamos al servidor de Somee la suma del stock de ese almacén específico
                int totalReal = await _apiService.GetArticleCountByInventoryAsync(idInventarioSeleccionado);

                // 3. Pintamos el número en tu Label del círculo verde
                MainThread.BeginInvokeOnMainThread(() => {
                    LblTotalArticulos.Text = $"{totalReal:N0} artículos";
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al actualizar stock circular: {ex.Message}");
                LblTotalArticulos.Text = "0 artículos";
            }
        }

    }
}
