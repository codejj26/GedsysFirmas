using System.Net;
using System.Text;

namespace FirmasApp.Services;

/// <summary>
/// Escucha un puerto HTTP local para recibir el callback de Keycloak cuando
/// el redirect_uri usa esquema http:// o https://. Es una alternativa más
/// confiable al custom protocol firmasapp:// en navegadores modernos.
/// </summary>
public class HttpCallbackService : IDisposable
{
    private readonly string _redirectUri;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _disposed;

    public event EventHandler<string>? CallbackReceived;
    public event EventHandler<string>? ErrorOccurred;

    public HttpCallbackService(string redirectUri)
    {
        _redirectUri = redirectUri;
    }

    public bool Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HttpCallbackService));

        try
        {
            var uri = new Uri(_redirectUri);
            var prefix = $"{uri.Scheme}://{uri.Host}:{uri.Port}{uri.AbsolutePath}";
            if (!prefix.EndsWith("/")) prefix += "/";

            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _listener.Start();

            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));

            AppLog.Info("HttpCallback", $"Escuchando en {prefix}");
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("HttpCallback", $"No se pudo iniciar listener: {ex.Message}", ex);
            ErrorOccurred?.Invoke(this, $"No se pudo iniciar el listener HTTP: {ex.Message}");
            return false;
        }
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            if (_listener?.IsListening == true)
            {
                _listener.Stop();
            }
            _listener?.Close();
        }
        catch (Exception ex)
        {
            AppLog.Warn("HttpCallback", $"Error deteniendo listener: {ex.Message}");
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        var listener = _listener;
        if (listener == null) return;

        try
        {
            while (!ct.IsCancellationRequested && listener.IsListening)
            {
                var context = await listener.GetContextAsync();
                if (context == null) continue;

                try
                {
                    HandleRequest(context);
                }
                catch (Exception ex)
                {
                    AppLog.Error("HttpCallback", $"Error manejando request: {ex.Message}", ex);
                }
            }
        }
        catch (HttpListenerException) when (ct.IsCancellationRequested)
        {
            // Cierre normal
        }
        catch (Exception ex)
        {
            AppLog.Error("HttpCallback", $"Error en listen loop: {ex.Message}", ex);
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        var code = request.QueryString["code"];
        var state = request.QueryString["state"];
        var error = request.QueryString["error"];

        AppLog.Debug("HttpCallback", $"Request: {request.Url}");

        var html = BuildResponseHtml(string.IsNullOrEmpty(error), string.IsNullOrEmpty(code));
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        response.ContentType = "text/html; charset=utf-8";
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.OutputStream.Close();

        if (!string.IsNullOrEmpty(error))
        {
            ErrorOccurred?.Invoke(this, $"Error en callback: {error}");
            return;
        }

        if (!string.IsNullOrEmpty(code))
        {
            var callbackUrl = $"{_redirectUri}?code={Uri.EscapeDataString(code)}"
                + (string.IsNullOrEmpty(state) ? "" : $"&state={Uri.EscapeDataString(state)}");
            AppLog.Info("HttpCallback", $"Callback recibido: {callbackUrl}");
            CallbackReceived?.Invoke(this, callbackUrl);
        }
    }

    private static string BuildResponseHtml(bool success, bool hasCode) => success && hasCode
        ? """
<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="utf-8">
    <title>Autenticación completada</title>
    <style>
        body { font-family: -apple-system, Segoe UI, sans-serif; display: flex; justify-content: center;
               align-items: center; height: 100vh; margin: 0; background: #f5f7fa; }
        .card { text-align: center; background: white; padding: 50px 60px; border-radius: 10px;
                box-shadow: 0 6px 24px rgba(0,0,0,0.08); }
        h1 { color: #2e7d32; margin: 0 0 12px 0; font-size: 26px; }
        p { color: #555; margin: 6px 0; }
    </style>
</head>
<body>
    <div class="card">
        <h1>✓ Autenticación completada</h1>
        <p>Ya puedes cerrar esta ventana y volver a la aplicación.</p>
        <p style="color:#888; font-size:13px; margin-top:24px;">Esta pestaña se cerrará automáticamente.</p>
    </div>
    <script>setTimeout(function(){ window.close(); }, 3000);</script>
</body>
</html>
"""
        : """
<!DOCTYPE html>
<html lang="es">
<head><meta charset="utf-8"><title>Error</title>
<style>body{font-family:sans-serif;display:flex;justify-content:center;align-items:center;height:100vh;background:#fff5f5}
.card{text-align:center;background:white;padding:40px;border-radius:10px;box-shadow:0 4px 20px rgba(0,0,0,.08)}
h1{color:#c62828;margin:0 0 12px}</style></head>
<body><div class="card"><h1>✗ Error</h1><p>No se recibió código de autorización.</p>
<p style="color:#888;font-size:13px">Cierra esta ventana e intenta de nuevo.</p></div></body></html>
""";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _cts?.Dispose();
        _listener = null;
    }
}
