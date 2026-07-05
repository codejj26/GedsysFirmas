using System.Windows;
using System.IO;
using System.IO.Pipes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FirmasApp.Models;
using FirmasApp.Services;

namespace FirmasApp;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }
    public static IConfiguration? Configuration { get; private set; }
    private const string MutexName = "Global\\FirmasAppSingleInstance";
    private const string PipeName = "FirmasAppCallback";
    private Mutex? _mutex;
    private CancellationTokenSource? _pipeCts;
    private MainViewModel? _mainViewModel;
    private ProtocolRegistrationService? _protocolService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(false, MutexName, out var createdNew);
        AppLog.Info("App", $"createdNew: {createdNew}");
        AppLog.Info("App", $"Args: {(e.Args != null && e.Args.Length > 0 ? e.Args[0] : "null")}");

        if (!createdNew)
        {
            AppLog.Info("App", "Segunda instancia detectada");

            if (e.Args != null && e.Args.Length > 0 &&
                e.Args[0].StartsWith("firmasapp://", StringComparison.OrdinalIgnoreCase))
            {
                AppLog.Info("App", $"Enviando callback a instancia existente: {e.Args[0]}");
                var ok = SendCallbackToRunningInstance(e.Args[0]);
                if (!ok)
                {
                    MessageBox.Show(
                        "No se pudo comunicar con la aplicación principal.\n\n" +
                        "Posibles causas:\n" +
                        "• La aplicación principal se cerró.\n" +
                        "• Otra instancia está usando el mismo puerto/pipeline.\n\n" +
                        "Abra la aplicación manualmente y use 'Iniciar Sesión' de nuevo, " +
                        "o en la ventana de login presione 'Pegar URL manualmente' " +
                        "con la URL del callback de su navegador.",
                        "Firmas App",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            else
            {
                MessageBox.Show("La aplicación ya está ejecutándose.", "Firmas App",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Shutdown();
            return;
        }

        AppLog.Info("App", "Primera instancia iniciada");

        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        Configuration = builder.Build();

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        App.ServiceProvider = serviceCollection.BuildServiceProvider();

        _mainViewModel = ServiceProvider.GetRequiredService<MainViewModel>();
        _protocolService = ServiceProvider.GetRequiredService<ProtocolRegistrationService>();

        // Registrar protocolo al startup (antes de cualquier login)
        TryRegisterProtocol();

        StartPipeServer();

        try
        {
            _mainViewModel.InitializeAsync(e.Args ?? Array.Empty<string>()).GetAwaiter().GetResult();
        }
        catch (Exception initEx)
        {
            AppLog.Error("App", $"Error en InitializeAsync: {initEx.Message}", initEx);
        }

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void TryRegisterProtocol()
    {
        try
        {
            if (_protocolService == null) return;
            if (_protocolService.IsProtocolRegistered())
            {
                AppLog.Info("App", "Protocolo firmasapp:// ya estaba registrado");
                return;
            }

            _protocolService.RegisterProtocol();
            AppLog.Info("App", "Protocolo firmasapp:// registrado al inicio");
        }
        catch (Exception ex)
        {
            AppLog.Warn("App", $"No se pudo pre-registrar el protocolo: {ex.Message}");
        }
    }

    private void StartPipeServer()
    {
        try
        {
            _pipeCts = new CancellationTokenSource();
            Task.Run(() => ListenForPipeCallbacksAsync(_pipeCts.Token));
            AppLog.Info("App", "Pipe server iniciado");
        }
        catch (Exception ex)
        {
            AppLog.Error("App", $"Error iniciando pipe server: {ex.Message}", ex);
        }
    }

    private async Task ListenForPipeCallbacksAsync(CancellationToken cancellationToken)
    {
        AppLog.Debug("App", "Iniciando ListenForPipeCallbacksAsync");

        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? pipeServer = null;
            try
            {
                pipeServer = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                AppLog.Debug("App", "Esperando conexión de pipe...");

                await pipeServer.WaitForConnectionAsync(cancellationToken);
                AppLog.Debug("App", "Cliente conectado al pipe");

                string? callbackUrl;
                using (var reader = new StreamReader(pipeServer))
                {
                    callbackUrl = await reader.ReadLineAsync();
                }

                AppLog.Info("App", $"Datos recibidos del pipe: {callbackUrl}");

                if (!string.IsNullOrWhiteSpace(callbackUrl) &&
                    callbackUrl.StartsWith("firmasapp://", StringComparison.OrdinalIgnoreCase))
                {
                    await Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (_mainViewModel == null)
                        {
                            AppLog.Warn("App", "MainViewModel es null al recibir callback");
                            return;
                        }

                        try
                        {
                            await _mainViewModel.ProcessCallbackFromPipeAsync(callbackUrl);
                            AppLog.Info("App", "Callback entregado al MainViewModel");
                        }
                        catch (Exception ex)
                        {
                            AppLog.Error("App", $"Error entregando callback: {ex.Message}", ex);
                        }
                    });
                }
                else
                {
                    AppLog.Warn("App", $"Callback inválido: {callbackUrl}");
                }
            }
            catch (OperationCanceledException)
            {
                AppLog.Debug("App", "Pipe server cancelado");
                break;
            }
            catch (Exception ex)
            {
                AppLog.Error("App", $"Error en pipe server: {ex.Message}", ex);
                try { await Task.Delay(2000, cancellationToken); }
                catch (OperationCanceledException) { break; }
            }
            finally
            {
                pipeServer?.Dispose();
            }
        }
    }

    private bool SendCallbackToRunningInstance(string callbackUrl)
    {
        const int maxAttempts = 2;
        const int timeoutMs = 10000;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                AppLog.Debug("App", $"Intento {attempt}: conectando al pipe servidor...");

                using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                pipeClient.Connect(timeoutMs);
                AppLog.Debug("App", "Conectado al pipe servidor");

                using var writer = new StreamWriter(pipeClient);
                writer.WriteLine(callbackUrl);
                writer.Flush();
                AppLog.Info("App", $"Callback enviado al pipe: {callbackUrl}");
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Warn("App", $"Intento {attempt} falló: {ex.GetType().Name}: {ex.Message}");
            }
        }

        AppLog.Error("App", "No se pudo enviar el callback tras varios intentos");
        return false;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _pipeCts?.Cancel();

            if (_mutex != null)
            {
                try { _mutex.ReleaseMutex(); }
                catch (Exception ex)
                {
                    AppLog.Warn("App", $"Error liberando mutex: {ex.Message}");
                }
                finally
                {
                    _mutex.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("App", $"Error en OnExit: {ex.Message}", ex);
        }
        finally
        {
            base.OnExit(e);
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.Configure<GedsysApiSettings>(Configuration!.GetSection("GedsysApi"));
        services.Configure<WacomStuSettings>(Configuration.GetSection("WacomStu"));
        services.Configure<KeycloakSettings>(Configuration.GetSection("Keycloak"));
        services.Configure<SupabaseSettings>(Configuration.GetSection("Supabase"));

        services.AddSingleton<KeycloakAuthService>(sp =>
            new KeycloakAuthService(
                Configuration.GetSection("Keycloak").Get<KeycloakSettings>()
                ?? new KeycloakSettings()));

        services.AddSingleton<ProtocolRegistrationService>();

        services.AddSingleton<WacomStuService>(sp =>
            new WacomStuService(
                Configuration.GetSection("WacomStu").Get<WacomStuSettings>()
                ?? new WacomStuSettings()));

        services.AddTransient(sp =>
            new UsuarioService(
                Configuration.GetSection("GedsysApi").Get<GedsysApiSettings>()
                ?? new GedsysApiSettings(),
                sp.GetRequiredService<KeycloakAuthService>(),
                sp.GetRequiredService<FirmaService>()));

        // Registrar servicios de BD local y sincronización
        services.AddSingleton<LocalDbService>();
        services.AddSingleton<QueueService>();
        services.AddSingleton<SyncCoordinatorService>(sp =>
            new SyncCoordinatorService(
                sp.GetRequiredService<QueueService>(),
                sp.GetRequiredService<LocalDbService>(),
                Configuration.GetSection("GedsysApi").Get<GedsysApiSettings>() ?? new GedsysApiSettings(),
                sp.GetRequiredService<KeycloakAuthService>()));

        // Registrar servicios de cloud (Supabase) - solo si está habilitado
        var supabaseSettings = Configuration.GetSection("Supabase").Get<SupabaseSettings>();
        if (supabaseSettings != null && supabaseSettings.Enabled && supabaseSettings.IsValid())
        {
            services.AddSingleton<CloudDbService>(sp =>
                new CloudDbService(supabaseSettings.BuildConnectionString()));

            services.AddSingleton<CloudSyncService>();

            AppLog.Info("App", "Servicios de Supabase habilitados");
        }
        else
        {
            AppLog.Info("App", "Servicios de Supabase deshabilitados (configuración incompleta)");
        }

        services.AddTransient(sp =>
            new FirmaService(
                Configuration.GetSection("GedsysApi").Get<GedsysApiSettings>()
                ?? new GedsysApiSettings(),
                sp.GetRequiredService<KeycloakAuthService>(),
                sp.GetRequiredService<SyncCoordinatorService>(),
                sp.GetRequiredService<LocalDbService>()));

        services.AddTransient<MainViewModel>();
        services.AddTransient<ViewModels.FirmaViewModel>();
        services.AddTransient(sp => new MainWindow(
            sp.GetRequiredService<MainViewModel>(),
            sp.GetRequiredService<FirmaService>(),
            sp.GetRequiredService<IOptions<KeycloakSettings>>()));
    }
}
