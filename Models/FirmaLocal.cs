namespace FirmasApp.Models;

/// <summary>
/// Representa una firma almacenada localmente en SQLite
/// </summary>
public class FirmaLocal
{
    /// <summary>
    /// Identificador único autoincremental
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Username del usuario (único)
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Nombre completo del usuario
    /// </summary>
    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>
    /// Firma en formato Base64
    /// </summary>
    public string FirmaDataUrl { get; set; } = string.Empty;

    /// <summary>
    /// Estado actual de la firma
    /// </summary>
    public EstadoFirma EstadoFirma { get; set; }

    /// <summary>
    /// Fecha/hora cuando se guardó localmente (ISO8601)
    /// </summary>
    public string FechaLocal { get; set; } = string.Empty;

    /// <summary>
    /// Fecha/hora cuando se confirmó en servidor (ISO8601), null si no se ha subido
    /// </summary>
    public string? FechaServidor { get; set; }

    /// <summary>
    /// Versión del registro para control de concurrencia
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Timestamp de creación del registro
    /// </summary>
    public string CreadoEn { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp de última actualización
    /// </summary>
    public string ActualizadoEn { get; set; } = string.Empty;
}
