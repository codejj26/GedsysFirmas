using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FirmasApp.Models;
using FirmasApp.Services;
using FirmasApp.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FirmasApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly FirmaService _firmaService;
    private readonly KeycloakSettings _keycloakSettings;
    private readonly GedsysApiSettings _apiSettings;
    private readonly SupabaseSettings _supabaseSettings;

    public MainWindow(
        MainViewModel viewModel,
        FirmaService firmaService,
        IOptions<KeycloakSettings> keycloakSettings,
        IOptions<GedsysApiSettings> apiSettings,
        IOptions<SupabaseSettings> supabaseSettings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _firmaService = firmaService;
        _keycloakSettings = keycloakSettings.Value;
        _apiSettings = apiSettings.Value;
        _supabaseSettings = supabaseSettings.Value;
        DataContext = _viewModel;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // La inicialización ahora se maneja en App.xaml.cs con los argumentos
    }

    private void Busqueda_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _viewModel.BuscarCommand.Execute(null);
        }
    }

    private void BtnFirmar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Usuario usuario)
        {
            AbrirGestionFirma(usuario);
        }
    }

    private void BtnBorrarFirma_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Usuario usuario)
        {
            BorrarFirmaUsuario(usuario);
        }
    }

    private void AbrirGestionFirma(Usuario usuario)
    {
        try
        {
            var gestorFirma = App.ServiceProvider?.GetService(typeof(ViewModels.FirmaViewModel)) as ViewModels.FirmaViewModel
                ?? throw new InvalidOperationException("No se pudo resolver FirmaViewModel");

            // Reset state of reused VM to avoid mixing previous user data
            gestorFirma.UsuarioActual = null;
            gestorFirma.FirmaDataUrl = null;

            var gestionFirmaView = new Views.GestionFirmaView(gestorFirma);

            var username = ObtenerUsernameDesdeUsuario(usuario);
            if (!string.IsNullOrWhiteSpace(username))
            {
                _ = gestionFirmaView.EstablecerUsuarioAsync(username);
            }

            gestionFirmaView.ShowDialog();

            if (gestionFirmaView.DialogResult.HasValue && gestionFirmaView.DialogResult.Value)
            {
                _ = _viewModel.LoadUsuariosAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error abriendo gestión de firmas: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BorrarFirmaUsuario(Usuario usuario)
    {
        try
        {
            var username = ObtenerUsernameDesdeUsuario(usuario);
            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("No se puede identificar el usuario", "Advertencia",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmacion = MessageBox.Show(
                $"¿Está seguro de eliminar la firma de {usuario.NombreCompleto}?",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmacion == MessageBoxResult.Yes)
            {
                var eliminada = await _firmaService.EliminarFirmaAsync(username, usuario.NombreCompleto);

                if (eliminada)
                {
                    MessageBox.Show($"Firma de {usuario.NombreCompleto} eliminada exitosamente", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    // Recargar usuarios para actualizar estado
                    await _viewModel.LoadUsuariosAsync();
                }
                else
                {
                    MessageBox.Show("No se pudo eliminar la firma", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error eliminando firma: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string? ObtenerUsernameDesdeUsuario(Usuario usuario)
    {
        // Prioridad: CuentaUsuario > Email > Documento > Nombres
        if (!string.IsNullOrWhiteSpace(usuario.CuentaUsuario))
        {
            return usuario.CuentaUsuario;
        }

        if (!string.IsNullOrWhiteSpace(usuario.Email))
        {
            var emailParts = usuario.Email.Split('@');
            return emailParts[0];
        }

        if (!string.IsNullOrWhiteSpace(usuario.Documento))
        {
            return usuario.Documento;
        }

        if (!string.IsNullOrWhiteSpace(usuario.Nombres))
        {
            return usuario.Nombres.ToLower().Replace(" ", ".").Normalize();
        }

        return null;
    }

    private void BtnConfigureGeneral_Click(object sender, RoutedEventArgs e)
    {
        var settingsDialog = new GeneralSettingsDialog(
            _keycloakSettings,
            _apiSettings,
            _supabaseSettings);

        var result = settingsDialog.ShowDialog();

        if (result == true)
        {
            // Actualizar configuración de Keycloak si se modificó
            if (settingsDialog.SavedKeycloakSettings != null)
            {
                _viewModel.UpdateKeycloakSettings(settingsDialog.SavedKeycloakSettings);
            }

            // Nota: Las configuraciones de API y Supabase requieren reinicio
            MessageBox.Show(
                "Configuración general actualizada exitosamente.\n\n" +
                "La nueva configuración se usará en el próximo inicio de sesión.",
                "Configuración Guardada",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}