namespace FirmasApp.Models;

public class GedsysApiSettings
{
    public string BaseUrl { get; set; } = "https://api.development.gedsys.app";
    public string AuthToken { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}

public class WacomStuSettings
{
    // Configuración básica
    public bool EnableBiometricData { get; set; } = true;
    public bool AutoConnect { get; set; } = true;

    // Configuración nativa del SDK
    public string DllPath { get; set; } = "libs/wgssSTU.dll";
    public int ConnectionTimeoutMs { get; set; } = 5000;
    public int CaptureTimeoutSeconds { get; set; } = 30;
    public bool EnablePressure { get; set; } = true;
    public int MinPressureThreshold { get; set; } = 10;
    public bool AutoDisconnectOnIdle { get; set; } = false;
    public int IdleTimeoutSeconds { get; set; } = 300;

    // Configuración de captura
    public int SignatureWidth { get; set; } = 1024;
    public int SignatureHeight { get; set; } = 600;
    public bool UseSimulation { get; set; } = false;  // Fallback a simulación si no hay dispositivo
}
