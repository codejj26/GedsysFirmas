using System.Runtime.InteropServices;

namespace FirmasApp.Services.Native;

/// <summary>
/// Estructuras y delegates para Wacom STU SDK
/// </summary>

[StructLayout(LayoutKind.Sequential)]
public struct PenData
{
    public short X;           // Posición X (0-1023 para STU-430)
    public short Y;           // Posición Y (0-599 para STU-430)
    public byte Pressure;     // Presión (0-255)
    public ushort TimeStamp;  // Timestamp en milisegundos
}

[StructLayout(LayoutKind.Sequential)]
public struct DeviceInfo
{
    public int Width;         // Ancho pantalla (1024 para STU-430)
    public int Height;        // Alto pantalla (600 para STU-430)
    public int ModelName;     // Modelo del dispositivo
    public int FirmwareVersion;
}

[StructLayout(LayoutKind.Sequential)]
public struct SignatureData
{
    public IntPtr Points;     // Array de PenData
    public int PointCount;
    public int Width;
    public int Height;
    public int Duration;      // Duración en ms
}

// Delegates para callbacks
public delegate void PenDataCallback(ref PenData data, IntPtr userData);
public delegate void DeviceChangeCallback(int eventType, IntPtr userData);

public enum DeviceEventType
{
    Connected = 1,
    Disconnected = 0,
    Error = -1
}

public enum ErrorCode
{
    Success = 0,
    Error = -1,
    NotConnected = -2,
    AlreadyConnected = -3,
    CaptureInProgress = -4
}