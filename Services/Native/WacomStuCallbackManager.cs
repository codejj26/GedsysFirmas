using System.Runtime.InteropServices;
using System.Security;

namespace FirmasApp.Services.Native;

/// <summary>
/// Maneja los callbacks del SDK nativo de Wacom STU
/// </summary>
internal sealed class WacomStuCallbackManager : IDisposable
{
    private PenDataCallback? _penDataCallback;
    private DeviceChangeCallback? _deviceChangeCallback;
    private GCHandle? _userDataHandle;
    private bool _disposed;

    public void RegisterCallbacks(WacomStuService service)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WacomStuCallbackManager));

        // Mantener referencias para prevenir garbage collection
        _penDataCallback = PenDataHandler;
        _deviceChangeCallback = DeviceChangeHandler;

        _userDataHandle = GCHandle.Alloc(service, GCHandleType.Normal);

        // Registrar callbacks con código nativo
        var penResult = WacomStuNative.stuSetPenDataCallback(
            _penDataCallback,
            GCHandle.ToIntPtr(_userDataHandle.Value)
        );

        var devResult = WacomStuNative.stuSetDeviceChangeCallback(
            _deviceChangeCallback,
            GCHandle.ToIntPtr(_userDataHandle.Value)
        );

        if (penResult != 0 || devResult != 0)
        {
            throw new InvalidOperationException($"Error registrando callbacks: pen={penResult}, dev={devResult}");
        }
    }

    public void UnregisterCallbacks()
    {
        if (_disposed) return;

        try
        {
            // Desregistrar callbacks pasando null
            WacomStuNative.stuSetPenDataCallback(null, IntPtr.Zero);
            WacomStuNative.stuSetDeviceChangeCallback(null, IntPtr.Zero);
        }
        catch
        {
            /* Ignorar errores al desregistrar */
        }

        _userDataHandle?.Free();
        _userDataHandle = null;
        _penDataCallback = null;
        _deviceChangeCallback = null;
    }

    private void PenDataHandler(ref PenData data, IntPtr userData)
    {
        try
        {
            var handle = GCHandle.FromIntPtr(userData);
            var target = handle.Target as WacomStuService;
            target?.OnPenDataReceived(data);
        }
        catch (Exception)
        {
            // Ignorar errores en callback
        }
    }

    private void DeviceChangeHandler(int eventType, IntPtr userData)
    {
        try
        {
            var handle = GCHandle.FromIntPtr(userData);
            var target = handle.Target as WacomStuService;
            target?.OnDeviceChanged(eventType);
        }
        catch (Exception)
        {
            // Ignorar errores en callback
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterCallbacks();
    }
}