using System.Windows;
using FirmasApp.Services;

namespace FirmasApp.Views;

public partial class WaitingForAuthDialog : Window
{
    private string? _receivedCallback;
    private readonly string _mechanism;
    private readonly Func<string, Task<bool>> _processCallbackAsync;
    private readonly Action _reopenBrowser;
    private bool _cancelled;

    public string? ReceivedCallback => _receivedCallback;
    public bool WasCancelled => _cancelled;

    public WaitingForAuthDialog(
        string mechanism,
        string authUrl,
        string initialStatus,
        Func<string, Task<bool>> processCallbackAsync,
        Action reopenBrowser)
    {
        InitializeComponent();

        _mechanism = mechanism;
        _processCallbackAsync = processCallbackAsync;
        _reopenBrowser = reopenBrowser;

        TxtMechanism.Text = $"Mecanismo: {_mechanism}";
        TxtStatus.Text = initialStatus;
        TxtAuthUrl.Text = authUrl;

        AppLog.Info("WaitingDialog", $"Diálogo abierto. Mecanismo: {_mechanism}");
    }

    /// <summary>
    /// Llamado por el CallbackReceiver cuando llega el callback.
    /// </summary>
    public async Task NotifyCallbackAsync(string callbackUrl)
    {
        if (Dispatcher.CheckAccess())
        {
            await HandleCallbackAsync(callbackUrl);
        }
        else
        {
            await Dispatcher.InvokeAsync(async () => await HandleCallbackAsync(callbackUrl));
        }
    }

    private async Task HandleCallbackAsync(string callbackUrl)
    {
        try
        {
            TxtStatus.Text = "Callback recibido. Procesando token...";
            AppLog.Info("WaitingDialog", $"Callback recibido, procesando...");

            var ok = await _processCallbackAsync(callbackUrl);
            if (ok)
            {
                _receivedCallback = callbackUrl;
                DialogResult = true;
                Close();
            }
            else
            {
                TxtStatus.Text = "No se pudo procesar el callback. Use 'Pegar URL manualmente' o intente de nuevo.";
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("WaitingDialog", $"Error procesando callback: {ex.Message}", ex);
            TxtStatus.Text = $"Error: {ex.Message}";
        }
    }

    public void UpdateStatus(string status)
    {
        if (Dispatcher.CheckAccess())
        {
            TxtStatus.Text = status;
        }
        else
        {
            Dispatcher.Invoke(() => TxtStatus.Text = status);
        }
    }

    public void ShowError(string error)
    {
        if (Dispatcher.CheckAccess())
        {
            TxtStatus.Text = $"⚠ {error}";
        }
        else
        {
            Dispatcher.Invoke(() => TxtStatus.Text = $"⚠ {error}");
        }
    }

    private void BtnCopiarUrl_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(TxtAuthUrl.Text);
            TxtStatus.Text = "URL copiada al portapapeles. Péguela en su navegador si la app no la abrió.";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"No se pudo copiar: {ex.Message}";
        }
    }

    private void BtnReabrirNavegador_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _reopenBrowser?.Invoke();
            TxtStatus.Text = "Navegador reabierto. Complete la autenticación.";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"No se pudo reabrir el navegador: {ex.Message}";
        }
    }

    private async void BtnPegarUrl_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PasteUrlDialog { Owner = this };
        if (dlg.ShowDialog() != true) return;

        var pasted = dlg.PastedText;
        if (string.IsNullOrWhiteSpace(pasted)) return;

        // Aceptar URL completa o solo el code
        string callbackUrl = pasted.Trim();
        if (!callbackUrl.StartsWith("firmasapp://", StringComparison.OrdinalIgnoreCase) &&
            !callbackUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !callbackUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            callbackUrl = $"firmasapp://callback?code={Uri.EscapeDataString(callbackUrl)}";
        }

        await HandleCallbackAsync(callbackUrl);
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        _cancelled = true;
        DialogResult = false;
        Close();
    }

    private void BtnDiagnostico_Click(object sender, RoutedEventArgs e)
    {
        var logPath = AppLog.LogFile;
        var protocolCmd = "(no disponible)";

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\firmasapp\shell\open\command");
            protocolCmd = key?.GetValue(null)?.ToString() ?? "(no registrado)";
        }
        catch { }

        MessageBox.Show(
            this,
            $"Información de diagnóstico:\n\n" +
            $"• Log de la app: {logPath}\n" +
            $"• Mecanismo activo: {_mechanism}\n" +
            $"• Comando registrado (HKCU): {protocolCmd}\n\n" +
            $"Si el callback no llega por el mecanismo automático:\n" +
            $"1. Use 'Pegar URL manualmente' (siempre funciona).\n" +
            $"2. Use 'Probar protocolo' para diagnosticar el custom protocol.\n" +
            $"3. Revise el log para ver el error exacto.",
            "Diagnóstico",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void BtnProbarProtocolo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "firmasapp://test",
                UseShellExecute = true
            });
            TxtStatus.Text = "Se abrió 'firmasapp://test' en el navegador. Si el custom protocol funciona, debería ver otra instancia de la app intentar abrirse.";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"No se pudo lanzar el protocolo: {ex.Message}";
            MessageBox.Show(
                this,
                $"No se pudo lanzar el protocolo personalizado.\n\n" +
                $"Error: {ex.Message}\n\n" +
                $"Esto usualmente significa que:\n" +
                $"• El protocolo no está registrado en Windows.\n" +
                $"• Windows bloqueó el lanzamiento (SmartScreen, AV, política).\n" +
                $"• El ejecutable registrado no existe o no se puede ejecutar.\n\n" +
                $"Recomendación: use el mecanismo HTTP (cambie RedirectUri a http://localhost:8080/callback).",
                "Protocolo no disponible",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
