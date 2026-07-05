using FirmasApp.Models;
using Microsoft.Extensions.Options;
using System.Windows;

namespace FirmasApp.Views;

public partial class LoginView : Window
{
    private readonly MainViewModel _viewModel;
    private readonly KeycloakSettings _keycloakSettings;

    public LoginView(MainViewModel viewModel, IOptions<KeycloakSettings> keycloakSettings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _keycloakSettings = keycloakSettings.Value;
        DataContext = _viewModel;
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsDialog = new KeycloakSettingsDialog(_keycloakSettings);
        var result = settingsDialog.ShowDialog();

        if (result == true && settingsDialog.SavedSettings != null)
        {
            _viewModel.UpdateKeycloakSettings(settingsDialog.SavedSettings);
            MessageBox.Show(
                "Configuración de Keycloak actualizada exitosamente.\n\n" +
                "La nueva configuración se usará en el próximo inicio de sesión.",
                "Configuración Guardada",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
