using FirmasApp.Models;
using FirmasApp.Services;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace FirmasApp.Services;

/// <summary>
/// Servicio coordinador de sincronización entre BD local y servidor
/// Detecta conexión, coordina la sincronización y notifica al usuario
/// </summary>
public class SyncCoordinatorService
{
    private readonly QueueService _queueService;
    private readonly LocalDbService _dbService;
    private readonly HttpClient _httpClient;
    private readonly KeycloakAuthService _keycloakAuth;
    private readonly GedsysApiSettings _apiSettings;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly CancellationTokenSource _shutdownCts;
    private bool _disposed;
    private Task? _sincronizacionTask;

    // Eventos para notificar cambios de sincronización
    public event EventHandler<SyncNotificacionEventArgs>? SyncNotificacion;

    public SyncCoordinatorService(
        QueueService queueService,
        LocalDbService dbService,
        GedsysApiSettings apiSettings,
        KeycloakAuthService keycloakAuth)
    {
        _queueService = queueService;
        _dbService = dbService;
        _apiSettings = apiSettings;
        _keycloakAuth = keycloakAuth;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(apiSettings.BaseUrl),
            Timeout = TimeSpan.FromSeconds(apiSettings.TimeoutSeconds)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        _shutdownCts = new CancellationTokenSource();
    }

    /// <summary>
    /// Inicia el servicio de sincronización automática
    /// </summary>
    public void IniciarSincronizacion()
    {
        if (_sincronizacionTask != null && !_sincronizacionTask.IsCompleted)
        {
            return; // Ya está corriendo
        }

        _sincronizacionTask = Task.Run(async () => await SincronizacionLoopAsync(_shutdownCts.Token));
        _queueService.IniciarProcesamiento();

        AppLog.Info("SyncCoordinator", "Sincronización automática iniciada");
    }

    /// <summary>
    /// Loop principal de sincronización automática
    /// </summary>
    private async Task SincronizacionLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Esperar 30 segundos entre ciclos
                await Task.Delay(TimeSpan.FromSeconds(30), ct);

                if (ct.IsCancellationRequested)
                    break;

                // Verificar si hay conexión con el servidor
                bool hayConexion = await VerificarConexionAsync();

                if (hayConexion)
                {
                    // Procesar cola pendiente
                    await _queueService.ProcesarColaAhoraAsync();

                    // Obtener estadísticas actualizadas
                    var stats = await _queueService.ObtenerEstadisticasAsync();

                    // Notificar si hay cambios
                    if (stats.Procesados > 0 || stats.Fallidos > 0)
                    {
                        NotificarSincronizacion(stats);
                    }
                }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AppLog.Error("SyncCoordinator", $"Error en sincronización: {ex.Message}", ex);
                    await Task.Delay(TimeSpan.FromMinutes(1), ct);
                }
            }

        AppLog.Info("SyncCoordinator", "Sincronización automática detenida");
    }

    /// <summary>
    /// Verifica si hay conexión con el servidor del core (público para uso externo)
    /// </summary>
    public async Task<bool> VerificarConexionAsync()
    {
        try
        {
            // Hacer un ping simple al endpoint del core
            // Usamos un endpoint que debería estar disponible
            var response = await _httpClient.GetAsync("/core/api/v1/health");

            return response.IsSuccessStatusCode || response.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable;
        }
        catch (HttpRequestException)
        {
            AppLog.Warn("SyncCoordinator", "No hay conexión con el servidor");
            return false;
        }
        catch (Exception ex)
        {
            AppLog.Warn("SyncCoordinator", $"Error verificando conexión: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sincroniza una firma específica inmediatamente
    /// </summary>
    public async Task<bool> SincronizarFirmaAsync(string username, string nombreCompleto, string firmaDataUrl)
    {
        await _semaphore.WaitAsync();
        try
        {
            try
            {
                // Intentar subir al servidor inmediatamente
                var exito = await SubirFirmaAServidorAsync(username, firmaDataUrl);

                if (exito)
                {
                    // Actualizar estado local
                    await _dbService.ActualizarEstadoAsync(username, EstadoFirma.ConFirma);

                    AppLog.Info("SyncCoordinator", $"Firma sincronizada exitosamente: {username}");
                    Notificar(new SyncNotificacionEventArgs
                    {
                        Tipo = TipoNotificacion.FirmaSubida,
                        Username = username,
                        Mensaje = $"Firma de {nombreCompleto} subida correctamente"
                    });

                    return true;
                }
                else
                {
                    // Encolar para reintento
                    await _queueService.EncolarFirmaAsync(username, nombreCompleto, firmaDataUrl, "Guardar");

                    Notificar(new SyncNotificacionEventArgs
                    {
                        Tipo = TipoNotificacion.FirmaEncolada,
                        Username = username,
                        Mensaje = $"Firma de {nombreCompleto} guardada localmente (subida pendiente)"
                    });

                    return false;
                }
            }
            catch (Exception)
            {
                // Sin conexión - encolar
                await _queueService.EncolarFirmaAsync(username, nombreCompleto, firmaDataUrl, "Guardar");

                AppLog.Warn("SyncCoordinator", $"Sin conexión, firma encolada: {username}");
                Notificar(new SyncNotificacionEventArgs
                {
                    Tipo = TipoNotificacion.FirmaEncolada,
                    Username = username,
                    Mensaje = $"Sin conexión - Firma guardada localmente"
                });

                return false;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Encola una eliminación de firma para procesamiento posterior
    /// </summary>
    public async Task EncolarEliminacionAsync(string username, string nombreCompleto)
    {
        try
        {
            // Encolar la eliminación
            await _queueService.EncolarFirmaAsync(username, nombreCompleto, string.Empty, "Eliminar");

            // Actualizar estado local a EnProceso
            await _dbService.ActualizarEstadoAsync(username, EstadoFirma.EnProceso);

            Notificar(new SyncNotificacionEventArgs
            {
                Tipo = TipoNotificacion.FirmaEncolada,
                Username = username,
                Mensaje = $"Eliminación de {nombreCompleto} encolada (pendiente de sincronización)"
            });

            AppLog.Info("SyncCoordinator", $"Eliminación encolada: {username}");
        }
        catch (Exception ex)
        {
            AppLog.Error("SyncCoordinator", $"Error encolando eliminación: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Sube una firma al servidor del core usando el API REST
    /// PUT /core/api/v1/firmas-almacenadas/usuarios/{username}
    /// </summary>
    private async Task<bool> SubirFirmaAServidorAsync(string username, string firmaDataUrl)
    {
        try
        {
            // Agregar token de autenticación
            var token = await _keycloakAuth.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                AppLog.Warn("SyncCoordinator", "No hay token válido para subir firma");
                return false;
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            // Convertir dataURL a formato esperado por el API
            var (contenido, mimeType) = DataUrlToBase64(firmaDataUrl);

            // Crear payload según el formato esperado por el core
            var payload = new
            {
                contenido = contenido,
                mimeType = mimeType
            };

            // Hacer llamada PUT al endpoint del core
            var response = await _httpClient.PutAsJsonAsync(
                $"/core/api/v1/firmas-almacenadas/usuarios/{Uri.EscapeDataString(username)}",
                payload);

            if (response.IsSuccessStatusCode)
            {
                AppLog.Info("SyncCoordinator", $"Firma subida exitosamente al core: {username}");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                AppLog.Warn("SyncCoordinator", $"Error subiendo firma al core: {response.StatusCode} - {errorContent}");
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            AppLog.Warn("SyncCoordinator", $"Error de conexión al subir firma: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            AppLog.Error("SyncCoordinator", $"Error inesperado subiendo firma: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Convierte un dataURL a base64 puro y mimeType para enviar al servidor
    /// </summary>
    private (string contenido, string mimeType) DataUrlToBase64(string dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            throw new ArgumentException("DataURL inválido");
        }

        // Formato esperado: data:image/png;base64,iVBORw0KG...
        var match = System.Text.RegularExpressions.Regex.Match(dataUrl, @"^data:([^;]+);base64,(.+)$");

        if (!match.Success)
        {
            throw new ArgumentException("DataURL formato inválido. Se espera: data:image/png;base64,...");
        }

        var mimeType = match.Groups[1].Value;
        var base64 = match.Groups[2].Value;

        return (base64, mimeType);
    }

    /// <summary>
    /// Agrega el header de autenticación al HttpClient
    /// </summary>
    private async Task<bool> AddAuthHeaderAsync()
    {
        var token = await _keycloakAuth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        return true;
    }

    /// <summary>
    /// Notifica a los suscriptores sobre eventos de sincronización
    /// </summary>
    private void Notificar(SyncNotificacionEventArgs evento)
    {
        SyncNotificacion?.Invoke(this, evento);
    }

    /// <summary>
    /// Obtiene el estado actual de sincronización
    /// </summary>
    public async Task<ColaEstadisticas> ObtenerEstadoAsync()
    {
        return await _queueService.ObtenerEstadisticasAsync();
    }

    /// <summary>
    /// Fuerza una sincronización inmediata (manual)
    /// </summary>
    public async Task SincronizarAhoraAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            bool hayConexion = await VerificarConexionAsync();

            if (hayConexion)
            {
                await _queueService.ProcesarColaAhoraAsync();
                var stats = await _queueService.ObtenerEstadisticasAsync();
                NotificarSincronizacion(stats);
            }
            else
            {
                AppLog.Warn("SyncCoordinator", "No hay conexión para sincronizar");
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Detiene el servicio de sincronización
    /// </summary>
    public void DetenerSincronizacion()
    {
        _shutdownCts.Cancel();
        _queueService.DetenerProcesamiento();
        _sincronizacionTask?.Wait(TimeSpan.FromSeconds(5));
        AppLog.Info("SyncCoordinator", "Sincronización detenida");
    }

    /// <summary>
    /// Notifica sobre el progreso de sincronización
    /// </summary>
    private void NotificarSincronizacion(ColaEstadisticas stats)
    {
        if (stats.Procesados > 0)
        {
            Notificar(new SyncNotificacionEventArgs
            {
                Tipo = TipoNotificacion.SincronizacionCompletada,
                Mensaje = $"{stats.Procesados} firmas sincronizadas correctamente",
                Cantidad = stats.Procesados
            });
        }

        if (stats.Fallidos > 0)
        {
            Notificar(new SyncNotificacionEventArgs
            {
                Tipo = TipoNotificacion.SincronizacionFallida,
                Mensaje = $"{stats.Fallidos} firmas no pudieron sincronizarse",
                Cantidad = stats.Fallidos
            });
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DetenerSincronizacion();
        _shutdownCts?.Dispose();
        _semaphore?.Dispose();
        _httpClient?.Dispose();
    }
}

/// <summary>
/// Eventos de notificación de sincronización
/// </summary>
public class SyncNotificacionEventArgs : EventArgs
{
    public TipoNotificacion Tipo { get; set; }
    public string? Username { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public int Cantidad { get; set; }

    public string MensajeCompleto => Cantidad > 0
        ? $"{Mensaje} ({Cantidad})"
        : Mensaje;
}

/// <summary>
/// Tipos de notificación de sincronización
/// </summary>
public enum TipoNotificacion
{
    FirmaEncolada,              // "Firma guardada localmente"
    SincronizacionIniciada,    // "Sincronizando firmas pendientes..."
    FirmaSubida,              // "Firma de [Usuario] subida correctamente"
    SincronizacionCompletada,  // "X firmas sincronizadas correctamente"
    SincronizacionFallida     // "X firmas no pudieron subirse"
}
