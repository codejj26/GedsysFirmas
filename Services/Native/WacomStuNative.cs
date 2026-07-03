using System.Runtime.InteropServices;
using System.Security;

namespace FirmasApp.Services.Native;

/// <summary>
/// Declaraciones P/Invoke para Wacom STU SDK
/// </summary>
internal static class WacomStuNative
{
    private const string DllName = "wgssSTU.dll";

    #region Connection Functions

    /// <summary>
    /// Conecta al dispositivo Wacom STU
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern int stuConnect();

    /// <summary>
    /// Desconecta del dispositivo Wacom STU
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern int stuDisconnect();

    /// <summary>
    /// Verifica si hay un dispositivo conectado
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool stuIsConnected();

    /// <summary>
    /// Obtiene información del dispositivo conectado
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern int stuGetDeviceInfo(ref DeviceInfo info);

    /// <summary>
    /// Obtiene el número de dispositivos conectados
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern int stuGetDeviceCount();

    #endregion

    #region Capture Functions

    /// <summary>
    /// Inicia la captura de firma
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern int stuStartCapture();

    /// <summary>
    /// Detiene la captura de firma
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern int stuStopCapture();

    /// <summary>
    /// Limpia la pantalla de la tablet
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern int stuClearScreen();

    /// <summary>
    /// Verifica si hay una captura en progreso
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static extern bool stuIsCapturing();

    #endregion

    #region Data Functions

    /// <summary>
    /// Obtiene los datos de firma capturados
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern int stuGetSignatureData(out IntPtr data);

    /// <summary>
    /// Libera la memoria de los datos de firma
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern void stuFreeSignatureData(IntPtr data);

    /// <summary>
    /// Obtiene el número de puntos capturados
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern int stuGetPointCount();

    #endregion

    #region Callbacks

    /// <summary>
    /// Registra callback para datos de stylus
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern int stuSetPenDataCallback([MarshalAs(UnmanagedType.FunctionPtr)] PenDataCallback? callback, IntPtr userData);

    /// <summary>
    /// Registra callback para cambios de dispositivo
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern int stuSetDeviceChangeCallback([MarshalAs(UnmanagedType.FunctionPtr)] DeviceChangeCallback? callback, IntPtr userData);

    #endregion

    #region Utility Functions

    /// <summary>
    /// Obtiene la versión del SDK
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern int stuGetSDKVersion();

    /// <summary>
    /// Obtiene el último código de error
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
    internal static extern int stuGetLastError();

    /// <summary>
    /// Obtiene mensaje de error para un código
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, SetLastError = true, CharSet = CharSet.Auto)]
    internal static extern IntPtr stuGetErrorMessage(int errorCode);

    #endregion
}