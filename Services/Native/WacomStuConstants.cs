namespace FirmasApp.Services.Native;

/// <summary>
/// Constantes y códigos de error para Wacom STU SDK
/// </summary>
internal static class WacomStuConstants
{
    // Device information
    public const int STU_430_WIDTH = 1024;
    public const int STU_430_HEIGHT = 600;
    public const int STU_430_MAX_PRESSURE = 255;

    // Error codes
    public const int SUCCESS = 0;
    public const int ERROR = -1;
    public const int NOT_CONNECTED = -2;
    public const int ALREADY_CONNECTED = -3;
    public const int CAPTURE_IN_PROGRESS = -4;
    public const int DEVICE_NOT_FOUND = -5;
    public const int ACCESS_DENIED = -6;
    public const int TIMEOUT = -7;

    // Connection states
    public const int DISCONNECTED = 0;
    public const int CONNECTED = 1;

    // Capture limits
    public const int MIN_PRESSURE_THRESHOLD = 5;
    public const int DEFAULT_PRESSURE_THRESHOLD = 10;
    public const int MAX_PRESSURE_THRESHOLD = 50;

    // Timeout values (ms)
    public const int DEFAULT_CONNECTION_TIMEOUT = 5000;
    public const int DEFAULT_CAPTURE_TIMEOUT = 30000;
    public const int MIN_CAPTURE_TIMEOUT = 5000;
    public const int MAX_CAPTURE_TIMEOUT = 60000;

    // Device capabilities
    public const bool SUPPORTS_PRESSURE = true;
    public const bool SUPPORTS_TIMESTAMP = true;
}