using Microsoft.Win32;
using System.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;
using FirmasApp.Services;

public class ProtocolRegistrationService
{
    private const string PROTOCOL_NAME = "firmasapp";
    private const string PROTOCOL_DISPLAY_NAME = "Firmas App";
    private const string PROTOCOL_PATH = $@"Software\Classes\{PROTOCOL_NAME}";

    /// <summary>
    /// Registra el protocolo personalizado firmasapp:// en el registro de Windows
    /// </summary>
    public bool RegisterProtocol()
    {
        try
        {
            var exePath = ResolveExecutablePath();
            if (string.IsNullOrWhiteSpace(exePath))
            {
                throw new Exception("No se pudo obtener la ruta del ejecutable");
            }

            AppLog.Info("ProtocolRegistration", $"Registrando protocolo con exePath: {exePath}");

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(PROTOCOL_PATH, false);
                AppLog.Debug("ProtocolRegistration", "Registro anterior eliminado");
            }
            catch (Exception ex)
            {
                AppLog.Debug("ProtocolRegistration", $"No había registro previo: {ex.Message}");
            }

            using (var key = Registry.CurrentUser.CreateSubKey(PROTOCOL_PATH))
            {
                key.SetValue(null, $"URL:{PROTOCOL_DISPLAY_NAME} Protocol");
                key.SetValue("URL Protocol", "");

                using (var appKey = key.CreateSubKey("Application"))
                {
                    appKey.SetValue("ApplicationCompany", "Gedsys");
                    appKey.SetValue("ApplicationDescription", "Sistema de Gestión de Firmas");
                }

                using (var iconKey = key.CreateSubKey("DefaultIcon"))
                {
                    iconKey.SetValue(null, $"\"{exePath}\",0");
                }

                using (var commandKey = key.CreateSubKey(@"shell\open\command"))
                {
                    commandKey.SetValue(null, $"\"{exePath}\" \"%1\"");
                }

                AppLog.Debug("ProtocolRegistration", "Claves principales registradas");
            }

            using (var testKey = Registry.CurrentUser.OpenSubKey($@"{PROTOCOL_PATH}\shell\open\command"))
            {
                var command = testKey?.GetValue(null)?.ToString();
                AppLog.Info("ProtocolRegistration", $"Comando registrado: {command}");
            }

            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("ProtocolRegistration", ex.Message, ex);
            throw new Exception($"Error registrando protocolo: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Verifica si el protocolo está registrado
    /// </summary>
    public bool IsProtocolRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"{PROTOCOL_PATH}\shell\open\command");
            return key?.GetValue(null) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Elimina el registro del protocolo
    /// </summary>
    public bool UnregisterProtocol()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(PROTOCOL_PATH, false);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Warn("ProtocolRegistration", $"No se pudo eliminar registro: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Extrae el código de autorización de una URL de callback
    /// </summary>
    public string? ExtractAuthCodeFromUrl(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (!url.StartsWith($"{PROTOCOL_NAME}://", StringComparison.OrdinalIgnoreCase)) return null;

            var uri = new Uri(url);
            var query = QueryHelpers.ParseQuery(uri.Query);
            return query.TryGetValue("code", out var codeValue) ? codeValue.ToString() : null;
        }
        catch (Exception ex)
        {
            AppLog.Warn("ProtocolRegistration", $"No se pudo extraer code de {url}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Detecta la ruta del ejecutable. Si se ejecuta con `dotnet run`, devuelve la ruta del dll.
    /// </summary>
    private static string? ResolveExecutablePath()
    {
        var mainModule = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(mainModule)) return null;

        if (mainModule.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            var dllPath = Process.GetCurrentProcess().MainModule?.FileName;
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) &&
                processPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return $"dotnet \"{processPath}\"";
            }
        }

        return mainModule;
    }
}
