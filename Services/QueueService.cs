using FirmasApp.Models;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace FirmasApp.Services;

/// <summary>
/// Servicio para gestión de cola de firmas pendientes de sincronización
/// Procesa firmas en background con reintentos automáticos
/// </summary>
public class QueueService : IDisposable
{
    private readonly LocalDbService _dbService;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancelacionesProceso;
    private readonly CancellationTokenSource _shutdownCts;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;
    private Task? _procesamientoTask;

    // Configuración de backoff exponencial
    private readonly TimeSpan[] _backoffIntervals = new[]
    {
        TimeSpan.FromSeconds(30),    // Intento 1: 30s
        TimeSpan.FromSeconds(60),    // Intento 2: 60s
        TimeSpan.FromSeconds(120),   // Intento 3: 120s
        TimeSpan.FromSeconds(300),   // Intento 4: 300s
        TimeSpan.FromSeconds(600)     // Intento 5: 600s
    };

    public QueueService(LocalDbService dbService)
    {
        _dbService = dbService;
        _cancelacionesProceso = new();
        _shutdownCts = new CancellationTokenSource();
    }

    /// <summary>
    /// Encola una firma para procesamiento posterior
    /// </summary>
    public async Task EncolarFirmaAsync(string username, string nombreCompleto, string firmaDataUrl, string operacion)
    {
        try
        {
            // Guardar en BD local con estado EnProceso
            await _dbService.GuardarAsync(username, nombreCompleto, firmaDataUrl, EstadoFirma.EnProceso);

            // Encolar en cola_firmas
            await _dbService.EncolarAsync(username, firmaDataUrl, operacion);

            AppLog.Info("QueueService", $"Firma encolada: {username} - {operacion}");
        }
        catch (Exception ex)
        {
            AppLog.Error("QueueService", $"Error encolando firma: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Inicia el procesamiento de cola en background
    /// </summary>
    public void IniciarProcesamiento()
    {
        if (_procesamientoTask != null && !_procesamientoTask.IsCompleted)
        {
            return; // Ya está corriendo
        }

        _procesamientoTask = Task.Run(async () => await ProcesamientoLoopAsync(_shutdownCts.Token));
        AppLog.Info("QueueService", "Procesamiento de cola iniciado");
    }

    /// <summary>
    /// Loop principal de procesamiento de cola
    /// </summary>
    private async Task ProcesamientoLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Esperar 30 segundos entre ciclos de procesamiento
                await Task.Delay(TimeSpan.FromSeconds(30), ct);

                if (ct.IsCancellationRequested)
                    break;

                await ProcesarColaPendienteAsync(ct);
            }
            catch (TaskCanceledException)
            {
                // Cancelación solicitada, salir del loop
                break;
            }
            catch (Exception ex)
            {
                AppLog.Error("QueueService", $"Error en loop de procesamiento: {ex.Message}", ex);
                await Task.Delay(TimeSpan.FromMinutes(1), ct); // Esperar 1 minuto antes de reintentar
            }
        }

        AppLog.Info("QueueService", "Procesamiento de cola detenido");
    }

    /// <summary>
    /// Procesa items pendientes de la cola
    /// </summary>
    private async Task ProcesarColaPendienteAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            // Obtener items pendientes (máximo 10 por ciclo)
            var pendientes = await _dbService.ObtenerPendientesAsync();

            if (!pendientes.Any())
            {
                return; // No hay pendientes
            }

            AppLog.Info("QueueService", $"Procesando {pendientes.Count} firmas pendientes");

            // Procesar cada item secuencialmente
            foreach (var item in pendientes)
            {
                if (ct.IsCancellationRequested)
                    break;

                await ProcesarItemIndividualAsync(item);
            }

            // Actualizar estado de sincronización
            await ActualizarEstadoSincronizacionAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Procesa un item individual de la cola
    /// </summary>
    private async Task ProcesarItemIndividualAsync(ColaFirma item)
    {
        try
        {
            // Marcar como procesando
            await _dbService.MarcarProcesandoAsync(item.Id);

            // Procesar según la operación
            bool exito = item.Operacion.ToLower() switch
            {
                "guardar" => await ProcesarGuardadoAsync(item),
                "eliminar" => await ProcesarEliminacionAsync(item),
                "actualizar" => await ProcesarActualizacionAsync(item),
                _ => false
            };

            if (exito)
            {
                // Eliminar de cola
                await _dbService.EliminarDeColaAsync(item.Id);
                AppLog.Info("QueueService", $"Item procesado exitosamente: {item.Username} - {item.Operacion}");
            }
            else
            {
                // Registrar fallo para reintento
                await _dbService.RegistrarFalloAsync(item.Id, "Error en procesamiento");
                AppLog.Warn("QueueService", $"Item fallido: {item.Username} - {item.Operacion}");
            }
        }
        catch (Exception ex)
        {
            // Registrar fallo por excepción
            await _dbService.RegistrarFalloAsync(item.Id, ex.Message);
            AppLog.Error("QueueService", $"Error procesando item: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Procesa operación de guardado
    /// </summary>
    private async Task<bool> ProcesarGuardadoAsync(ColaFirma item)
    {
        try
        {
            // Aquí iría la llamada real a la API para guardar la firma
            // Por ahora simulamos éxito
            await Task.Delay(TimeSpan.FromMilliseconds(500)); // Simular llamada a API

            // Actualizar estado local a ConFirma
            await _dbService.ActualizarEstadoAsync(item.Username, EstadoFirma.ConFirma);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Procesa operación de eliminación
    /// </summary>
    private async Task<bool> ProcesarEliminacionAsync(ColaFirma item)
    {
        try
        {
            // Aquí iría la llamada real a la API para eliminar la firma
            await Task.Delay(TimeSpan.FromMilliseconds(500)); // Simular llamada a API

            // Eliminar de BD local
            await _dbService.EliminarAsync(item.Username);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Procesa operación de actualización
    /// </summary>
    private async Task<bool> ProcesarActualizacionAsync(ColaFirma item)
    {
        try
        {
            // Aquí iría la llamada real a la API para actualizar la firma
            await Task.Delay(TimeSpan.FromMilliseconds(500)); // Simular llamada a API

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Obtiene estadísticas de la cola
    /// </summary>
    public async Task<ColaEstadisticas> ObtenerEstadisticasAsync()
    {
        var sincro = await _dbService.ObtenerSincronizacionAsync();

        return new ColaEstadisticas
        {
            Pendientes = sincro.Pendientes,
            Procesados = sincro.Procesados,
            Fallidos = sincro.Fallidos,
            Estado = sincro.Estado
        };
    }

    /// <summary>
    /// Fuerza el procesamiento inmediato de la cola (útil para testing o acción manual)
    /// </summary>
    public async Task ProcesarColaAhoraAsync()
    {
        await ProcesarColaPendienteAsync(CancellationToken.None);
    }

    /// <summary>
    /// Detiene el procesamiento de cola
    /// </summary>
    public void DetenerProcesamiento()
    {
        _shutdownCts.Cancel();
        _procesamientoTask?.Wait(TimeSpan.FromSeconds(5));
        AppLog.Info("QueueService", "Procesamiento de cola detenido");
    }

    /// <summary>
    /// Actualiza el estado de sincronización después de procesar cola
    /// </summary>
    private async Task ActualizarEstadoSincronizacionAsync()
    {
        var sincro = await _dbService.ObtenerSincronizacionAsync();
        var pendientes = await _dbService.ObtenerPendientesAsync();

        await _dbService.ActualizarSincronizacionAsync(
            pendientes: pendientes.Count,
            procesados: sincro.Procesados,
            fallidos: sincro.Fallidos,
            estado: pendientes.Count > 0 ? "Procesando" : "Sincronizado"
        );
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DetenerProcesamiento();
        _shutdownCts?.Dispose();
        _semaphore?.Dispose();

        foreach (var cts in _cancelacionesProceso.Values)
        {
            cts?.Dispose();
        }
        _cancelacionesProceso.Clear();
    }
}

/// <summary>
/// Estadísticas del estado de la cola
/// </summary>
public class ColaEstadisticas
{
    public int Pendientes { get; set; }
    public int Procesados { get; set; }
    public int Fallidos { get; set; }
    public string Estado { get; set; } = string.Empty;
}
