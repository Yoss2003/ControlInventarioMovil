using ControlInventario.Models;
using System.Diagnostics;

namespace ControlInventarioMovil.Views
{
    public partial class MainPage : ContentPage
    {
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
        }


        protected override void OnAppearing()
        {
            base.OnAppearing();
            _estaNavegando = false;

            if (_radarCts == null || _radarCts.IsCancellationRequested)
            {
                _radarCts = new CancellationTokenSource();
                _ = AnimateAroEnergiaInfiniteSmooth(_radarCts.Token);
            }

            if (UserSession.CurrentUser != null)
            {
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
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _radarCts?.Cancel();
            _radarCts?.Dispose();
            _radarCts = null;
            StopOrbitalAnimation();
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

            // 1. EFECTO PULSE AL TOCAR
            if (_botonesOrbitales[index].Children[0] is VerticalStackLayout vsl && vsl.Children[0] is Border borde)
            {
                borde.Stroke = Colors.Black;
                await Task.Delay(150);
                borde.Stroke = Color.FromArgb("#d3d3d3");
            }

            // 2. MATEMÁTICA DE LA RUTA MÁS CORTA
            int pasoBase = (int)Math.Round(_anguloAcumuladoRad / (Math.PI / 2));

            // Obtenemos dónde está físicamente el botón ahora mismo (0=Arriba, 1=Der, 2=Abajo, 3=Izq)
            int posFisica = ((index + pasoBase) % 4 + 4) % 4;

            int offsetPasos = 0;
            if (posFisica == 1) offsetPasos = -1;      // Si está a las 3, retrocedemos 1 paso
            else if (posFisica == 2) offsetPasos = 2;  // Si está a las 6, avanzamos 2 pasos (180°)
            else if (posFisica == 3) offsetPasos = 1;  // Si está a las 9, avanzamos 1 paso

            int pasoObjetivo = pasoBase + offsetPasos;
            double anguloInicial = _anguloAcumuladoRad;
            double anguloObjetivo = pasoObjetivo * (Math.PI / 2);
            double distancia = anguloObjetivo - anguloInicial;

            // 3. ANIMACIÓN FLUIDA (CUBIC IN-OUT) HACIA EL ZENIT
            if (Math.Abs(distancia) > 0.01)
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                // Tiempo dinámico: Si viaja de lado a lado (180°) le damos 800ms, si es 90° le damos 600ms
                int duracionVelozMs = Math.Abs(offsetPasos) == 2 ? 800 : 600;
                var animacionOrbital = new Animation(v =>
                {
                    double progresoConEasing = v < 0.5
                        ? 4 * Math.Pow(v, 3)
                        : 1 - Math.Pow(-2 * v + 2, 3) / 2;

                    _anguloAcumuladoRad = anguloInicial + (distancia * progresoConEasing);
                    ActualizarPosicionesNodalesOnly(_anguloAcumuladoRad);
                });

                animacionOrbital.Commit(this, "GiroOrbital", length: (uint)duracionVelozMs, easing: Easing.Linear);
                stopwatch.Stop();
            }

            // Fijación geométrica exacta
            _pasoActual = pasoObjetivo;
            _anguloAcumuladoRad = pasoObjetivo * (Math.PI / 2);
            ActualizarPosicionesNodalesOnly(_anguloAcumuladoRad);

            // 4. REDIRECCIÓN A LA VISTA CORRESPONDIENTE
            string rutaDestino = index switch
            {
                0 => "RegistrosPage",
                1 => "InventoryPage",
                2 => "ReportesPage",
                3 => "ConfiguracionPage",
                _ => ""
            };

            // TODO: Comenta el DisplayAlertAsync y descomenta la línea de GoToAsync
            await Task.Delay(500);
            if (!string.IsNullOrEmpty(rutaDestino))
            {
                await Shell.Current.GoToAsync(rutaDestino);
            }
            // await Shell.Current.GoToAsync(rutaDestino); 

            _estaNavegando = false;
            _inactivityTimer?.Start();
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
                        tiempoPasado = stepStopwatch.Elapsed.TotalMilliseconds;
                        double progresoLineal = Math.Min(tiempoPasado / DuracionGiroMs, 1.0);
                        double progresoConEasing = 1 - Math.Pow(1 - progresoLineal, 3);

                        _anguloAcumuladoRad = anguloInicial + (anguloObjetivo - anguloInicial) * progresoConEasing;
                        ActualizarPosicionesNodalesOnly(_anguloAcumuladoRad);

                        await Task.Delay(16);
                    }

                    _pasoActual++;
                    _anguloAcumuladoRad = _pasoActual * (Math.PI / 2);
                    ActualizarPosicionesNodalesOnly(_anguloAcumuladoRad);
                    _faseDeMovimientoActiva = false;

                    if (_solicitudDetenerDespuesDelPaso || token.IsCancellationRequested) break;
                    await Task.Delay(TiempoExposicionMs, token);
                }
            }
            catch (OperationCanceledException) { }
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
                        _ = _botonesOrbitales[i].ScaleToAsync(EscalaZoomZenit, 100, Easing.Linear);
                    }

                    double opacidadCalculada = 1.0 - (diferenciaRad / umbralZenitRad);
                    _textosOrbitales[i].Opacity = Math.Clamp(Math.Pow(opacidadCalculada, 2), 0, 1);
                }
                else
                {
                    if (_botonesOrbitales[i].Scale != 1.0)
                    {
                        _ = _botonesOrbitales[i].ScaleToAsync(1.0, 100, Easing.Linear);
                    }
                    _textosOrbitales[i].Opacity = 0;
                }
            }
        }
    }
}
