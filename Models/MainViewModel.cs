using FirmasApp.Services;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace FirmasApp.Models;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly UsuarioService _usuarioService;
    private readonly KeycloakAuthService _keycloakAuth;
    private readonly ProtocolRegistrationService _protocolService;
    private List<Usuario> _usuarios = new();
    private List<Usuario> _usuariosFiltrados = new();
    private Usuario? _usuarioSeleccionado;
    private string _busqueda = string.Empty;
    private bool _isLoading;
    private string _statusMessage = "Listo";
    private bool _isConnected;
    private bool _isAuthenticated;
    private string _currentUserInfo = string.Empty;

    public MainViewModel(
        UsuarioService usuarioService,
        KeycloakAuthService keycloakAuth,
        ProtocolRegistrationService protocolService)
    {
        _usuarioService = usuarioService;
        _keycloakAuth = keycloakAuth;
        _protocolService = protocolService;

        LoginCommand = new RelayCommand(async () => await LoginAsync(), () => !IsLoading);
        LogoutCommand = new RelayCommand(async () => await LogoutAsync(), () => IsAuthenticated);
        LoadUsuariosCommand = new RelayCommand(async () => await LoadUsuariosAsync(), () => !IsLoading && IsAuthenticated);
        BuscarCommand = new RelayCommand(async () => await BuscarUsuariosAsync(), () => !IsLoading && IsAuthenticated);
        GestionarFirmaCommand = new RelayCommand(() => GestionarFirma(), () => !IsLoading && IsAuthenticated);
        SeleccionarUsuarioCommand = new RelayCommand(() => { }, () => UsuarioSeleccionado != null);
    }

    public event EventHandler<string>? CallbackUrlReceived;

    public void RaiseCallbackUrlReceived(string callbackUrl)
    {
        AppLog.Info("MainViewModel", $"Callback URL recibida: {callbackUrl}");
        CallbackUrlReceived?.Invoke(this, callbackUrl);
    }

    public async Task ProcessCallbackFromPipeAsync(string callbackUrl)
    {
        RaiseCallbackUrlReceived(callbackUrl);
    }

    public List<Usuario> Usuarios
    {
        get => _usuarios;
        set
        {
            _usuarios = value ?? new List<Usuario>();
            OnPropertyChanged(nameof(Usuarios));
            OnPropertyChanged(nameof(UsuariosConFirmaCount));
            OnPropertyChanged(nameof(UsuariosSinFirmaCount));
        }
    }

    public List<Usuario> UsuariosFiltrados
    {
        get => _usuariosFiltrados;
        set
        {
            _usuariosFiltrados = value ?? new List<Usuario>();
            OnPropertyChanged(nameof(UsuariosFiltrados));
            OnPropertyChanged(nameof(UsuariosConFirmaCount));
            OnPropertyChanged(nameof(UsuariosSinFirmaCount));
        }
    }

    public Usuario? UsuarioSeleccionado
    {
        get => _usuarioSeleccionado;
        set
        {
            _usuarioSeleccionado = value;
            OnPropertyChanged(nameof(UsuarioSeleccionado));
            ((RelayCommand)SeleccionarUsuarioCommand).RaiseCanExecuteChanged();
        }
    }

    public string Busqueda
    {
        get => _busqueda;
        set
        {
            _busqueda = value;
            OnPropertyChanged(nameof(Busqueda));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading));
            UpdateCommands();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            _isConnected = value;
            OnPropertyChanged(nameof(IsConnected));
        }
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set
        {
            _isAuthenticated = value;
            OnPropertyChanged(nameof(IsAuthenticated));
            UpdateCommands();
        }
    }

    public string CurrentUserInfo
    {
        get => _currentUserInfo;
        set
        {
            _currentUserInfo = value;
            OnPropertyChanged(nameof(CurrentUserInfo));
        }
    }

    public int UsuariosConFirmaCount => UsuariosFiltrados.Count(u => u.TieneFirma);

    public int UsuariosSinFirmaCount => UsuariosFiltrados.Count(u => !u.TieneFirma);

    public ICommand LoginCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand LoadUsuariosCommand { get; }
    public ICommand BuscarCommand { get; }
    public ICommand GestionarFirmaCommand { get; }
    public ICommand SeleccionarUsuarioCommand { get; }

    public async Task InitializeAsync(string[] args)
    {
        if (args != null && args.Length > 0)
        {
            var callbackUrl = args[0];
            AppLog.Info("MainViewModel", $"Argumentos recibidos: {callbackUrl}");

            if (!string.IsNullOrWhiteSpace(callbackUrl) &&
                callbackUrl.StartsWith("firmasapp://", StringComparison.OrdinalIgnoreCase))
            {
                await ProcessCallbackAsync(callbackUrl);
                return;
            }
        }

        CheckExistingSession();
    }

    public async Task<bool> ProcessCallbackAsync(string callbackUrl)
    {
        try
        {
            AppLog.Info("MainViewModel", $"Procesando callback: {callbackUrl}");
            IsLoading = true;
            StatusMessage = "Procesando autenticación...";

            var authCode = _protocolService.ExtractAuthCodeFromUrl(callbackUrl);
            if (string.IsNullOrWhiteSpace(authCode))
            {
                StatusMessage = "Error: No se pudo extraer el código del callback";
                return false;
            }

            var success = await _keycloakAuth.ProcessCallbackAsync(callbackUrl);

            if (success && _keycloakAuth.IsAuthenticated)
            {
                IsAuthenticated = true;
                var user = _keycloakAuth.CurrentUser;
                CurrentUserInfo = $"{user?.GivenName} {user?.FamilyName} ({user?.PreferredUsername})";
                StatusMessage = "¡Autenticación exitosa!";
                await LoadUsuariosAsync();
                return true;
            }

            StatusMessage = "Error en la autenticación";
            return false;
        }
        catch (Exception ex)
        {
            AppLog.Error("MainViewModel", $"Error procesando callback: {ex.Message}", ex);
            StatusMessage = $"Error: {ex.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CheckExistingSession()
    {
        if (_keycloakAuth.IsAuthenticated)
        {
            IsAuthenticated = true;
            var user = _keycloakAuth.CurrentUser;
            CurrentUserInfo = $"{user?.GivenName} {user?.FamilyName} ({user?.PreferredUsername})";
            StatusMessage = "Sesión activa detectada";
        }
        else
        {
            IsAuthenticated = false;
            StatusMessage = "Debe iniciar sesión para continuar";
        }
    }

    private async Task LoginAsync()
    {
        var options = App.ServiceProvider?.GetService(typeof(Microsoft.Extensions.Options.IOptions<KeycloakSettings>))
            as Microsoft.Extensions.Options.IOptions<KeycloakSettings>;
        var keycloakSettings = options?.Value ?? new KeycloakSettings();

        AppLog.Info("MainViewModel", $"LoginAsync iniciado. RedirectUri: {keycloakSettings.RedirectUri}");

        // Asegurar protocolo registrado (para el caso de fallback con browser externo)
        try
        {
            if (!_protocolService.IsProtocolRegistered())
            {
                _protocolService.RegisterProtocol();
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("MainViewModel", $"Pre-registro de protocolo: {ex.Message}");
        }

        var (authUrl, state) = _keycloakAuth.BuildAuthorizationUrl();
        AppLog.Info("MainViewModel", $"Auth URL generada (longitud: {authUrl.Length})");

        // 1) Intentar WebView2 (mecanismo más confiable)
        IsLoading = true;
        StatusMessage = "Abriendo autenticación...";

        var webDialog = new Views.WebLoginDialog(authUrl, keycloakSettings.RedirectUri);
        webDialog.Owner = Application.Current.MainWindow;

        var result = webDialog.ShowDialog();

        if (result == true && !string.IsNullOrWhiteSpace(webDialog.CallbackUrl))
        {
            AppLog.Info("MainViewModel", "WebView2 login completado");
            var ok = await ProcessCallbackAsync(webDialog.CallbackUrl);
            IsLoading = false;
            return;
        }

        if (webDialog.WasCancelled)
        {
            AppLog.Info("MainViewModel", "Usuario canceló el login");
            StatusMessage = "Login cancelado";
            IsLoading = false;
            return;
        }

        // 2) Fallback: browser externo + mecanismo pipe/HTTP
        AppLog.Warn("MainViewModel", "WebView2 no disponible o falló, usando browser externo");
        await LoginWithExternalBrowserAsync(keycloakSettings, authUrl);
    }

    private async Task LoginWithExternalBrowserAsync(KeycloakSettings keycloakSettings, string authUrl)
    {
        var scheme = GetScheme(keycloakSettings.RedirectUri);
        HttpCallbackService? httpService = null;
        var mechanism = scheme switch
        {
            "http" or "https" => $"HTTP local ({keycloakSettings.RedirectUri})",
            "firmasapp" => "Custom protocol (firmasapp://)",
            _ => $"Personalizado ({scheme}://)"
        };

        AppLog.Info("MainViewModel", $"Fallback - mecanismo: {mechanism}");

        if (scheme is "http" or "https")
        {
            httpService = new HttpCallbackService(keycloakSettings.RedirectUri);
            httpService.CallbackReceived += (_, url) => RaiseCallbackUrlReceived(url);
            httpService.ErrorOccurred += (_, err) => AppLog.Warn("MainViewModel", $"HTTP error: {err}");

            if (!httpService.Start())
            {
                StatusMessage = "No se pudo iniciar el listener HTTP local. Verifique que el puerto esté libre.";
                httpService.Dispose();
                IsLoading = false;
                return;
            }
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLog.Error("MainViewModel", $"No se pudo abrir el navegador: {ex.Message}", ex);
            StatusMessage = $"No se pudo abrir el navegador: {ex.Message}";
            httpService?.Dispose();
            IsLoading = false;
            return;
        }

        StatusMessage = "Esperando autenticación en el navegador...";

        var dialog = new Views.WaitingForAuthDialog(
            mechanism,
            authUrl,
            "Complete el login en el navegador. Si no vuelve solo, use 'Pegar URL manualmente'.",
            ProcessCallbackAsync,
            reopenBrowser: () =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });
            });

        EventHandler<string> onCallback = (_, url) =>
        {
            _ = dialog.NotifyCallbackAsync(url);
        };
        CallbackUrlReceived += onCallback;

        try
        {
            dialog.ShowDialog();
        }
        finally
        {
            CallbackUrlReceived -= onCallback;
            httpService?.Dispose();
        }

        if (dialog.ReceivedCallback != null)
        {
            StatusMessage = "¡Autenticación exitosa!";
        }
        else if (dialog.WasCancelled)
        {
            StatusMessage = "Login cancelado.";
        }

        IsLoading = false;
    }

    private static string GetScheme(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return string.Empty;
        var idx = uri.IndexOf("://", StringComparison.Ordinal);
        return idx > 0 ? uri[..idx].ToLowerInvariant() : string.Empty;
    }

    private async Task LogoutAsync()
    {
        try
        {
            IsLoading = true;

            _keycloakAuth.Logout();
            IsAuthenticated = false;
            CurrentUserInfo = string.Empty;
            Usuarios = new List<Usuario>();
            UsuariosFiltrados = new List<Usuario>();
            Busqueda = string.Empty;

            StatusMessage = "Sesión cerrada";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadUsuariosAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Cargando usuarios...";

            if (_keycloakAuth.IsTokenExpiringSoon())
            {
                await _keycloakAuth.RefreshTokenAsync();
            }

            var usuarios = await _usuarioService.GetUsuariosAsync();
            Usuarios = usuarios;
            UsuariosFiltrados = usuarios;

            StatusMessage = $"Se cargaron {Usuarios.Count} usuarios ({UsuariosConFirmaCount} con firma, {UsuariosSinFirmaCount} sin firma)";
        }
        catch (Exception ex)
        {
            AppLog.Error("MainViewModel", $"Error en LoadUsuariosAsync: {ex.Message}", ex);
            StatusMessage = $"Error: {ex.Message}";
            UsuariosFiltrados = new List<Usuario>();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task BuscarUsuariosAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Buscando...";

            if (string.IsNullOrWhiteSpace(Busqueda))
            {
                UsuariosFiltrados = Usuarios;
            }
            else
            {
                var usuarios = await _usuarioService.GetUsuariosAsync(Busqueda);
                UsuariosFiltrados = usuarios;
            }

            StatusMessage = $"{UsuariosFiltrados.Count} usuarios encontrados ({UsuariosConFirmaCount} con firma, {UsuariosSinFirmaCount} sin firma)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void GestionarFirma()
    {
        try
        {
            var gestorFirma = App.ServiceProvider?.GetService(typeof(FirmasApp.ViewModels.FirmaViewModel)) as FirmasApp.ViewModels.FirmaViewModel
                ?? throw new InvalidOperationException("No se pudo resolver FirmaViewModel");

            gestorFirma.UsuarioActual = null;
            gestorFirma.FirmaDataUrl = null;

            var gestionFirmaView = new Views.GestionFirmaView(gestorFirma);

            if (!string.IsNullOrWhiteSpace(CurrentUserInfo))
            {
                var match = System.Text.RegularExpressions.Regex.Match(CurrentUserInfo, @"\(([^)]+)\)");
                if (match.Success)
                {
                    var username = match.Groups[1].Value;
                    _ = gestionFirmaView.EstablecerUsuarioAsync(username);
                }
            }

            gestionFirmaView.ShowDialog();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error abriendo gestión de firmas: {ex.Message}";
        }
    }

    private void UpdateCommands()
    {
        ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
        ((RelayCommand)LogoutCommand).RaiseCanExecuteChanged();
        ((RelayCommand)LoadUsuariosCommand).RaiseCanExecuteChanged();
        ((RelayCommand)BuscarCommand).RaiseCanExecuteChanged();
        ((RelayCommand)GestionarFirmaCommand).RaiseCanExecuteChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
