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
    private readonly SessionManagerService _sessionManager;
    private List<Usuario> _usuarios = new();
    private List<Usuario> _usuariosFiltrados = new();
    private Usuario? _usuarioSeleccionado;
    private string _busqueda = string.Empty;
    private bool _isLoading;
    private string _statusMessage = "Listo";
    private bool _isConnected;
    private bool _isAuthenticated;
    private string _currentUserInfo = string.Empty;
    private bool _sessionExpiringSoon;
    private int _currentPage = 0;
    private int _pageSize = 50;
    private int _totalPages = 0;
    private int _totalElements = 0;

    public MainViewModel(
        UsuarioService usuarioService,
        KeycloakAuthService keycloakAuth,
        ProtocolRegistrationService protocolService,
        SessionManagerService sessionManager)
    {
        _usuarioService = usuarioService;
        _keycloakAuth = keycloakAuth;
        _protocolService = protocolService;
        _sessionManager = sessionManager;

        // Suscribir a eventos del gestor de sesión
        _sessionManager.SessionExpiringSoon += OnSessionExpiringSoon;
        _sessionManager.SessionRefreshed += OnSessionRefreshed;
        _sessionManager.SessionExpired += OnSessionExpired;

        LoginCommand = new RelayCommand(async () => await LoginAsync(), () => !IsLoading);
        LogoutCommand = new RelayCommand(async () => await LogoutAsync(), () => IsAuthenticated);
        LoadUsuariosCommand = new RelayCommand(async () => await LoadUsuariosAsync(), () => !IsLoading && IsAuthenticated);
        BuscarCommand = new RelayCommand(async () => await BuscarUsuariosAsync(), () => !IsLoading && IsAuthenticated);
        SeleccionarUsuarioCommand = new RelayCommand(() => { }, () => UsuarioSeleccionado != null);
        FirstPageCommand = new RelayCommand(async () => await GoToFirstPageAsync(), () => CanGoToPreviousPage());
        PreviousPageCommand = new RelayCommand(async () => await GoToPreviousPageAsync(), () => CanGoToPreviousPage());
        NextPageCommand = new RelayCommand(async () => await GoToNextPageAsync(), () => CanGoToNextPage());
        LastPageCommand = new RelayCommand(async () => await GoToLastPageAsync(), () => CanGoToNextPage());
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

    public bool SessionExpiringSoon
    {
        get => _sessionExpiringSoon;
        set
        {
            _sessionExpiringSoon = value;
            OnPropertyChanged(nameof(SessionExpiringSoon));
        }
    }

    public int UsuariosConFirmaCount => UsuariosFiltrados.Count(u => u.TieneFirma);

    public int UsuariosSinFirmaCount => UsuariosFiltrados.Count(u => !u.TieneFirma);

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            _currentPage = value;
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(PageInfo));
            UpdatePaginationCommands();
        }
    }

    public int PageSize
    {
        get => _pageSize;
        set
        {
            _pageSize = value;
            OnPropertyChanged(nameof(PageSize));
            OnPropertyChanged(nameof(PageInfo));
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        set
        {
            _totalPages = value;
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(PageInfo));
            UpdatePaginationCommands();
        }
    }

    public int TotalElements
    {
        get => _totalElements;
        set
        {
            _totalElements = value;
            OnPropertyChanged(nameof(TotalElements));
            OnPropertyChanged(nameof(PageInfo));
        }
    }

    public string PageInfo => TotalPages > 0
        ? $"Página {CurrentPage + 1} de {TotalPages} ({TotalElements} total)"
        : "Sin resultados";

    #region Manejadores de eventos de sesión

    private void OnSessionExpiringSoon(object? sender, bool isExpiring)
    {
        SessionExpiringSoon = isExpiring;

        if (isExpiring)
        {
            StatusMessage = "⚠️ Tu sesión está por expirar";
            AppLog.Warn("MainViewModel", "Sesión por expirar");
        }
        else
        {
            StatusMessage = "Sesión refrescada";
            AppLog.Info("MainViewModel", "Sesión refrescada");
        }
    }

    private void OnSessionRefreshed(object? sender, EventArgs e)
    {
        StatusMessage = "✅ Sesión refrescada automáticamente";
        SessionExpiringSoon = false;
        AppLog.Info("MainViewModel", "Token refrescado exitosamente");
    }

    private void OnSessionExpired(object? sender, EventArgs e)
    {
        IsAuthenticated = false;
        SessionExpiringSoon = false;
        CurrentUserInfo = string.Empty;
        Usuarios = new List<Usuario>();
        UsuariosFiltrados = new List<Usuario>();
        Busqueda = string.Empty;

        StatusMessage = "⏰ Tu sesión ha expirado";
        AppLog.Warn("MainViewModel", "Sesión expirada");

        Application.Current?.Dispatcher.Invoke(() =>
        {
            MessageBox.Show(
                "Tu sesión ha expirado. Por favor, inicia sesión nuevamente.",
                "Sesión Expirada",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });
    }

    #endregion

    public ICommand LoginCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand LoadUsuariosCommand { get; }
    public ICommand BuscarCommand { get; }
    public ICommand SeleccionarUsuarioCommand { get; }
    public ICommand FirstPageCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand LastPageCommand { get; }

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

                // Iniciar monitoreo de sesión
                _sessionManager.StartSessionMonitoring(Application.Current?.MainWindow);

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

            // Iniciar monitoreo de sesión
            _sessionManager.StartSessionMonitoring(Application.Current?.MainWindow);
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

            // Detener monitoreo de sesión
            _sessionManager.StopSessionMonitoring();

            _keycloakAuth.Logout();
            IsAuthenticated = false;
            SessionExpiringSoon = false;
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

            var resultado = await _usuarioService.GetUsuariosAsync(busqueda: null, page: CurrentPage, size: PageSize);
            Usuarios = resultado.Content;
            UsuariosFiltrados = resultado.Content;
            TotalPages = resultado.TotalPages;
            TotalElements = resultado.TotalElements;

            StatusMessage = $"Página {CurrentPage + 1} de {TotalPages} - {UsuariosFiltrados.Count} usuarios ({UsuariosConFirmaCount} con firma, {UsuariosSinFirmaCount} sin firma)";
        }
        catch (Exception ex)
        {
            AppLog.Error("MainViewModel", $"Error en LoadUsuariosAsync: {ex.Message}", ex);
            StatusMessage = $"Error: {ex.Message}";
            UsuariosFiltrados = new List<Usuario>();
            TotalPages = 0;
            TotalElements = 0;
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
                // Resetear a primera página si no hay búsqueda
                CurrentPage = 0;
                await LoadUsuariosAsync();
                return;
            }
            else
            {
                // Si hay búsqueda, resetear a primera página
                CurrentPage = 0;
                var resultado = await _usuarioService.GetUsuariosAsync(Busqueda, CurrentPage, PageSize);
                UsuariosFiltrados = resultado.Content;
                TotalPages = resultado.TotalPages;
                TotalElements = resultado.TotalElements;
            }

            StatusMessage = $"Página {CurrentPage + 1} de {TotalPages} - {UsuariosFiltrados.Count} usuarios encontrados ({UsuariosConFirmaCount} con firma, {UsuariosSinFirmaCount} sin firma)";
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

    private bool CanGoToPreviousPage() => !IsLoading && IsAuthenticated && CurrentPage > 0;
    private bool CanGoToNextPage() => !IsLoading && IsAuthenticated && CurrentPage < TotalPages - 1;

    private async Task GoToFirstPageAsync()
    {
        if (CurrentPage == 0) return;
        CurrentPage = 0;
        await LoadPageAsync();
    }

    private async Task GoToPreviousPageAsync()
    {
        if (CurrentPage == 0) return;
        CurrentPage--;
        await LoadPageAsync();
    }

    private async Task GoToNextPageAsync()
    {
        if (CurrentPage >= TotalPages - 1) return;
        CurrentPage++;
        await LoadPageAsync();
    }

    private async Task GoToLastPageAsync()
    {
        if (TotalPages == 0) return;
        CurrentPage = TotalPages - 1;
        await LoadPageAsync();
    }

    private async Task LoadPageAsync()
    {
        if (string.IsNullOrWhiteSpace(Busqueda))
        {
            await LoadUsuariosAsync();
        }
        else
        {
            await BuscarUsuariosAsync();
        }
    }

    private void UpdatePaginationCommands()
    {
        ((RelayCommand)FirstPageCommand).RaiseCanExecuteChanged();
        ((RelayCommand)PreviousPageCommand).RaiseCanExecuteChanged();
        ((RelayCommand)NextPageCommand).RaiseCanExecuteChanged();
        ((RelayCommand)LastPageCommand).RaiseCanExecuteChanged();
    }

    private void UpdateCommands()
    {
        ((RelayCommand)LoginCommand).RaiseCanExecuteChanged();
        ((RelayCommand)LogoutCommand).RaiseCanExecuteChanged();
        ((RelayCommand)LoadUsuariosCommand).RaiseCanExecuteChanged();
        ((RelayCommand)BuscarCommand).RaiseCanExecuteChanged();
        UpdatePaginationCommands();
    }

    public void UpdateKeycloakSettings(KeycloakSettings newSettings)
    {
        try
        {
            AppLog.Info("MainViewModel", $"Actualizando configuración de Keycloak: {newSettings.Url}");
            _keycloakAuth.UpdateSettings(newSettings);
            StatusMessage = "Configuración de Keycloak actualizada exitosamente";
        }
        catch (Exception ex)
        {
            AppLog.Error("MainViewModel", $"Error actualizando configuración: {ex.Message}", ex);
            StatusMessage = $"Error actualizando configuración: {ex.Message}";
        }
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
