using FirmasApp.Models;
using FirmasApp.Services;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

public class KeycloakAuthService
{
    private readonly KeycloakSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private TokenResponse? _currentToken;
    private UserInfo? _currentUser;
    private string? _expectedState;

    public KeycloakAuthService(KeycloakSettings settings)
    {
        _settings = settings;
        _httpClient = new HttpClient { BaseAddress = new Uri(settings.Url) };
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public bool IsAuthenticated => _currentToken != null && !_currentToken.IsExpired;
    public UserInfo? CurrentUser => _currentUser;
    public string? ExpectedState => _expectedState;

    /// <summary>
    /// Genera la URL de autorización de Keycloak y guarda el state para validación CSRF.
    /// </summary>
    public (string Url, string State) BuildAuthorizationUrl()
    {
        var state = Guid.NewGuid().ToString("N");
        _expectedState = state;

        var authUrl = $"{_settings.Url}/realms/{_settings.Realm}{_settings.AuthorizationEndpoint}?" +
            $"client_id={Uri.EscapeDataString(_settings.ClientId)}&" +
            $"redirect_uri={Uri.EscapeDataString(_settings.RedirectUri)}&" +
            $"response_type=code&" +
            $"scope={Uri.EscapeDataString(_settings.Scope)}&" +
            $"state={state}";

        return (authUrl, state);
    }

    /// <summary>
    /// Abre el navegador del sistema con la URL de autorización
    /// </summary>
    public void OpenBrowserForLogin()
    {
        var (authUrl, _) = BuildAuthorizationUrl();
        AppLog.Info("Keycloak", $"Abriendo navegador: {authUrl}");
        Process.Start(new ProcessStartInfo
        {
            FileName = authUrl,
            UseShellExecute = true
        });
    }

    /// <summary>
    /// Procesa el callback del navegador y obtiene el token
    /// </summary>
    /// <param name="callbackUrl">URL completa de callback firmasapp://callback?code=xxx&state=yyy</param>
    public async Task<bool> ProcessCallbackAsync(string callbackUrl)
    {
        try
        {
            var uri = new Uri(callbackUrl);
            var query = QueryHelpers.ParseQuery(uri.Query);

            var code = query.TryGetValue("code", out var codeValue) ? codeValue.ToString() : null;
            var state = query.TryGetValue("state", out var stateValue) ? stateValue.ToString() : null;
            var error = query.TryGetValue("error", out var errorValue) ? errorValue.ToString() : null;

            if (!string.IsNullOrWhiteSpace(error))
            {
                throw new Exception($"Error en autenticación: {error}");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new Exception("No se recibió código de autorización");
            }

            if (!string.IsNullOrWhiteSpace(_expectedState) &&
                !string.Equals(state, _expectedState, StringComparison.Ordinal))
            {
                throw new Exception("State inválido (posible CSRF). Autenticación rechazada.");
            }

            _expectedState = null;

            return await ExchangeCodeForTokenAsync(code);
        }
        catch (Exception ex)
        {
            AppLog.Error("Keycloak", $"Error procesando callback: {ex.Message}", ex);
            throw new Exception($"Error procesando callback: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Intercambia el código de autorización por un token de acceso
    /// </summary>
    private async Task<bool> ExchangeCodeForTokenAsync(string code)
    {
        try
        {
            var tokenUrl = $"{_settings.Url}/realms/{_settings.Realm}{_settings.TokenEndpoint}";
            AppLog.Debug("Keycloak", $"Token URL: {tokenUrl}");

            var parameters = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "client_id", _settings.ClientId },
                { "code", code },
                { "redirect_uri", _settings.RedirectUri }
            };

            if (!string.IsNullOrWhiteSpace(_settings.ClientSecret))
            {
                parameters["client_secret"] = _settings.ClientSecret;
                AppLog.Debug("Keycloak", "Using client_secret");
            }

            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync(tokenUrl, content);

            AppLog.Debug("Keycloak", $"Response status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                AppLog.Error("Keycloak", $"Error response: {response.StatusCode} - {errorContent}");
                throw new Exception($"Error obteniendo token: {response.StatusCode} - {errorContent}");
            }

            var tokenContent = await response.Content.ReadAsStringAsync();
            _currentToken = JsonSerializer.Deserialize<TokenResponse>(tokenContent, _jsonOptions);

            if (_currentToken == null || string.IsNullOrWhiteSpace(_currentToken.AccessToken))
            {
                throw new Exception("Token inválido recibido");
            }

            AppLog.Info("Keycloak", "Token recibido exitosamente");

            await LoadUserInfoAsync();
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("Keycloak", $"Excepción en ExchangeCodeForToken: {ex.Message}", ex);
            throw new Exception($"Error intercambiando código por token: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Obtiene el perfil del usuario usando el token de acceso
    /// </summary>
    private async Task LoadUserInfoAsync()
    {
        if (_currentToken == null) return;

        try
        {
            var userInfoUrl = $"{_settings.Url}/realms/{_settings.Realm}/protocol/openid-connect/userinfo";

            var request = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
            request.Headers.Add("Authorization", $"Bearer {_currentToken.AccessToken}");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var userInfoContent = await response.Content.ReadAsStringAsync();
                _currentUser = JsonSerializer.Deserialize<UserInfo>(userInfoContent, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Keycloak", $"No se pudo obtener userInfo: {ex.Message}");
        }
    }

    /// <summary>
    /// Refresca el token usando el refresh token
    /// </summary>
    public async Task<bool> RefreshTokenAsync()
    {
        if (_currentToken == null || string.IsNullOrWhiteSpace(_currentToken.RefreshToken))
        {
            return false;
        }

        try
        {
            var tokenUrl = $"{_settings.Url}/realms/{_settings.Realm}{_settings.TokenEndpoint}";

            var parameters = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "client_id", _settings.ClientId },
                { "refresh_token", _currentToken.RefreshToken }
            };

            if (!string.IsNullOrWhiteSpace(_settings.ClientSecret))
            {
                parameters["client_secret"] = _settings.ClientSecret;
            }

            var content = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync(tokenUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                Logout();
                return false;
            }

            var tokenContent = await response.Content.ReadAsStringAsync();
            var newToken = JsonSerializer.Deserialize<TokenResponse>(tokenContent, _jsonOptions);

            if (newToken != null && !string.IsNullOrWhiteSpace(newToken.AccessToken))
            {
                _currentToken = newToken;
                await LoadUserInfoAsync();
                return true;
            }

            return false;
        }
        catch (Exception)
        {
            Logout();
            return false;
        }
    }

    /// <summary>
    /// Obtiene el token de acceso actual, refrescándolo si es necesario
    /// </summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        if (_currentToken == null)
        {
            return null;
        }

        if (_currentToken.IsExpired)
        {
            var refreshed = await RefreshTokenAsync();
            if (!refreshed)
            {
                return null;
            }
        }

        return _currentToken.AccessToken;
    }

    /// <summary>
    /// Cierra la sesión y limpia los tokens
    /// </summary>
    public void Logout()
    {
        _currentToken = null;
        _currentUser = null;
        _expectedState = null;
    }

    /// <summary>
    /// Verifica si el token actual está próximo a expirar (menos de 5 minutos)
    /// </summary>
    public bool IsTokenExpiringSoon()
    {
        if (_currentToken == null) return true;
        return DateTime.UtcNow.AddMinutes(5) >= _currentToken.ExpiresAt;
    }

    /// <summary>
    /// Actualiza la configuración de Keycloak y reinicia la sesión
    /// </summary>
    public void UpdateSettings(KeycloakSettings newSettings)
    {
        AppLog.Info("KeycloakAuthService", $"Actualizando configuración. Nueva URL: {newSettings.Url}");

        // Limpiar sesión actual
        Logout();

        // Actualizar configuración
        _settings.Url = newSettings.Url;
        _settings.Realm = newSettings.Realm;
        _settings.ClientId = newSettings.ClientId;
        _settings.ClientSecret = newSettings.ClientSecret;
        _settings.RedirectUri = newSettings.RedirectUri;
        _settings.AuthorizationEndpoint = newSettings.AuthorizationEndpoint;
        _settings.TokenEndpoint = newSettings.TokenEndpoint;
        _settings.Scope = newSettings.Scope;

        // Actualizar HttpClient con nueva URL base
        _httpClient.BaseAddress = new Uri(newSettings.Url);

        AppLog.Info("KeycloakAuthService", "Configuración actualizada exitosamente");
    }
}
