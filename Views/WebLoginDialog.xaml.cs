using System.ComponentModel;
using System.Windows;
using FirmasApp.Services;
using Microsoft.Web.WebView2.Core;

namespace FirmasApp.Views;

public partial class WebLoginDialog : Window
{
    private readonly string _authUrl;
    private readonly string _redirectUriMatch;
    private bool _navigating;
    private bool _isClosing;

    /// <summary>URL completa del callback cuando el login termina (con ?code=...&state=...).</summary>
    public string? CallbackUrl { get; private set; }

    /// <summary>True si el usuario canceló manualmente.</summary>
    public bool WasCancelled { get; private set; }

    public WebLoginDialog(string authUrl, string redirectUriMatch)
    {
        InitializeComponent();

        _authUrl = authUrl;
        _redirectUriMatch = redirectUriMatch.TrimEnd('/');

        Loaded += OnLoaded;
        Closing += OnClosingHandler;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            TxtStatus.Text = "Inicializando navegador embebido...";

            var userDataFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FirmasApp", "WebView2");
            AppLog.Info("WebLogin", $"UserData folder: {userDataFolder}");
            System.IO.Directory.CreateDirectory(userDataFolder);

            // Inicializar WebView2 con runtime del sistema y userData folder específico
            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(
                null, userDataFolder, null);

            await WebView.EnsureCoreWebView2Async(env);

            WebView.CoreWebView2.NewWindowRequested += (s, args) =>
            {
                args.Handled = true;
                WebView.CoreWebView2.Navigate(args.Uri);
            };

            WebView.NavigationStarting += WebView_NavigationStarting;
            WebView.CoreWebView2.NavigationCompleted += (s, args) =>
            {
                TxtStatus.Text = args.IsSuccess
                    ? "Navegador listo. Complete el login."
                    : $"Error de navegación: {args.WebErrorStatus}";
            };

            TxtStatus.Text = "Cargando Keycloak...";
            WebView.CoreWebView2.Navigate(_authUrl);
        }
        catch (Exception ex)
        {
            AppLog.Error("WebLogin", $"No se pudo inicializar WebView2: {ex.Message}", ex);
            TxtStatus.Text = $"Error: {ex.Message}";
            MessageBox.Show(this,
                $"No se pudo inicializar el navegador embebido.\n\n" +
                $"Detalles: {ex.Message}\n\n" +
                $"La app usará el mecanismo alternativo (browser externo).",
                "WebView2 no disponible",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            DialogResult = false;
            Close();
        }
    }

    private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (_navigating || _isClosing) return;

        var uri = e.Uri ?? string.Empty;
        AppLog.Debug("WebLogin", $"Navegando a: {uri}");

        // Detectar el redirect_uri
        // Caso 1: HTTP/HTTPS (http://localhost:8080/callback?code=...)
        // Caso 2: Custom protocol (firmasapp://callback?code=...)
        try
        {
            var parsed = new Uri(uri);

            // Comparar scheme + host + path
            bool isMatch = IsRedirectMatch(parsed);

            if (isMatch)
            {
                AppLog.Info("WebLogin", $"Detectado redirect_uri en navegación: {uri}");
                _navigating = true;
                e.Cancel = true;
                CallbackUrl = uri;
                DialogResult = true;
                Close();
            }
        }
        catch
        {
            // Ignorar URIs que no se pueden parsear
        }
    }

    private bool IsRedirectMatch(Uri uri)
    {
        // Parsear el redirect_uri configurado
        Uri expected;
        try { expected = new Uri(_redirectUriMatch); }
        catch { return false; }

        // Para HTTP: scheme + host + port + path deben coincidir
        if (expected.Scheme == Uri.UriSchemeHttp || expected.Scheme == Uri.UriSchemeHttps)
        {
            return string.Equals(uri.Scheme, expected.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(uri.Host, expected.Host, StringComparison.OrdinalIgnoreCase)
                && uri.Port == expected.Port
                && string.Equals(uri.AbsolutePath.TrimEnd('/'), expected.AbsolutePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        // Para custom protocol: comparar el scheme completo
        // firmasapp://callback?code=...  vs  firmasapp://callback?code=...
        if (uri.AbsoluteUri.StartsWith(_redirectUriMatch, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Para firmasapp://, también aceptar cualquier URL con ese scheme
        if (string.Equals(uri.Scheme, expected.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private void BtnReintentar_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TxtStatus.Text = "Reintentando...";
            WebView.CoreWebView2?.Navigate(_authUrl);
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error: {ex.Message}";
        }
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        WasCancelled = true;
        DialogResult = false;
        Close();
    }

    private void OnClosingHandler(object? sender, CancelEventArgs e)
    {
        _isClosing = true;
    }
}
