using FirmasApp.Models;
using Microsoft.Extensions.Options;
using System.Windows;

namespace FirmasApp.Views;

public partial class LoginView : Window
{
    private readonly MainViewModel _viewModel;
    private readonly KeycloakSettings _keycloakSettings;
    private readonly GedsysApiSettings _apiSettings;
    private readonly SupabaseSettings _supabaseSettings;

    public LoginView(
        MainViewModel viewModel,
        IOptions<KeycloakSettings> keycloakSettings,
        IOptions<GedsysApiSettings> apiSettings,
        IOptions<SupabaseSettings> supabaseSettings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _keycloakSettings = keycloakSettings.Value;
        _apiSettings = apiSettings.Value;
        _supabaseSettings = supabaseSettings.Value;
        DataContext = _viewModel;
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsDialog = new GeneralSettingsDialog(
            _keycloakSettings,
            _apiSettings,
            _supabaseSettings);

        var result = settingsDialog.ShowDialog();

        if (result == true)
        {
            if (settingsDialog.SavedKeycloakSettings != null)
            {
                _viewModel.UpdateKeycloakSettings(settingsDialog.SavedKeycloakSettings);
            }

            MessageBox.Show(
                "Configuración general actualizada exitosamente.\n\n" +
                "La nueva configuración se usará en el próximo inicio de sesión.",
                "Configuración Guardada",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
