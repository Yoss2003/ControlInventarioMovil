using ControlInventario.Models;
using ControlInventarioMovil.ViewModels;
using System.Diagnostics;

namespace ControlInventarioMovil.Views
{
    public partial class MainPage : ContentPage
    {
        private readonly MainPageViewModel _viewModel;

        // ==========================================
        // CONFIGURACIÓN DEL DIAL ORBITAL
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
        private const int MinutosParaCierreSesion = 15;
        private readonly List<Grid> _botonesOrbitales;
        private readonly List<Label> _textosOrbitales;

        public MainPage()
        {
            InitializeComponent();
            _viewModel = new MainPageViewModel();
            BindingContext = _viewModel;

            _botonesOrbitales = [OrbitaRegistros, OrbitaInventario, OrbitaReportes, OrbitaConfig];
            _textosOrbitales = [TxtRegistros, TxtInventario, TxtReportes, TxtConfig];
            ConfigurarEventosDeToque();

            _anguloAcumuladoRad = _pasoActual * (Math.PI / 2);
            ActualizarPosicionesNodalesOnly(_anguloAcumuladoRad);

            _cts = new CancellationTokenSource();
            SetupInactivityTimer();

            _inactivityTimer = Dispatcher.CreateTimer();
            _inactivityTimer.Interval = TimeSpan.FromMinutes(MinutosParaCierreSesion);
            _inactivityTimer.Tick += OnSessionTimeout;
            _inactivityTimer.Start();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            _estaNavegando = false;

            // ESCUDO DE SEGURIDAD 
            if (UserSession.CurrentUser == null)
            {
                Debug.WriteLine("[SEGURIDAD] Sesión vacía. Restableciendo LoginPage como raíz.");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (Application.Current?.Windows.Count > 0)
                        Application.Current.Windows[0].Page = new Views.LoginPage();
                });
                return;
            }

            // ANIMACIÓN DEL RADAR ENERGÉTICO
            if (_radarCts == null || _radarCts.IsCancellationRequested)
            {
                _radarCts = new CancellationTokenSource();
                _ = AnimateAroEnergiaInfiniteSmooth(_radarCts.Token);
            }

            // DELEGAMOS LA CARGA PESADA AL VIEWMODEL (Sin bloquear la UI)
            await _viewModel.InitializeUserDataAsync();

            _ = Task.Run(async () =>
            {
                await _viewModel.LoadWorkspacesAsync();
                await _viewModel.CalculateGlobalAlertsAsync();
                await _viewModel.SyncMeasurementUnitsAsync();
            });

            ResetInactivityTimer();
        }

        private async void OnSessionTimeout(object? sender, EventArgs e)
        {
            _inactivityTimer?.Stop();

            UserSession.CurrentUser = null;
            UserSession.CurrentProfile = null;
            UserSession.CurrentInventory = null;

            await DisplayAlertAsync("Sesión Expirada", "Tu sesión se ha cerrado por inactividad por motivos de seguridad.", "OK");

            if (Application.Current?.Windows.Count > 0)
                Application.Current.Windows[0].Page = new Views.LoginPage();
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

        // ==========================================
        // MOTOR MATEMÁTICO DEL DIAL ORBITAL
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
                    finished: (v, c) => tcs.SetResult(true));

                await tcs.Task;
            }

            _pasoActual = pasoObjetivo;
            _anguloAcumuladoRad = pasoObjetivo * (Math.PI / 2);
            ActualizarPosicionesNodalesOnly(_anguloAcumuladoRad);

            string rutaDestino = index switch
            {
                0 => "RegistrosPage",
                1 => "InventoryPage",
                2 => "ReportsPage",
                3 => "ConfigurationPage",
                _ => ""
            };

            await Task.Delay(500);

            if (!string.IsNullOrEmpty(rutaDestino))
                await Shell.Current.GoToAsync(rutaDestino);
        }

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
            _inactivityTimer?.Stop();
            _inactivityTimer?.Start();
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
                        if (token.IsCancellationRequested) break;

                        tiempoPasado = stepStopwatch.Elapsed.TotalMilliseconds;
                        double progresoLineal = Math.Min(tiempoPasado / DuracionGiroMs, 1.0);
                        double progresoConEasing = 1 - Math.Pow(1 - progresoLineal, 3);

                        _anguloAcumuladoRad = anguloInicial + (anguloObjetivo - anguloInicial) * progresoConEasing;
                        ActualizarPosicionesNodalesOnly(_anguloAcumuladoRad);

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
            catch (Exception) { }
        }

        public void CallProcessScanFromFooter(string codigoEscaneado)
        {
            _viewModel.ProcessScanCommand.Execute(codigoEscaneado);
        }
    }
}