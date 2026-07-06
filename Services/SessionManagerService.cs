using System.Windows;
using System.Windows.Threading;
using FirmasApp.Models;

namespace FirmasApp.Services;

/// <summary>
/// Servicio para gestionar la renovación automática de tokens y notificaciones de sesión
/// </summary>
public class SessionManagerService
{
    private readonly KeycloakAuthService _authService;
    private readonly DispatcherTimer _checkTimer;
    private readonly DispatcherTimer _warningTimer;
    private bool _hasShownWarning;
    private bool _hasShownFinalWarning;
    private Window? _ownerWindow;

    // Eventos para notificar cambios de estado
    public event EventHandler<bool>? SessionExpiringSoon;
    public event EventHandler? SessionRefreshed;
    public event EventHandler? SessionExpired;

    public SessionManagerService(KeycloakAuthService authService)
    {
        _authService = authService;

        // Timer para verificar estado del token (cada 30 segundos)
        _checkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _checkTimer.Tick += CheckTokenStatus;

        // Timer para mostrar advertencia final (cada minuto cuando está por expirar)
        _warningTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _warningTimer.Tick += ShowExpirationWarning;
    }

    /// <summary>
    /// Inicia el monitoreo de la sesión
    /// </summary>
    public void StartSessionMonitoring(Window? ownerWindow = null)
    {
        _ownerWindow = ownerWindow;
        _hasShownWarning = false;
        _hasShownFinalWarning = false;

        if (_authService.IsAuthenticated)
        {
            _checkTimer.Start();
            AppLog.Info("SessionManager", "Monitoreo de sesión iniciado");
        }
    }

    /// <summary>
    /// Detiene el monitoreo de la sesión
    /// </summary>
    public void StopSessionMonitoring()
    {
        _checkTimer.Stop();
        _warningTimer.Stop();
        _hasShownWarning = false;
        _hasShownFinalWarning = false;

        AppLog.Info("SessionManager", "Monitoreo de sesión detenido");
    }

    /// <summary>
    /// Verifica el estado del token y toma acciones apropiadas
    /// </summary>
    private async void CheckTokenStatus(object? sender, EventArgs e)
    {
        if (!_authService.IsAuthenticated)
        {
            StopSessionMonitoring();
            SessionExpired?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Verificar si el token está expirado o por expirar
        var isExpiringSoon = _authService.IsTokenExpiringSoon();

        if (isExpiringSoon && !_hasShownFinalWarning)
        {
            // Intentar refrescar el token automáticamente
            var refreshed = await _authService.RefreshTokenAsync();

            if (refreshed)
            {
                AppLog.Info("SessionManager", "Token refrescado exitosamente");
                SessionRefreshed?.Invoke(this, EventArgs.Empty);
                _hasShownWarning = false;
                _hasShownFinalWarning = false;
                _warningTimer.Stop();
            }
            else
            {
                AppLog.Warn("SessionManager", "No se pudo refrescar el token");
                _hasShownFinalWarning = true;

                // Notificar que la sesión está por expirar
                SessionExpiringSoon?.Invoke(this, true);

                // Iniciar timer de advertencia final
                if (!_warningTimer.IsEnabled)
                {
                    _warningTimer.Start();
                }
            }
        }
        else if (!isExpiringSoon && _hasShownWarning)
        {
            // Token refrescado, resetear estados
            _hasShownWarning = false;
            _hasShownFinalWarning = false;
            _warningTimer.Stop();
            SessionExpiringSoon?.Invoke(this, false);
        }
    }

    /// <summary>
    /// Muestra advertencia de expiración de sesión
    /// </summary>
    private void ShowExpirationWarning(object? sender, EventArgs e)
    {
        if (_hasShownFinalWarning && _authService.IsAuthenticated)
        {
            _ownerWindow?.Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    "⚠️ Tu sesión está por expirar.\n\n" +
                    "La sesión se cerrará automáticamente en pocos minutos.\n\n" +
                    "¿Deseas refrescar la sesión ahora?",
                    "Sesión por Expirar",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _ = RefreshSessionManually();
                }
                else
                {
                    // Usuario rechazó refrescar, cerrar sesión
                    _authService.Logout();
                    StopSessionMonitoring();
                    SessionExpired?.Invoke(this, EventArgs.Empty);
                }
            });
        }
    }

    /// <summary>
    /// Refresca la sesión manualmente (solicitado por usuario)
    /// </summary>
    private async Task<bool> RefreshSessionManually()
    {
        var refreshed = await _authService.RefreshTokenAsync();

        if (refreshed)
        {
            AppLog.Info("SessionManager", "Sesión refrescada manualmente");
            SessionRefreshed?.Invoke(this, EventArgs.Empty);
            _hasShownWarning = false;
            _hasShownFinalWarning = false;
            _warningTimer.Stop();

            _ownerWindow?.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    "✅ Sesión refrescada exitosamente.",
                    "Sesión Refrescada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });

            return true;
        }
        else
        {
            AppLog.Warn("SessionManager", "No se pudo refrescar la sesión manualmente");

            _ownerWindow?.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    "❌ No se pudo refrescar la sesión.\n\n" +
                    "Por favor, inicia sesión nuevamente.",
                    "Error de Sesión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                _authService.Logout();
                StopSessionMonitoring();
                SessionExpired?.Invoke(this, EventArgs.Empty);
            });

            return false;
        }
    }

    /// <summary>
    /// Obtiene el tiempo restante de la sesión en minutos
    /// </summary>
    public int GetSessionTimeRemaining()
    {
        // Este método requiere acceso al token interno
        // Por ahora retornamos un valor estimado
        return _authService.IsTokenExpiringSoon() ? 5 : 30;
    }

    /// <summary>
    /// Verifica si la sesión está activa
    /// </summary>
    public bool IsSessionActive => _authService.IsAuthenticated;

    /// <summary>
    /// Verifica si la sesión está por expirar
    /// </summary>
    public bool IsSessionExpiring => _authService.IsTokenExpiringSoon();
}