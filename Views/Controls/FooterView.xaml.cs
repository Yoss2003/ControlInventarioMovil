using ControlInventario.Models;
using ControlInventario.Shared.Models;
using ControlInventarioMovil.Services;
using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace ControlInventarioMovil.Views.Controls
{
    public partial class FooterView : ContentView
    {
        private bool _estaNavegando = false;
        private readonly ApiService _apiService;

        public FooterView()
        {
            InitializeComponent();
            _apiService = new ApiService();
        }

        private async void OnFooterButtonTapped(object sender, TappedEventArgs e)
        {
            if (_estaNavegando) return;

            var botonSeleccionado = sender as ContentView;
            if (botonSeleccionado == null) return;

            string parametro = (e.Parameter as string) ?? string.Empty;

            // --- EFECTO PULSO PREMIUM ---
            await botonSeleccionado.ScaleToAsync(0.9, 80, Easing.CubicIn);
            _ = botonSeleccionado.ScaleToAsync(1.0, 120, Easing.CubicOut);

            _estaNavegando = true;

            var rutaActual = Shell.Current.CurrentState.Location.OriginalString;
            var paginaActual = Shell.Current.CurrentPage;

            // --- ENRUTAMIENTO INTELIGENTE ---
            switch (parametro)
            {
                case "Agregar":
                    await NavegarFlujoAgregarSeguroAsync();
                    break;

                case "Vender":
                    await Shell.Current.GoToAsync("SalesPage");
                    break;

                case "Analisis":
                    if (!rutaActual.Contains("MainPage"))
                    {
                        await Shell.Current.GoToAsync("//MainPage");
                    }
                    break;

                case "Perfil":
                    if (!rutaActual.Contains("ProfilePage"))
                    {
                        await Shell.Current.GoToAsync("ProfilePage", false);
                    }
                    break;
            }

            _estaNavegando = false;
        }

        /// <summary>
        /// 🦾 FLUJO CENTRALIZADO: Pregunta, escanea y actualiza stock desde cualquier lugar de la app.
        /// </summary>
        private async Task ProcesarBotonAgregarGlobalAsync(Page? paginaActual)
        {
            if (paginaActual == null) return;

            string opcion = await paginaActual.DisplayActionSheetAsync("¿Qué acción deseas realizar?", "Cancelar", null, "📦 Registrar Producto Nuevo", "🔍 Escanear Código de Barras");

            if (opcion == "📦 Registrar Producto Nuevo")
            {
                UserSession.CurrentArticleToEdit = null;
                UserSession.PreloadedBarcode = null;
                await Shell.Current.GoToAsync("ArticleFormPage");
            }
            else if (opcion == "🔍 Escanear Código de Barras")
            {
                // 2. Disparar el lector simulado o la cámara
                string codigoEscaneado = await DispararEscanerCamaraGlobalAsync(paginaActual);
                if (string.IsNullOrWhiteSpace(codigoEscaneado)) return;

                // 3. Consultamos en caché centralizado de Somee si el Barcode ya existía
                var articuloExistente = await _apiService.GetArticleByBarcodeAsync(codigoEscaneado);

                if (articuloExistente != null)
                {
                    // 🪙 ¡SÍ EXISTE!: Solicitamos la cantidad exacta a sumar al inventario
                    string cantidadTxt = await paginaActual.DisplayPromptAsync(
                        "Producto Detectado",
                        $"El artículo '{articuloExistente.Name}' ya está en el sistema.\n\n¿Qué cantidad deseas añadir al stock actual? (Stock actual: {articuloExistente.Stock:0.##})",
                        "Aumentar Stock",
                        "Cancelar",
                        "1",
                        -1,
                        Keyboard.Numeric);

                    if (decimal.TryParse(cantidadTxt, out decimal cantidadAumentar) && cantidadAumentar > 0)
                    {
                        articuloExistente.Stock += cantidadAumentar;

                        // Mandamos el PUT de actualización
                        bool exito = await _apiService.UpdateArticleAsync(articuloExistente.Id, articuloExistente);
                        if (exito)
                        {
                            await paginaActual.DisplayAlertAsync("Éxito", $"Stock incrementado. Nuevo inventario total: {articuloExistente.Stock:0.##}", "OK");
                        }
                        else
                        {
                            await paginaActual.DisplayAlertAsync("Error", "No se pudo sincronizar el nuevo stock en la nube.", "OK");
                        }
                    }
                }
                else
                {
                    // 🆕 ¡NO EXISTE!: Preparamos los datos para una ficha técnica totalmente nueva
                    bool crearNuevo = await paginaActual.DisplayAlertAsync(
                        "Código No Registrado",
                        "Este código de barras no existe en el almacén.\n\n¿Deseas dar de alta su ficha técnica desde cero?",
                        "Sí, registrar",
                        "No");

                    if (crearNuevo)
                    {
                        UserSession.PreloadedBarcode = codigoEscaneado;
                        UserSession.CurrentArticleToEdit = null;
                        await Shell.Current.GoToAsync("ArticleFormPage");
                    }
                }
            }
        }

        /// <summary>
        /// Simulador temporal de lectura de código de barras.
        /// </summary>
        private async Task<string> DispararEscanerCamaraGlobalAsync(Page paginaActual)
        {
            string resultado = await paginaActual.DisplayPromptAsync("Escáner Activo", "Acerque el código de barras o digítelo manualmente:", "Escanear", "Cancelar", "Ej. 7501000001", -1, Keyboard.Numeric);
            return resultado?.Trim() ?? "";
        }

        private async Task NavegarFlujoAgregarSeguroAsync()
        {
            // Preguntamos al operador sobre la página actual
            string opcion = await Shell.Current.CurrentPage.DisplayActionSheetAsync("¿Qué acción deseas?", "Cancelar", null, "📦 Nuevo Producto", "🔍 Usar Cámara");

            if (opcion == "📦 Nuevo Producto")
            {
                UserSession.CurrentArticleToEdit = null;
                UserSession.PreloadedBarcode = null;
                await Shell.Current.GoToAsync("ArticleFormPage");
            }
            else if (opcion == "🔍 Usar Cámara")
            {
                // 🌟 NAVEGACIÓN REAL: Viajamos a la pantalla de la cámara
                await Shell.Current.GoToAsync("ScanBarcodePage", false);
            }
        }

        // 🌟 METODO REUTILIZABLE: Procesa la lógica contable del código escaneado desde cualquier página
        public async Task ProcesarLógicaAvanzadaEscanerAsync(string codigoEscaneado)
        {
            if (string.IsNullOrWhiteSpace(codigoEscaneado)) return;

            Page paginaActual = Shell.Current.CurrentPage; // Contexto para alertas
            await Task.Delay(200); // Pequeño delay de cortesía por estética de interfaz

            // 1. CONSULTA INTELIGENTE A LA NUBE: ¿Este código ya existe en SQL Somee?
            var articuloExistente = await _apiService.GetArticleByBarcodeAsync(codigoEscaneado);

            if (articuloExistente != null)
            {
                // 🪙 ¡SÍ EXISTE!: Aumento de stock de mercancía repetida
                string cantidadTxt = await paginaActual.DisplayPromptAsync(
                    "Producto Detectado",
                    $"El artículo '{articuloExistente.Name}' ya está registrado.\n\n¿Qué cantidad deseas agregar al stock actual? (Stock actual: {articuloExistente.Stock:0.##})",
                    "Aumentar Stock", "Cancelar", "1", -1, Keyboard.Numeric);

                if (decimal.TryParse(cantidadTxt, out decimal cantidadAumentar) && cantidadAumentar > 0)
                {
                    articuloExistente.Stock += cantidadAumentar;
                    bool exito = await _apiService.UpdateArticleAsync(articuloExistente.Id, articuloExistente);
                    if (exito) await paginaActual.DisplayAlertAsync("Éxito", $"Stock actualizado contablemente.", "OK");
                }
            }
            else
            {
                // 🆕 ¡NO EXISTE!: Alta de ficha técnica nueva
                bool crearNuevo = await paginaActual.DisplayAlertAsync(
                    "Código No Encontrado", "Este código de barras no existe en el almacén.\n\n¿Deseas dar de alta su ficha técnica?",
                    "Sí, registrar", "No");

                if (crearNuevo)
                {
                    UserSession.PreloadedBarcode = codigoEscaneado;
                    UserSession.CurrentArticleToEdit = null;
                    await Shell.Current.GoToAsync("ArticleFormPage");
                }
            }
        }
    }
}