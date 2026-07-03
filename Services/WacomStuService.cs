using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using FirmasApp.Services.Native;
using FirmasApp.Models;

namespace FirmasApp.Services;

/// <summary>
/// Event args para datos de stylus
/// </summary>
public class WacomPenDataEventArgs : EventArgs
{
    public PenData PenData { get; }

    public WacomPenDataEventArgs(PenData penData)
    {
        PenData = penData;
    }
}

/// <summary>
/// Event args para cambios de dispositivo
/// </summary>
public class WacomDeviceEventArgs : EventArgs
{
    public DeviceEventType EventType { get; }

    public WacomDeviceEventArgs(int eventType)
    {
        EventType = (DeviceEventType)eventType;
    }
}

/// <summary>
/// Servicio para captura de firmas usando tabletas Wacom STU
/// Implementación real con SDK nativo
/// </summary>
public class WacomStuService : IDisposable
{
    private readonly WacomStuSettings _settings;
    private readonly WacomStuCallbackManager _callbackManager;
    private bool _isInitialized;
    private bool _isConnected;
    private bool _disposed;

    // Capture state
    private StrokeCollection? _currentCapturedStrokes;
    private StylusPointCollection? _currentStroke;
    private readonly object _captureLock = new();
    private CancellationTokenSource? _captureCts;

    public bool IsConnected => _isConnected;
    public bool IsInitialized => _isInitialized;

    // Events
    public event EventHandler<WacomPenDataEventArgs>? PenDataReceived;
    public event EventHandler<WacomDeviceEventArgs>? DeviceChanged;

    public WacomStuService(WacomStuSettings settings)
    {
        _settings = settings;
        _callbackManager = new WacomStuCallbackManager();
        AppLog.Info("Wacom", "Servicio creado");

        // Verificar si la DLL está disponible (sin cargarla aún)
        var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _settings.DllPath);
        if (File.Exists(dllPath))
        {
            AppLog.Info("Wacom", $"DLL detectada: {dllPath}");
        }
        else
        {
            AppLog.Warn("Wacom", $"DLL no encontrada: {dllPath} - Se usará simulación");
        }
    }

    /// <summary>
    /// Inicializa la conexión con la tablet Wacom
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        try
        {
            if (_isInitialized)
            {
                AppLog.Info("Wacom", "Ya inicializado");
                return _isConnected;
            }

            AppLog.Info("Wacom", "Iniciando inicialización del SDK STU-430...");

            // Check if simulation mode is enabled
            if (_settings.UseSimulation)
            {
                AppLog.Info("Wacom", "Modo simulación activado");
                _isInitialized = true;
                _isConnected = SimularConexionDispositivo();
                return _isConnected;
            }

            // 1. Cargar DLL nativa
            if (!LoadNativeLibrary())
            {
                AppLog.Warn("Wacom", "No se pudo cargar wgssSTU.dll, usando simulación");
                _isInitialized = true;
                _isConnected = SimularConexionDispositivo();
                return _isConnected;
            }

            // 2. Intentar conectar a dispositivo
            await Task.Run(() =>
            {
                var connectResult = WacomStuNative.stuConnect();
                if (connectResult < 0)
                {
                    AppLog.Warn("Wacom", $"Error conectando: {connectResult}, usando simulación");
                    return;
                }

                // 3. Verificar conexión
                var isConnected = WacomStuNative.stuIsConnected();
                if (!isConnected)
                {
                    AppLog.Warn("Wacom", "Dispositivo no responde, usando simulación");
                    return;
                }

                // 4. Obtener información del dispositivo
                var deviceInfo = new DeviceInfo();
                WacomStuNative.stuGetDeviceInfo(ref deviceInfo);

                AppLog.Info("Wacom", $"Conectado a STU-430 ({deviceInfo.Width}x{deviceInfo.Height})");
            });

            // 5. Registrar callbacks si está conectado
            if (_isConnected)
            {
                try
                {
                    _callbackManager.RegisterCallbacks(this);
                }
                catch (Exception ex)
                {
                    AppLog.Warn("Wacom", $"No se pudieron registrar callbacks: {ex.Message}");
                }
            }

            _isInitialized = true;

            return _isConnected;
        }
        catch (Exception ex)
        {
            AppLog.Error("Wacom", $"Error inicializando: {ex.Message}", ex);
            // Fallback to simulation
            _isInitialized = true;
            _isConnected = SimularConexionDispositivo();
            return _isConnected;
        }
    }

    /// <summary>
    /// Comienza la captura de firma
    /// </summary>
    public async Task<StrokeCollection?> CapturarFirmaAsync(int timeoutSegundos = 30)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Wacom STU no está inicializado. Llame a InitializeAsync() primero.");
        }

        try
        {
            AppLog.Info("Wacom", "Iniciando captura de firma...");

            // Use simulation if not connected to real device
            if (!_isConnected || _settings.UseSimulation)
            {
                return await SimularCapturaFirmaAsync(timeoutSegundos);
            }

            // Real capture with device
            return await CapturarFirmaRealAsync(timeoutSegundos);
        }
        catch (Exception ex)
        {
            AppLog.Error("Wacom", $"Error capturando firma: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Captura de firma real con dispositivo
    /// </summary>
    private async Task<StrokeCollection?> CapturarFirmaRealAsync(int timeoutSegundos)
    {
        lock (_captureLock)
        {
            if (_captureCts != null)
            {
                throw new InvalidOperationException("Ya hay una captura en progreso");
            }

            _currentCapturedStrokes = new StrokeCollection();
            _currentStroke = new StylusPointCollection();
            _captureCts = new CancellationTokenSource();
        }

        AppLog.Info("Wacom", "Iniciando captura de firma en STU-430...");

        try
        {
            // Limpiar pantalla de la tablet
            var clearResult = WacomStuNative.stuClearScreen();
            if (clearResult < 0)
            {
                AppLog.Warn("Wacom", $"No se pudo limpiar pantalla: {clearResult}");
            }

            // Iniciar captura nativa
            var startResult = WacomStuNative.stuStartCapture();
            if (startResult < 0)
            {
                throw new InvalidOperationException($"Error iniciando captura: {startResult}");
            }

            try
            {
                // Esperar timeout o que el usuario complete la captura
                await Task.Delay(timeoutSegundos * 1000, _captureCts.Token);

                AppLog.Warn("Wacom", "Timeout de captura alcanzado");
            }
            catch (OperationCanceledException)
            {
                AppLog.Info("Wacom", "Captura completada");
            }
            finally
            {
                WacomStuNative.stuStopCapture();

                // Finalizar último stroke
                lock (_captureLock)
                {
                    if (_currentStroke != null && _currentStroke.Count > 0)
                    {
                        _currentCapturedStrokes!.Add(new Stroke(_currentStroke));
                    }
                }
            }

            return _currentCapturedStrokes;
        }
        finally
        {
            lock (_captureLock)
            {
                _captureCts?.Dispose();
                _captureCts = null;
                _currentStroke = null;
            }
        }
    }

    /// <summary>
    /// Handler interno para datos de stylus
    /// </summary>
    internal void OnPenDataReceived(PenData data)
    {
        try
        {
            lock (_captureLock)
            {
                if (_currentCapturedStrokes == null) return;

                // Verificar umbral de presión
                if (data.Pressure < _settings.MinPressureThreshold)
                {
                    // Stylus levantado - finalizar stroke actual
                    if (_currentStroke != null && _currentStroke.Count > 0)
                    {
                        _currentCapturedStrokes.Add(new Stroke(_currentStroke));
                        _currentStroke = new StylusPointCollection();
                    }
                    return;
                }

                // Agregar punto al stroke actual
                _currentStroke ??= new StylusPointCollection();

                var point = new StylusPoint(
                    data.X,
                    data.Y,
                    data.Pressure / 255f  // Normalizar presión a 0-1
                );

                _currentStroke.Add(point);

                // Disparar evento para UI
                PenDataReceived?.Invoke(this, new WacomPenDataEventArgs(data));
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Wacom", $"Error en OnPenDataReceived: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Handler interno para cambios de dispositivo
    /// </summary>
    internal void OnDeviceChanged(int eventType)
    {
        try
        {
            AppLog.Info("Wacom", $"Evento de dispositivo: {eventType}");

            if (eventType == (int)DeviceEventType.Disconnected)
            {
                _isConnected = false;
            }
            else if (eventType == (int)DeviceEventType.Connected)
            {
                _isConnected = true;
            }

            DeviceChanged?.Invoke(this, new WacomDeviceEventArgs(eventType));
        }
        catch (Exception ex)
        {
            AppLog.Error("Wacom", $"Error en OnDeviceChanged: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Carga la librería nativa
    /// </summary>
    private bool LoadNativeLibrary()
    {
        try
        {
            var dllPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                _settings.DllPath
            );

            if (!File.Exists(dllPath))
            {
                AppLog.Error("Wacom", $"DLL no encontrada: {dllPath}");
                return false;
            }

            // Precargar DLL para verificar dependencias
            var handle = LoadLibrary(dllPath);
            if (handle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                AppLog.Error("Wacom", $"Error cargando DLL: {error}");
                return false;
            }

            FreeLibrary(handle);
            AppLog.Info("Wacom", $"DLL cargada: {dllPath}");
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Wacom", $"Error cargando librería: {ex.Message}", ex);
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    /// <summary>
    /// Limpia la tablet para nueva captura
    /// </summary>
    public void LimpiarTablet()
    {
        if (!_isConnected || _settings.UseSimulation)
        {
            AppLog.Info("Wacom", "Limpiando tablet (simulación)");
            return;
        }

        try
        {
            var result = WacomStuNative.stuClearScreen();
            if (result < 0)
            {
                AppLog.Warn("Wacom", $"Error limpiando tablet: {result}");
            }
            else
            {
                AppLog.Info("Wacom", "Tablet limpiada");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Wacom", $"Error limpiando tablet: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Desconecta la tablet
    /// </summary>
    public void Disconnect()
    {
        if (!_isConnected)
        {
            return;
        }

        try
        {
            if (!_settings.UseSimulation)
            {
                WacomStuNative.stuDisconnect();
            }
            _isConnected = false;
            AppLog.Info("Wacom", "Tablet desconectada");
        }
        catch (Exception ex)
        {
            AppLog.Error("Wacom", $"Error desconectando: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _callbackManager?.Dispose();
        Disconnect();
    }

    // MÉTODOS DE SIMULACIÓN (para desarrollo sin dispositivo físico)

    private bool SimularConexionDispositivo()
    {
        // Simular que siempre hay un dispositivo conectado para desarrollo
        AppLog.Info("Wacom", "Usando modo simulación");
        return true;
    }

    private async Task<StrokeCollection?> SimularCapturaFirmaAsync(int timeoutSegundos)
    {
        await Task.Delay(100); // Pequeño delay para realismo

        var strokes = new StrokeCollection();
        Random rnd = new Random();

        // Crear una firma simulada con 3 trazos
        // Trazo 1: Nombre
        var stylusPoints1 = new StylusPointCollection();
        for (int i = 0; i < 10; i++)
        {
            stylusPoints1.Add(new StylusPoint(50 + i * 15, 80 + rnd.Next(-5, 5)));
        }
        strokes.Add(new Stroke(stylusPoints1));

        // Trazo 2: Línea
        var stylusPoints2 = new StylusPointCollection();
        for (int i = 0; i < 8; i++)
        {
            stylusPoints2.Add(new StylusPoint(50 + i * 20, 100 + rnd.Next(-3, 3)));
        }
        strokes.Add(new Stroke(stylusPoints2));

        // Trazo 3: Rubrica
        var stylusPoints3 = new StylusPointCollection();
        for (int i = 0; i < 15; i++)
        {
            var x = 180 + i * 8;
            var y = 80 + Math.Sin(i * 0.5) * 20 + rnd.Next(-5, 5);
            stylusPoints3.Add(new StylusPoint(x, y));
        }
        strokes.Add(new Stroke(stylusPoints3));

        AppLog.Info("Wacom", $"Simulación completada: {strokes.Count} trazos");
        return strokes;
    }

    /// <summary>
    /// Convierte una colección de trazos a imagen PNG
    /// </summary>
    public byte[] ConvertirTrazosAImagen(StrokeCollection strokes, int ancho = 400, int alto = 200)
    {
        try
        {
            // Crear un DrawingVisual para renderizar los trazos
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                // Fondo transparente
                context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ancho, alto));

                // Configurar estilo de trazo
                var stroke = new Pen(Brushes.Black, 2.5)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    DashCap = PenLineCap.Round
                };

                // Dibujar cada trazo
                foreach (Stroke strokeCollection in strokes)
                {
                    var points = strokeCollection.StylusPoints
                        .Select(p => new Point(p.X, p.Y))
                        .ToList();

                    if (points.Count >= 2)
                    {
                        var geometry = new StreamGeometry();
                        using (var geometryContext = geometry.Open())
                        {
                            geometryContext.BeginFigure(points[0], false, false);
                            for (int i = 1; i < points.Count; i++)
                            {
                                geometryContext.LineTo(points[i], true, true);
                            }
                            geometryContext.Close();
                        }
                        context.DrawGeometry(null, stroke, geometry);
                    }
                }
            }

            // Renderizar a bitmap
            var bitmap = new RenderTargetBitmap(ancho, alto, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);

            // Convertir a PNG
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Wacom", $"Error convirtiendo trazos a imagen: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Convierte trazos a dataURL (base64)
    /// </summary>
    public string ConvertirTrazosADataUrl(StrokeCollection strokes, int ancho = 400, int alto = 200)
    {
        var bytes = ConvertirTrazosAImagen(strokes, ancho, alto);
        var base64 = Convert.ToBase64String(bytes);
        return $"data:image/png;base64,{base64}";
    }

    /// <summary>
    /// Convierte dataURL a StrokeCollection
    /// </summary>
    public StrokeCollection? ConvertirDataUrlATrazos(string dataUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dataUrl))
            {
                return null;
            }

            // Extraer base64 del dataURL
            var match = System.Text.RegularExpressions.Regex.Match(dataUrl, @"^data:image/[^;]+;base64,(.+)$");
            if (!match.Success)
            {
                return null;
            }

            var base64 = match.Groups[1].Value;
            var bytes = Convert.FromBase64String(base64);

            // Convertir bytes a imagen
            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();

            // TODO: Implementar conversión de imagen a trazos (OCR de trazos)
            // Esto es complejo y requiere algoritmos avanzados
            AppLog.Info("Wacom", "Conversión de imagen a trazos no implementada aún");
            return null;
        }
        catch (Exception ex)
        {
            AppLog.Error("Wacom", $"Error convirtiendo dataURL a trazos: {ex.Message}", ex);
            return null;
        }
    }
}