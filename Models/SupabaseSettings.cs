namespace FirmasApp.Models;

/// <summary>
/// Configuración de conexión a Supabase/PostgreSQL
/// </summary>
public class SupabaseSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "postgres";
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = string.Empty;
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Construye la cadena de conexión para Npgsql
    /// </summary>
    public string BuildConnectionString()
    {
        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(Password))
        {
            return string.Empty;
        }

        return $"Host={Host};Port={Port};Username={Username};Password={Password};Database={Database}";
    }

    /// <summary>
    /// Valida si la configuración es correcta
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Host) &&
               !string.IsNullOrWhiteSpace(Username) &&
               !string.IsNullOrWhiteSpace(Password) &&
               Port > 0;
    }
}