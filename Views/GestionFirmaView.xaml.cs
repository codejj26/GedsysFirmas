using FirmasApp.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FirmasApp.Views;

public partial class GestionFirmaView : Window
{
    private readonly FirmaViewModel _viewModel;

    public GestionFirmaView(FirmaViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // Suscribirse a cambios en el ViewModel
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(FirmaViewModel.StatusMessage))
            {
                Dispatcher.Invoke(() =>
                {
                    TxtStatus.Text = _viewModel.StatusMessage;
                    ((TxtStatus.Parent as Border)!).Visibility =
                        string.IsNullOrWhiteSpace(_viewModel.StatusMessage) || _viewModel.StatusMessage == "Listo"
                        ? Visibility.Collapsed : Visibility.Visible;
                });
            }
            else if (e.PropertyName == nameof(FirmaViewModel.IsLoading))
            {
                Dispatcher.Invoke(() =>
                {
                    LoadingOverlay.Visibility = _viewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
                    UpdateBotones();
                });
            }
            else if (e.PropertyName == nameof(FirmaViewModel.UsuarioActual))
            {
                Dispatcher.Invoke(() =>
                {
                    Title = $"Gestión de Firmas - {_viewModel.UsuarioActual}";
                    UpdateBotones();
                });
            }
        };
    }

    /// <summary>
    /// Establece el usuario cuya firma se va a gestionar
    /// </summary>
    public async Task EstablecerUsuarioAsync(string username)
    {
        await _viewModel.EstablecerUsuarioAsync(username);
        Title = $"Gestión de Firmas - {username}";
    }

    private void BtnCargarFirma_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.UsuarioActual))
        {
            Task.Run(async () =>
            {
                await _viewModel.CargarFirmaAsync();
            });
        }
    }

    private void BtnGuardarFirma_Click(object sender, RoutedEventArgs e)
    {
        // Obtener la firma del canvas
        var firmaDataUrl = FirmaCanvasControl.ObtenerFirmaDataUrl();
        if (!string.IsNullOrWhiteSpace(firmaDataUrl))
        {
            _viewModel.FirmaDataUrl = firmaDataUrl;
            Task.Run(async () => await _viewModel.GuardarFirmaAsync());
        }
        else
        {
            MessageBox.Show("Capture una firma primero", "Advertencia",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnEliminarFirma_Click(object sender, RoutedEventArgs e)
    {
        Task.Run(async () => await _viewModel.EliminarFirmaAsync());
    }

    private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
    {
        FirmaCanvasControl?.Limpiar();
        _viewModel.LimpiarFirmaCommand.Execute(null);
    }

    private void BtnWacom_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Función Wacom temporalmente deshabilitada", "Información",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void UpdateBotones()
    {
        BtnCargarFirma.IsEnabled = _viewModel.CargarFirmaCommand.CanExecute(null);
        BtnGuardarFirma.IsEnabled = _viewModel.GuardarFirmaCommand.CanExecute(null);
        BtnEliminarFirma.IsEnabled = _viewModel.EliminarFirmaCommand.CanExecute(null);
        BtnLimpiar.IsEnabled = _viewModel.LimpiarFirmaCommand.CanExecute(null);
        BtnWacom.IsEnabled = _viewModel.IniciarWacomCommand.CanExecute(null);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateBotones();
    }
}