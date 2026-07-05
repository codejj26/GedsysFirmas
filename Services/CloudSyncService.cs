using FirmasApp.Models;
using FirmasApp.Services;

namespace FirmasApp.Services;

/// <summary>
/// Servicio de sincronización cloud entre SQLite local y PostgreSQL (Supabase)
/// Sincronización bidireccional automática con detección de cambios
/// </summary>
public class CloudSyncService : IDisposable
{
    private readonly CloudDbService _cloudDb;
    private readonly LocalDbService _localDb;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly CancellationTokenSource _shutdownCts;
    private bool _disposed;
    private Task? _sincronizacionTask;

    public CloudSyncService(CloudDbService cloudDb, LocalDbService localDb)
    {
        _cloudDb = cloudDb;
        _localDb = localDb;
        _shutdownCts = new CancellationTokenSource();
    }

    /// <summary>
    /// Inicia la sincronización automática con la nube
    /// </summary>
    public void IniciarSincronizacion()
    {
        if (_sincronizacionTask != null && !_sincronizacionTask.IsCompleted)
        {
            return; // Ya está corriendo
        }

        _sincronizacionTask = Task.Run(async () => await SincronizacionLoopAsync(_shutdownCts.Token));

        AppLog.Info("CloudSync", "Sincronización cloud iniciada");
    }

    /// <summary>
    /// Loop principal de sincronización cloud
    /// </summary>
    private async Task SincronizacionLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Esperar 2 minutos entre ciclos de sincronización cloud
                await Task.Delay(TimeSpan.FromMinutes(2), ct);

                if (ct.IsCancellationRequested)
                    break;

                // Verificar conexión con Supabase
                bool hayConexionCloud = await _cloudDb.ProbarConexionAsync();

                if (hayConexionCloud)
                {
                    await SincronizarConCloudAsync();
                }
                else
                {
                    AppLog.Warn("CloudSync", "Sin conexión con Supabase, se reintentará en 2 minutos");
                }
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLog.Error("CloudSync", $"Error en sincronización cloud: {ex.Message}", ex);
                await Task.Delay(TimeSpan.FromMinutes(5), ct); // Esperar 5 minutos antes de reintentar
            }
        }

        AppLog.Info("CloudSync", "Sincronización cloud detenida");
    }

    /// <summary>
    /// Sincroniza datos entre SQLite y PostgreSQL
    /// </summary>
    private async Task SincronizarConCloudAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            AppLog.Info("CloudSync", "Iniciando sincronización con PostgreSQL...");

            // 1. Obtener última fecha de sincronización
            var sincroCloud = await _cloudDb.ObtenerSincronizacionAsync();
            var ultimaSincro = sincroCloud.UltimaSincro ?? DateTime.MinValue;

            // 2. Subir cambios locales a PostgreSQL
            await SubirCambiosLocalesAsync(ultimaSincro);

            // 3. Descargar cambios de PostgreSQL a SQLite
            await DescargarCambiosCloudAsync(ultimaSincro);

            // 4. Actualizar estado de sincronización
            var firmasPendientes = await _localDb.ObtenerPendientesAsync();
            await _cloudDb.ActualizarSincronizacionAsync(
                pendientes: firmasPendientes.Count,
                procesados: sincroCloud.Procesados,
                fallidos: sincroCloud.Fallidos,
                estado: firmasPendientes.Count > 0 ? "Parcialmente Sincronizado" : "Sincronizado"
            );

            AppLog.Info("CloudSync", "Sincronización con PostgreSQL completada");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Sube cambios locales a PostgreSQL
    /// </summary>
    private async Task SubirCambiosLocalesAsync(DateTime ultimaSincro)
    {
        try
        {
            // Obtener firmas locales modificadas después de la última sincro
            var firmasLocales = await _localDb.ObtenerTodasAsync();
            var firmasModificadas = firmasLocales
                .Where(f =>
                {
                    if (DateTime.TryParse(f.FechaServidor, out var fechaServidor))
                    {
                        return fechaServidor > ultimaSincro;
                    }
                    return true; // Si no se puede parsear, considerar como modificada
                })
                .ToList();

            if (firmasModificadas.Any())
            {
                AppLog.Info("CloudSync", $"Subiendo {firmasModificadas.Count} firmas a PostgreSQL...");

                foreach (var firma in firmasModificadas)
                {
                    try
                    {
                        await _cloudDb.GuardarFirmaAsync(firma);
                        AppLog.Debug("CloudSync", $"Firma {firma.Username} subida a PostgreSQL");
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error("CloudSync", $"Error subiendo firma {firma.Username}: {ex.Message}", ex);
                    }
                }

                AppLog.Info("CloudSync", $"Subidas {firmasModificadas.Count} firmas a PostgreSQL");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("CloudSync", $"Error subiendo cambios locales: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Descarga cambios de PostgreSQL a SQLite
    /// </summary>
    private async Task DescargarCambiosCloudAsync(DateTime ultimaSincro)
    {
        try
        {
            // Obtener firmas modificadas en PostgreSQL
            var firmasCloud = await _cloudDb.ObtenerFirmasModificadasAsync(ultimaSincro);

            if (firmasCloud.Any())
            {
                AppLog.Info("CloudSync", $"Descargando {firmasCloud.Count} firmas desde PostgreSQL...");

                foreach (var firmaCloud in firmasCloud)
                {
                    try
                    {
                        // Verificar si ya existe localmente
                        var firmaLocal = await _localDb.ObtenerAsync(firmaCloud.Username);

                        if (firmaLocal == null)
                        {
                            // No existe localmente, insertar
                            await _localDb.GuardarAsync(
                                firmaCloud.Username,
                                firmaCloud.NombreCompleto,
                                firmaCloud.FirmaDataUrl,
                                firmaCloud.EstadoFirma
                            );
                            AppLog.Debug("CloudSync", $"Firma {firmaCloud.Username} insertada localmente");
                        }
                        else if (DateTime.TryParse(firmaCloud.FechaServidor, out var fechaCloud) &&
                                DateTime.TryParse(firmaLocal.FechaServidor, out var fechaLocal) &&
                                fechaCloud > fechaLocal)
                        {
                            // La versión cloud es más reciente, actualizar
                            await _localDb.GuardarAsync(
                                firmaCloud.Username,
                                firmaCloud.NombreCompleto,
                                firmaCloud.FirmaDataUrl,
                                firmaCloud.EstadoFirma
                            );
                            AppLog.Debug("CloudSync", $"Firma {firmaCloud.Username} actualizada desde cloud");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error("CloudSync", $"Error descargando firma {firmaCloud.Username}: {ex.Message}", ex);
                    }
                }

                AppLog.Info("CloudSync", $"Descargadas {firmasCloud.Count} firmas desde PostgreSQL");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("CloudSync", $"Error descargando cambios cloud: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Fuerza una sincronización inmediata con la nube
    /// </summary>
    public async Task SincronizarAhoraAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            await SincronizarConCloudAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Obtiene estadísticas de sincronización cloud
    /// </summary>
    public async Task<CloudSyncStats> ObtenerEstadisticasAsync()
    {
        try
        {
            var hayConexion = await _cloudDb.ProbarConexionAsync();
            var sincroCloud = hayConexion ? await _cloudDb.ObtenerSincronizacionAsync() : null;
            var firmasLocales = await _localDb.ObtenerTodasAsync();
            var firmasCloud = hayConexion ? await _cloudDb.ObtenerTodasFirmasAsync() : new List<FirmaLocal>();

            return new CloudSyncStats
            {
                ConectadoCloud = hayConexion,
                FirmasLocales = firmasLocales.Count,
                FirmasCloud = firmasCloud.Count,
                UltimaSincro = sincroCloud?.UltimaSincro,
                EstadoCloud = sincroCloud?.Estado ?? "Desconectado",
                PendientesSincro = sincroCloud?.Pendientes ?? 0
            };
        }
        catch (Exception ex)
        {
            AppLog.Error("CloudSync", $"Error obteniendo estadísticas: {ex.Message}", ex);
            return new CloudSyncStats
            {
                ConectadoCloud = false,
                EstadoCloud = "Error"
            };
        }
    }

    /// <summary>
    /// Detiene la sincronización cloud
    /// </summary>
    public void DetenerSincronizacion()
    {
        _shutdownCts.Cancel();
        _sincronizacionTask?.Wait(TimeSpan.FromSeconds(10));
        AppLog.Info("CloudSync", "Sincronización cloud detenida");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DetenerSincronizacion();
        _shutdownCts?.Dispose();
        _semaphore?.Dispose();
    }
}

/// <summary>
/// Estadísticas de sincronización cloud
/// </summary>
public class CloudSyncStats
{
    public bool ConectadoCloud { get; set; }
    public int FirmasLocales { get; set; }
    public int FirmasCloud { get; set; }
    public DateTime? UltimaSincro { get; set; }
    public string EstadoCloud { get; set; } = string.Empty;
    public int PendientesSincro { get; set; }
}