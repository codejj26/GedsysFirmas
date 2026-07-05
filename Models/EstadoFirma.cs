namespace FirmasApp.Models;

/// <summary>
/// Estados posibles de una firma en el sistema local
/// </summary>
public enum EstadoFirma
{
    /// <summary>
    /// Usuario sin firma registrada
    /// </summary>
    SinFirma = 0,

    /// <summary>
    /// Firma guardada localmente y en proceso de subida al servidor
    /// </summary>
    EnProceso = 1,

    /// <summary>
    /// Firma confirmada y guardada en el servidor
    /// </summary>
    ConFirma = 2,

    /// <summary>
    /// Firma guardada localmente pero falló la subida al servidor
    /// </summary>
    FallaSubida = 3
}
