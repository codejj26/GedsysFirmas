using FirmasApp.Services;
using FirmasApp.Models;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace FirmasApp.ViewModels;

public class FirmaViewModel : INotifyPropertyChanged
{
    private readonly FirmaService _firmaService;
    private readonly WacomStuService _wacomService;
    private string? _usuarioActual;
    private string? _firmaDataUrl;
    private bool _tieneFirma;
    private bool _isLoading;
    private string _statusMessage = "Listo";

    public FirmaViewModel(FirmaService firmaService, WacomStuService wacomService)
    {
        _firmaService = firmaService;
        _wacomService = wacomService;

        CargarFirmaCommand = new RelayCommand(async () => await CargarFirmaAsync(), () => !IsLoading && !string.IsNullOrWhiteSpace(UsuarioActual));
        GuardarFirmaCommand = new RelayCommand(async () => await GuardarFirmaAsync(), () => !IsLoading && !string.IsNullOrWhiteSpace(FirmaDataUrl));
        EliminarFirmaCommand = new RelayCommand(async () => await EliminarFirmaAsync(), () => !IsLoading && TieneFirma);
        LimpiarFirmaCommand = new RelayCommand(() => LimpiarFirma(), () => !IsLoading);
        IniciarWacomCommand = new RelayCommand(async () => await IniciarWacomAsync(), () => !IsLoading);
    }

    public string? UsuarioActual
    {
        get => _usuarioActual;
        set
        {
            _usuarioActual = value;
            OnPropertyChanged(nameof(UsuarioActual));
            UpdateCommands();
        }
    }

    public string? FirmaDataUrl
    {
        get => _firmaDataUrl;
        set
        {
            _firmaDataUrl = value;
            OnPropertyChanged(nameof(FirmaDataUrl));
            TieneFirma = !string.IsNullOrWhiteSpace(value);
        }
    }

    public bool TieneFirma
    {
        get => _tieneFirma;
        set
        {
            _tieneFirma = value;
            OnPropertyChanged(nameof(TieneFirma));
            UpdateCommands();
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

    public ICommand CargarFirmaCommand { get; }
    public ICommand GuardarFirmaCommand { get; }
    public ICommand EliminarFirmaCommand { get; }
    public ICommand LimpiarFirmaCommand { get; }
    public ICommand IniciarWacomCommand { get; }

    /// <summary>
    /// Establece el usuario actual y carga su firma si existe
    /// </summary>
    public async Task EstablecerUsuarioAsync(string username)
    {
        UsuarioActual = username;
        await CargarFirmaAsync();
    }

    /// <summary>
    /// Carga la firma del usuario actual desde el servidor
    /// </summary>
    public async Task CargarFirmaAsync()
    {
        if (string.IsNullOrWhiteSpace(UsuarioActual))
        {
            StatusMessage = "Seleccione un usuario primero";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = $"Cargando firma de {UsuarioActual}...";

            var firma = await _firmaService.ObtenerFirmaComoDataUrlAsync(UsuarioActual);

            if (firma != null)
            {
                FirmaDataUrl = firma;
                StatusMessage = $"Firma de {UsuarioActual} cargada";
            }
            else
            {
                FirmaDataUrl = null;
                StatusMessage = $"{UsuarioActual} no tiene firma almacenada";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error cargando firma: {ex.Message}";
            MessageBox.Show($"Error cargando firma: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Guarda la firma actual en el servidor
    /// </summary>
    public async Task GuardarFirmaAsync()
    {
        if (string.IsNullOrWhiteSpace(UsuarioActual))
        {
            StatusMessage = "Seleccione un usuario primero";
            return;
        }

        if (string.IsNullOrWhiteSpace(FirmaDataUrl))
        {
            StatusMessage = "Capture una firma primero";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = $"Guardando firma de {UsuarioActual}...";

            var resultado = await _firmaService.GuardarFirmaAsync(UsuarioActual, FirmaDataUrl);

            if (resultado != null)
            {
                TieneFirma = true;
                StatusMessage = $"Firma guardada exitosamente ({resultado.TamanoBytes} bytes)";
                MessageBox.Show($"Firma guardada exitosamente para {UsuarioActual}", "Éxito",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = "Error al guardar la firma";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error guardando firma: {ex.Message}";
            MessageBox.Show($"Error guardando firma: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Elimina la firma del usuario actual
    /// </summary>
    public async Task EliminarFirmaAsync()
    {
        if (string.IsNullOrWhiteSpace(UsuarioActual))
        {
            StatusMessage = "Seleccione un usuario primero";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = $"Eliminando firma de {UsuarioActual}...";

            var confirmacion = MessageBox.Show(
                $"¿Está seguro de eliminar la firma de {UsuarioActual}?",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmacion == MessageBoxResult.Yes)
            {
                var eliminada = await _firmaService.EliminarFirmaAsync(UsuarioActual);

                if (eliminada)
                {
                    FirmaDataUrl = null;
                    TieneFirma = false;
                    StatusMessage = $"Firma de {UsuarioActual} eliminada";
                    MessageBox.Show($"Firma eliminada exitosamente", "Éxito",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = "No se pudo eliminar la firma";
                }
            }
            else
            {
                StatusMessage = "Eliminación cancelada";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error eliminando firma: {ex.Message}";
            MessageBox.Show($"Error eliminando firma: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Limpia la firma actual del canvas
    /// </summary>
    private void LimpiarFirma()
    {
        FirmaDataUrl = null;
        StatusMessage = "Firma limpiada";
    }

    /// <summary>
    /// Inicia captura desde Wacom
    /// </summary>
    public async Task IniciarWacomAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Iniciando Wacom STU...";

            if (!_wacomService.IsConnected)
            {
                var conectado = await _wacomService.InitializeAsync();
                if (!conectado)
                {
                    StatusMessage = "Wacom STU no está conectado";
                    MessageBox.Show("No se detectó ninguna tablet Wacom STU conectada.\nUse mouse para firmar.",
                        "Wacom no disponible", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            StatusMessage = "Wacom conectado - Capturando firma...";
            // TODO: Implementar captura real desde Wacom
            MessageBox.Show("Funcionalidad Wacom en desarrollo. Use mouse para firmar.",
                "Información", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error con Wacom: {ex.Message}";
            MessageBox.Show($"Error con Wacom: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateCommands()
    {
        ((RelayCommand)CargarFirmaCommand).RaiseCanExecuteChanged();
        ((RelayCommand)GuardarFirmaCommand).RaiseCanExecuteChanged();
        ((RelayCommand)EliminarFirmaCommand).RaiseCanExecuteChanged();
        ((RelayCommand)LimpiarFirmaCommand).RaiseCanExecuteChanged();
        ((RelayCommand)IniciarWacomCommand).RaiseCanExecuteChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}