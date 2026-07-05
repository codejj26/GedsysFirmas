using System.Windows;
using FirmasApp.Models;

namespace FirmasApp.Views;

public partial class KeycloakSettingsDialog : Window
{
    private readonly KeycloakSettings _originalSettings;
    private readonly KeycloakSettings _currentSettings;

    public KeycloakSettings SavedSettings { get; private set; } = null!;

    public KeycloakSettingsDialog(KeycloakSettings currentSettings)
    {
        InitializeComponent();

        _originalSettings = CloneSettings(currentSettings);
        _currentSettings = CloneSettings(currentSettings);

        LoadSettingsToUI();
    }

    private KeycloakSettings CloneSettings(KeycloakSettings settings)
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

    private void LoadSettingsToUI()
    {
        TxtKeycloakUrl.Text = _currentSettings.Url;
        TxtRealm.Text = _currentSettings.Realm;
        TxtClientId.Text = _currentSettings.ClientId;
        TxtRedirectUri.Text = _currentSettings.RedirectUri;

        TxtCurrentUrl.Text = _originalSettings.Url;
        TxtCurrentRealm.Text = _originalSettings.Realm;
        TxtCurrentClientId.Text = _originalSettings.ClientId;
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        TxtKeycloakUrl.Text = _originalSettings.Url;
        TxtRealm.Text = _originalSettings.Realm;
        TxtClientId.Text = _originalSettings.ClientId;
        TxtRedirectUri.Text = _originalSettings.RedirectUri;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var newSettings = new KeycloakSettings
        {
            Url = TxtKeycloakUrl.Text.Trim(),
            Realm = TxtRealm.Text.Trim(),
            ClientId = TxtClientId.Text.Trim(),
            ClientSecret = _originalSettings.ClientSecret,
            RedirectUri = TxtRedirectUri.Text.Trim(),
            AuthorizationEndpoint = _originalSettings.AuthorizationEndpoint,
            TokenEndpoint = _originalSettings.TokenEndpoint,
            Scope = _originalSettings.Scope
        };

        if (!ValidateSettings(newSettings))
        {
            return;
        }

        SavedSettings = newSettings;
        DialogResult = true;
        Close();
    }

    private bool ValidateSettings(KeycloakSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            MessageBox.Show("La URL de Keycloak es obligatoria", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!Uri.TryCreate(settings.Url, UriKind.Absolute, out _))
        {
            MessageBox.Show("La URL de Keycloak no es válida. Debe incluir el protocolo (https://)", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.Realm))
        {
            MessageBox.Show("El Realm es obligatorio", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.ClientId))
        {
            MessageBox.Show("El Client ID es obligatorio", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(settings.RedirectUri))
        {
            MessageBox.Show("El Redirect URI es obligatorio", "Validación",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
