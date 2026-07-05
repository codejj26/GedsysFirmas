namespace FirmasApp.Models;

/// <summary>
/// Representa el estado de sincronización con el servidor
/// </summary>
public class Sincronizacion
{
    /// <summary>
    /// Identificador único (siempre es 1, singleton)
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Timestamp de la última sincronización exitosa
    /// </summary>
    public string? UltimaSincro { get; set; }

    /// <summary>
    /// Cantidad de firmas pendientes de procesamiento
    /// </summary>
    public int Pendientes { get; set; } = 0;

    /// <summary>
    /// Cantidad de firmas procesadas exitosamente
    /// </summary>
    public int Procesados { get; set; } = 0;

    /// <summary>
    /// Cantidad de firmas que fallaron el procesamiento
    /// </summary>
    public int Fallidos { get; set; } = 0;

    /// <summary>
    /// Estado actual de la sincronización: Sincronizado, Procesando, Error
    /// </summary>
    public string Estado { get; set; } = "Sincronizado";
}
