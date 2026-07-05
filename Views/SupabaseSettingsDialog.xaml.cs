using FirmasApp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Windows;
using System.Text.Json;
using System.IO;

namespace FirmasApp.Views;

public partial class SupabaseSettingsDialog : Window
{
    private readonly SupabaseSettings _settings;
    private readonly IConfiguration _configuration;

    public SupabaseSettingsDialog(IConfiguration configuration, IOptions<SupabaseSettings> settings)
    {
        InitializeComponent();
        _configuration = configuration;
        _settings = settings.Value;

        CargarConfiguracionExistente();
    }

    /// <summary>
    /// Carga la configuración existente de Supabase
    /// </summary>
    private void CargarConfiguracionExistente()
    {
        TxtHost.Text = _settings.Host ?? string.Empty;
        TxtPort.Text = _settings.Port.ToString();
        TxtDatabase.Text = _settings.Database ?? "postgres";
        TxtUsername.Text = _settings.Username ?? "postgres";
        ChkEnabled.IsChecked = _settings.Enabled;

        // Password no se carga por seguridad
        if (!string.IsNullOrWhiteSpace(_settings.Password))
        {
            TxtStatus.Text = "⚠️ Contraseña guardada. Deja vacía para mantener la actual.";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Orange;
        }
    }

    /// <summary>
    /// Prueba la conexión con Supabase
    /// </summary>
    private async void BtnProbar_Click(object sender, RoutedEventArgs e)
    {
        var settings = ObtenerConfiguracionActual();
        if (!settings.IsValid())
        {
            MostrarError("Por favor completa todos los campos requeridos.");
            return;
        }

        try
        {
            BtnProbar.IsEnabled = false;
            BtnProbar.Content = "🔄 Conectando...";
            TxtStatus.Text = "🔍 Probando conexión con Supabase...";

            var cloudDb = new Services.CloudDbService(settings.BuildConnectionString());
            var exito = await cloudDb.ProbarConexionAsync();

            if (exito)
            {
                TxtStatus.Text = "✅ Conexión exitosa con Supabase. Los datos son correctos.";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Green;
                MessageBox.Show("Conexión exitosa con Supabase", "Éxito",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                TxtStatus.Text = "❌ Error de conexión. Verifica tus credenciales.";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
                MessageBox.Show("No se pudo conectar con Supabase. Verifica tus credenciales.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MostrarError($"Error al probar conexión: {ex.Message}");
        }
        finally
        {
            BtnProbar.IsEnabled = true;
            BtnProbar.Content = "🔍 Probar Conexión";
        }
    }

    /// <summary>
    /// Guarda la configuración de Supabase
    /// </summary>
    private void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        var settings = ObtenerConfiguracionActual();

        if (ChkEnabled.IsChecked == true && !settings.IsValid())
        {
            MostrarError("Para habilitar la sincronización, completa todos los campos requeridos.");
            return;
        }

        try
        {
            // Actualizar appsettings.json
            var configPath = "appsettings.json";
            var configJson = File.ReadAllText(configPath);
            var configDoc = JsonDocument.Parse(configJson);
            var root = configDoc.RootElement;

            // Crear nuevo objeto JSON con la configuración actualizada
            var newConfig = new Dictionary<string, object>
            {
                ["Keycloak"] = JsonElementToDictionary(root.GetProperty("Keycloak")),
                ["GedsysApi"] = JsonElementToDictionary(root.GetProperty("GedsysApi")),
                ["Supabase"] = new Dictionary<string, object>
                {
                    ["Host"] = settings.Host,
                    ["Port"] = settings.Port,
                    ["Database"] = settings.Database,
                    ["Username"] = settings.Username,
                    ["Password"] = string.IsNullOrWhiteSpace(settings.Password) ?
                        _settings.Password : settings.Password,
                    ["Enabled"] = ChkEnabled.IsChecked == true
                },
                ["WacomStu"] = JsonElementToDictionary(root.GetProperty("WacomStu"))
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(configPath, JsonSerializer.Serialize(newConfig, options));

            TxtStatus.Text = "✅ Configuración guardada. Reinicia la aplicación para aplicar los cambios.";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Green;

            MessageBox.Show(
                "Configuración guardada exitosamente.\n\nReinicia la aplicación para aplicar los cambios.",
                "Configuración Guardada",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MostrarError($"Error guardando configuración: {ex.Message}");
        }
    }

    /// <summary>
    /// Convierte un JsonElement a Dictionary
    /// </summary>
    private Dictionary<string, object> JsonElementToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object>();
        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = property.Value;
        }
        return dict;
    }

    /// <summary>
    /// Cancela y cierra el diálogo
    /// </summary>
    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Obtiene la configuración actual desde los campos del formulario
    /// </summary>
    private SupabaseSettings ObtenerConfiguracionActual()
    {
        return new SupabaseSettings
        {
            Host = TxtHost.Text.Trim(),
            Port = int.TryParse(TxtPort.Text, out var port) ? port : 5432,
            Database = TxtDatabase.Text.Trim(),
            Username = TxtUsername.Text.Trim(),
            Password = TxtPassword.Password,
            Enabled = ChkEnabled.IsChecked == true
        };
    }

    /// <summary>
    /// Muestra un mensaje de error
    /// </summary>
    private void MostrarError(string mensaje)
    {
        TxtStatus.Text = $"❌ {mensaje}";
        TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
    }
}