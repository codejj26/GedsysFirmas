using System;
using System.Windows;
using FirmasApp.Models;

namespace FirmasApp.Views;

/// <summary>
/// Diálogo de configuración general con pestañas para diferentes servicios
/// </summary>
public partial class GeneralSettingsDialog : Window
{
    private readonly KeycloakSettings _originalKeycloakSettings;
    private readonly GedsysApiSettings _originalApiSettings;
    private readonly SupabaseSettings _originalSupabaseSettings;

    public KeycloakSettings? SavedKeycloakSettings { get; private set; }
    public GedsysApiSettings? SavedApiSettings { get; private set; }
    public SupabaseSettings? SavedSupabaseSettings { get; private set; }

    public GeneralSettingsDialog(
        KeycloakSettings keycloakSettings,
        GedsysApiSettings apiSettings,
        SupabaseSettings supabaseSettings)
    {
        InitializeComponent();

        // Clonar configuraciones originales
        _originalKeycloakSettings = CloneKeycloakSettings(keycloakSettings);
        _originalApiSettings = CloneApiSettings(apiSettings);
        _originalSupabaseSettings = CloneSupabaseSettings(supabaseSettings);

        LoadSettingsToUI();
    }

    #region Métodos de Clonación (evitar mutaciones no deseadas)

    private KeycloakSettings CloneKeycloakSettings(KeycloakSettings settings)
    {
        return new KeycloakSettings
        {
            Url = settings.Url,
            Realm = settings.Realm,
            ClientId = settings.ClientId,
            ClientSecret = settings.ClientSecret,
            RedirectUri = settings.RedirectUri,
            AuthorizationEndpoint = settings.AuthorizationEndpoint,
            TokenEndpoint = settings.TokenEndpoint,
            Scope = settings.Scope
        };
    }

    private GedsysApiSettings CloneApiSettings(GedsysApiSettings settings)
    {
        return new GedsysApiSettings
        {
            BaseUrl = settings.BaseUrl,
            AuthToken = settings.AuthToken,
            TimeoutSeconds = settings.TimeoutSeconds
        };
    }

    private SupabaseSettings CloneSupabaseSettings(SupabaseSettings settings)
    {
        return new SupabaseSettings
        {
            Host = settings.Host,
            Port = settings.Port,
            Database = settings.Database,
            Username = settings.Username,
            Password = settings.Password,
            Enabled = settings.Enabled
        };
    }

    #endregion

    #region Cargar Configuraciones a la UI

    private void LoadSettingsToUI()
    {
        LoadKeycloakSettings();
        LoadApiSettings();
        LoadSupabaseSettings();
    }

    private void LoadKeycloakSettings()
    {
        TxtKeycloakUrl.Text = _originalKeycloakSettings.Url;
        TxtRealm.Text = _originalKeycloakSettings.Realm;
        TxtClientId.Text = _originalKeycloakSettings.ClientId;
        TxtRedirectUri.Text = _originalKeycloakSettings.RedirectUri;

        // Mostrar configuración actual
        TxtCurrentKeycloakUrl.Text = _originalKeycloakSettings.Url;
        TxtCurrentRealm.Text = _originalKeycloakSettings.Realm;
        TxtCurrentClientId.Text = _originalKeycloakSettings.ClientId;
    }

    private void LoadApiSettings()
    {
        TxtApiBaseUrl.Text = _originalApiSettings.BaseUrl;
        TxtApiTimeout.Text = _originalApiSettings.TimeoutSeconds.ToString();
        TxtApiToken.Text = _originalApiSettings.AuthToken ?? string.Empty;

        // Mostrar configuración actual
        TxtCurrentApiBaseUrl.Text = _originalApiSettings.BaseUrl;
        TxtCurrentApiTimeout.Text = _originalApiSettings.TimeoutSeconds.ToString();
    }

    private void LoadSupabaseSettings()
    {
        ChkSupabaseEnabled.IsChecked = _originalSupabaseSettings.Enabled;
        TxtSupabaseHost.Text = _originalSupabaseSettings.Host ?? string.Empty;
        TxtSupabasePort.Text = _originalSupabaseSettings.Port.ToString();
        TxtSupabaseDatabase.Text = _originalSupabaseSettings.Database ?? string.Empty;
        TxtSupabaseUsername.Text = _originalSupabaseSettings.Username ?? string.Empty;
        TxtSupabasePassword.Password = _originalSupabaseSettings.Password ?? string.Empty;

        // Mostrar configuración actual
        TxtCurrentSupabaseHost.Text = _originalSupabaseSettings.Host ?? "No configurado";
        TxtCurrentSupabasePort.Text = _originalSupabaseSettings.Port.ToString();
        TxtCurrentSupabaseStatus.Text = _originalSupabaseSettings.Enabled ? "Habilitado" : "Deshabilitado";
    }

    #endregion

    #region Validaciones

    private bool ValidateAllSettings()
    {
        return ValidateKeycloakSettings() &&
               ValidateApiSettings() &&
               ValidateSupabaseSettings();
    }

    private bool ValidateKeycloakSettings()
    {
        if (string.IsNullOrWhiteSpace(TxtKeycloakUrl.Text))
        {
            ShowValidationError("La URL de Keycloak es obligatoria");
            return false;
        }

        if (!Uri.TryCreate(TxtKeycloakUrl.Text, UriKind.Absolute, out _))
        {
            ShowValidationError("La URL de Keycloak no es válida. Debe incluir el protocolo (https://)");
            return false;
        }

        if (string.IsNullOrWhiteSpace(TxtRealm.Text))
        {
            ShowValidationError("El Realm de Keycloak es obligatorio");
            return false;
        }

        if (string.IsNullOrWhiteSpace(TxtClientId.Text))
        {
            ShowValidationError("El Client ID de Keycloak es obligatorio");
            return false;
        }

        if (string.IsNullOrWhiteSpace(TxtRedirectUri.Text))
        {
            ShowValidationError("El Redirect URI de Keycloak es obligatorio");
            return false;
        }

        return true;
    }

    private bool ValidateApiSettings()
    {
        if (string.IsNullOrWhiteSpace(TxtApiBaseUrl.Text))
        {
            ShowValidationError("La URL base de la API es obligatoria");
            return false;
        }

        if (!Uri.TryCreate(TxtApiBaseUrl.Text, UriKind.Absolute, out _))
        {
            ShowValidationError("La URL base de la API no es válida. Debe incluir el protocolo (https://)");
            return false;
        }

        if (!int.TryParse(TxtApiTimeout.Text, out int timeout) || timeout <= 0)
        {
            ShowValidationError("El timeout debe ser un número positivo");
            return false;
        }

        return true;
    }

    private bool ValidateSupabaseSettings()
    {
        // Solo validar si está habilitado
        if (ChkSupabaseEnabled.IsChecked != true)
            return true;

        if (string.IsNullOrWhiteSpace(TxtSupabaseHost.Text))
        {
            ShowValidationError("El host de Supabase es obligatorio cuando está habilitado");
            return false;
        }

        if (!int.TryParse(TxtSupabasePort.Text, out int port) || port <= 0 || port > 65535)
        {
            ShowValidationError("El puerto de Supabase debe ser entre 1 y 65535");
            return false;
        }

        if (string.IsNullOrWhiteSpace(TxtSupabaseDatabase.Text))
        {
            ShowValidationError("El nombre de la base de datos es obligatorio cuando Supabase está habilitado");
            return false;
        }

        return true;
    }

    private void ShowValidationError(string message)
    {
        MessageBox.Show(message, "Validación",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    #endregion

    #region Event Handlers

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        LoadSettingsToUI();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateAllSettings())
            return;

        // Crear objetos de configuración con los valores de la UI
        SavedKeycloakSettings = new KeycloakSettings
        {
            Url = TxtKeycloakUrl.Text.Trim(),
            Realm = TxtRealm.Text.Trim(),
            ClientId = TxtClientId.Text.Trim(),
            ClientSecret = _originalKeycloakSettings.ClientSecret,
            RedirectUri = TxtRedirectUri.Text.Trim(),
            AuthorizationEndpoint = _originalKeycloakSettings.AuthorizationEndpoint,
            TokenEndpoint = _originalKeycloakSettings.TokenEndpoint,
            Scope = _originalKeycloakSettings.Scope
        };

        SavedApiSettings = new GedsysApiSettings
        {
            BaseUrl = TxtApiBaseUrl.Text.Trim(),
            AuthToken = TxtApiToken.Text.Trim(),
            TimeoutSeconds = int.Parse(TxtApiTimeout.Text)
        };

        SavedSupabaseSettings = new SupabaseSettings
        {
            Enabled = ChkSupabaseEnabled.IsChecked == true,
            Host = TxtSupabaseHost.Text.Trim(),
            Port = int.Parse(TxtSupabasePort.Text),
            Database = TxtSupabaseDatabase.Text.Trim(),
            Username = TxtSupabaseUsername.Text.Trim(),
            Password = TxtSupabasePassword.Password
        };

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnTestSupabase_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Funcionalidad de prueba de conexión no implementada aún.\n" +
                       "Esta característica estará disponible en futuras versiones.",
                       "Prueba de Conexión",
                       MessageBoxButton.OK,
                       MessageBoxImage.Information);
    }

    #endregion
}