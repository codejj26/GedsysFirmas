namespace FirmasApp.Models;

public class Usuario
{
    public string? Id { get; set; }
    public string CuentaUsuario { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Identificacion? Identificacion { get; set; }
    public string? Telefono { get; set; }
    public Direccion? Direccion { get; set; }
    public CargoEmpleado? Cargo { get; set; }
    public string? Estado { get; set; }
    public bool TieneFirma { get; set; }

    // Helper properties for backward compatibility
    public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();
    public string Documento => Identificacion?.Numero ?? string.Empty;
    public string EstadoFirma => TieneFirma ? "✓ Con firma" : "Sin firma";
}

public class Identificacion
{
    public string Tipo { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
}

public class Direccion
{
    public string? TipoVia { get; set; }
    public int? NumeroVia { get; set; }
    public string? LetraVia { get; set; }
    public bool? EsBis { get; set; }
    public string? LetraBis { get; set; }
    public string? SectorVia { get; set; }
    public int? NumeroGeneradora { get; set; }
    public string? LetraGeneradora { get; set; }
    public bool? GeneradoraEsBis { get; set; }
    public string? SectorGeneradora { get; set; }
    public int? NumeroPlaca { get; set; }
    public string? Barrio { get; set; }
    public string? TipoUnidad { get; set; }
    public string? NumeroUnidad { get; set; }
    public string? Bloque { get; set; }
    public string? OtrosDetalles { get; set; }
    public string? CodigoPostal { get; set; }
    public string? DireccionFormateada { get; set; }
}

public class CargoEmpleado
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}
