namespace FirmasApp.Models;

/// <summary>
/// Representa una firma en cola esperando ser procesada
/// </summary>
public class ColaFirma
{
    /// <summary>
    /// Identificador único autoincremental
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Username del usuario
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de operación a realizar: Guardar, Eliminar o Actualizar
    /// </summary>
    public string Operacion { get; set; } = string.Empty;

    /// <summary>
    /// Firma en formato Base64 (null para Eliminar)
    /// </summary>
    public string? FirmaDataUrl { get; set; }

    /// <summary>
    /// Número de intentos de procesamiento realizados
    /// </summary>
    public int Intentos { get; set; } = 0;

    /// <summary>
    /// Máximo número de intentos permitidos
    /// </summary>
    public int MaxIntentos { get; set; } = 5;

    /// <summary>
    /// Mensaje de error del último intento fallido
    /// </summary>
    public string? UltimoError { get; set; }

    /// <summary>
    /// Estado actual del procesamiento: Pendiente, Procesando, Fallido
    /// </summary>
    public string Estado { get; set; } = "Pendiente";

    /// <summary>
    /// Timestamp de creación del registro en cola
    /// </summary>
    public string CreadoEn { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp para el próximo intento de procesamiento (ISO8601)
    /// </summary>
    public string? ProximoIntento { get; set; }
}
