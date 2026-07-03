# 📝 FirmasApp - Sistema de Gestión de Firmas Biométricas

Aplicación WPF para la gestión de firmas digitales con soporte biométrico mediante tablets Wacom STU-430 y autenticación mediante Keycloak.

## 🏗️ **Arquitectura del Proyecto**

### **📁 Estructura de Directorios**

```
FirmasApp/
├── 📁 Components/              # Componentes UI reutilizables
│   ├── FirmaCanvas.xaml       # Canvas para captura de firma con mouse
│   └── FirmaCanvas_Simple.xaml # Canvas simplificado
├── 📁 Models/                  # Modelos de datos y ViewModels
│   ├── MainViewModel.cs       # ViewModel principal (MVVM)
│   ├── Usuario.cs              # Modelo de usuario
│   └── GedsysApiSettings.cs    # Configuración API y Wacom
├── 📁 Services/                # Lógica de negocio y servicios
│   ├── Native/                 # Capa nativa (Wacom STU SDK)
│   │   ├── WacomStuNative.cs           # P/Invoke declarations
│   │   ├── WacomStuStructs.cs          # Estructuras nativas
│   │   ├── WacomStuConstants.cs        # Constantes y códigos de error
│   │   └── WacomStuCallbackManager.cs  # Gestión de callbacks
│   ├── WacomStuService.cs      # Servicio de captura de firmas
│   ├── UsuarioService.cs       # Gestión de usuarios
│   ├── FirmaService.cs         # Gestión de firmas
│   ├── KeycloakAuthService.cs  # Autenticación OAuth/OIDC
│   ├── ProtocolRegistrationService.cs # Registro de protocolos
│   ├── HttpCallbackService.cs  # Manejo de callbacks HTTP
│   └── AppLog.cs               # Sistema de logging
├── 📁 ViewModels/              # ViewModels específicos
│   └── FirmaViewModel.cs       # ViewModel para gestión de firmas
├── 📁 Views/                   # Vistas XAML
│   ├── LoginView.xaml          # Vista de login
│   ├── GestionFirmaView.xaml   # Vista de gestión de firmas
│   ├── WebLoginDialog.xaml     # Diálogo de login web
│   ├── WaitingForAuthDialog.xaml # Diálogo de espera
│   └── PasteUrlDialog.xaml     # Diálogo para pegar URL manual
├── 📁 libs/                    # DLLs nativas
│   ├── wgssSTU.dll            # Wacom STU SDK (64-bit)
│   ├── libcrypto-1_1-x64.dll  # OpenSSL dependency
│   ├── libssl-1_1-x64.dll     # OpenSSL dependency
│   └── README.txt              # Instrucciones de instalación
├── 📁 Tools/                   # Herramientas adicionales
│   └── WebView2Runtime/        # Runtime WebView2 incluido
├── App.xaml                    # Aplicación principal
├── App.xaml.cs                 # Startup y DI configuration
├── MainWindow.xaml            # Ventana principal
├── MainWindow.xaml.cs         # Code-behind principal
├── appsettings.json           # Configuración de la aplicación
└── FirmasApp.csproj           # Proyecto .NET 8
```

## 🎯 **Patrones de Arquitectura**

### **1. MVVM (Model-View-ViewModel)**

```csharp
// View: XAML UI Components
<TextBox Text="{Binding Busqueda, UpdateSourceTrigger=PropertyChanged}"/>

// ViewModel: Lógica de presentación y comandos
public class MainViewModel : INotifyPropertyChanged
{
    public ICommand BuscarCommand { get; }
    public string Busqueda { get; set; }
}

// Model: Datos de dominio
public class Usuario
{
    public string Nombres { get; set; }
    public string Apellidos { get; set; }
    public bool TieneFirma { get; set; }
}
```

### **2. Dependency Injection (DI)**

```csharp
// Registro de servicios en App.xaml.cs
private void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<KeycloakAuthService>();
    services.AddSingleton<WacomStuService>();
    services.AddSingleton<ProtocolRegistrationService>();
    services.AddTransient<UsuarioService>();
    services.AddTransient<FirmaService>();
    services.AddTransient<MainViewModel>();
    services.AddTransient<FirmaViewModel>();
}
```

### **3. Service Layer Pattern**

```csharp
// Servicios especializados por responsabilidad
public class UsuarioService     // Gestión de usuarios
public class FirmaService       // Gestión de firmas
public class KeycloakAuthService // Autenticación
public class WacomStuService    // Captura biométrica
```

### **4. Repository Pattern (Implícito)**

```csharp
// Servicios actúan como repositorios
public async Task<List<Usuario>> GetUsuariosAsync()
public async Task<string?> ObtenerFirmaComoDataUrlAsync(string username)
public async Task<bool> GuardarFirmaAsync(string username, string firmaDataUrl)
```

## 🔧 **Principios Clean Code Aplicados**

### **SOLID Principles**

#### **S - Single Responsibility Principle**
```csharp
// ✅ Cada clase tiene una única responsabilidad
WacomStuNative         // Solo declaraciones P/Invoke
UsuarioService         // Solo gestión de usuarios
FirmaService          // Solo gestión de firmas
AppLog                // Solo logging
```

#### **O - Open/Closed Principle**
```csharp
// ✅ Abierto a extensión, cerrado a modificación
public abstract class ServicioBase { }
public class UsuarioService : ServicioBase { }
// Nuevo servicio puede extender sin modificar existente
```

#### **L - Liskov Substitution Principle**
```csharp
// ✅ Interfaces consistentes
public interface IDisposable { }
public class WacomStuService : IDisposable { }
// Cualquier derivado de IDisposable puede sustituirse
```

#### **I - Interface Segregation Principle**
```csharp
// ✅ Interfaces específicas y pequeñas
public delegate void PenDataCallback(ref PenData data, IntPtr userData);
public delegate void DeviceChangeCallback(int eventType, IntPtr userData);
// Cada interfaz tiene un propósito específico
```

#### **D - Dependency Inversion Principle**
```csharp
// ✅ Dependencias inyectadas, no creadas internamente
public WacomStuService(WacomStuSettings settings)
public UsuarioService(GedsysApiSettings settings, KeycloakAuthService auth, FirmaService firma)
// Dependencias abstraídas vía constructor
```

### **Clean Code Practices**

#### **Nomenclatura Clara**
```csharp
// ✅ Nombres descriptivos
CapturarFirmaAsync(int timeoutSegundos = 30)
ObtenerFirmaComoDataUrlAsync(string username)
AddAuthHeaderAsync()

// ❌ Evitar
void foo(int x)
string getData()
```

#### **Funciones Pequeñas y Enfocadas**
```csharp
// ✅ Una responsabilidad por función
public async Task<bool> InitializeAsync()
{
    try
    {
        if (_isInitialized) return true;
        AppLog.Info("Wacom", "Iniciando inicialización...");

        if (!LoadNativeLibrary())
            return false;

        await Task.Run(() => ConnectToDevice());
        RegisterCallbacks();

        return true;
    }
    catch (Exception ex)
    {
        AppLog.Error("Wacom", $"Error: {ex.Message}", ex);
        return false;
    }
}
```

#### **Sin Código Duplicado (DRY)**
```csharp
// ✅ Reutilización de componentes
AppLog.Info("Wacom", "Mensaje informativo");
AppLog.Error("Wacom", "Mensaje error", exception);
// Sistema de logging centralizado

// ✅ Reutilización de estructuras
PenData data = new PenData { X = 100, Y = 200, Pressure = 128 };
DeviceInfo info = new DeviceInfo { Width = 1024, Height = 600 };
```

#### **Comentarios Significativos**
```csharp
/// <summary>
/// Servicio para captura de firmas usando tabletas Wacom STU
/// Implementación real con SDK nativo
/// </summary>
public class WacomStuService : IDisposable
{
    /// <summary>
    /// Inicializa la conexión con la tablet Wacom
    /// </summary>
    public async Task<bool> InitializeAsync()
}
```

## 🏛️ **Clean Architecture Layered**

### **📊 Separación por Responsabilidades**

```
┌─────────────────────────────────────────────────────────┐
│              PRESENTATION LAYER                         │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │
│  │ MainWindow  │  │    Views    │  │ ViewModels  │   │
│  │   .xaml     │  │    .xaml    │  │    .cs      │   │
│  └─────────────┘  └─────────────┘  └─────────────┘   │
└─────────────────────────────────────────────────────────┘
                          ↓↑
┌─────────────────────────────────────────────────────────┐
│              BUSINESS LOGIC LAYER                       │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │
│  │ MainView    │  │  FirmaView  │  │   Services   │   │
│  │   Model     │  │    Model    │  │    .cs       │   │
│  └─────────────┘  └─────────────┘  └─────────────┘   │
└─────────────────────────────────────────────────────────┘
                          ↓↑
┌─────────────────────────────────────────────────────────┐
│              DATA LAYER                                   │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │
│  │   Models    │  │  Settings   │  │ Native SDK  │   │
│  │    .cs      │  │  .json      │  │   Layer     │   │
│  └─────────────┘  └─────────────┘  └─────────────┘   │
└─────────────────────────────────────────────────────────┘
```

### **🔄 Flujo de Datos**

```
User Action (UI)
    ↓
ViewModel Command
    ↓
Service Layer
    ↓
External API / Native SDK
    ↓
Response Processing
    ↓
ViewModel Update
    ↓
UI Refresh (Data Binding)
```

## 🚀 **Tecnologías y Frameworks**

### **Core Technologies**
- **.NET 8.0** - Framework principal
- **WPF** - Windows Presentation Foundation para UI
- **C# 12** - Lenguaje de programación
- **XAML** - Markup para UI

### **Libraries & Dependencies**
```xml
<ItemGroup>
  <!-- Configuration -->
  <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
  <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
  <PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="8.0.0" />

  <!-- Dependency Injection -->
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />

  <!-- HTTP -->
  <PackageReference Include="Microsoft.AspNetCore.WebUtilities" Version="8.0.0" />

  <!-- WebView2 -->
  <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2651.64" />
</ItemGroup>
```

### **Authentication & Security**
- **Keycloak** - OAuth 2.0 / OpenID Connect
- **Bearer Token Authentication** - JWT tokens
- **Custom Protocol Registration** - `firmasapp://` callbacks

### **Native Integration**
- **Wacom STU SDK 2.15.4** - Captura de firmas biométricas
- **P/Invoke** - Interoperabilidad con código nativo
- **OpenSSL** - Criptografía para SDK dependencies

## 📦 **Componentes Principales**

### **🔐 Authentication Service**
```csharp
public class KeycloakAuthService
{
    // Gestión de tokens OAuth/OIDC
    public async Task<string?> GetAccessTokenAsync()
    public async Task<bool> ValidateTokenAsync()

    // Gestión de refresh tokens
    public async Task<string?> RefreshAccessTokenAsync()
}
```

### **👥 User Management Service**
```csharp
public class UsuarioService
{
    // CRUD de usuarios
    public async Task<List<Usuario>> GetUsuariosAsync(string? busqueda = null)
    public async Task<Usuario?> GetUsuarioAsync(string username)

    // Verificación de firmas
    public async Task<bool> VerificarFirmaAsync(string username)
}
```

### **✍️ Signature Management Service**
```csharp
public class FirmaService
{
    // Gestión de firmas
    public async Task<string?> ObtenerFirmaComoDataUrlAsync(string username)
    public async Task<bool> GuardarFirmaAsync(string username, string firmaDataUrl)
    public async Task<bool> EliminarFirmaAsync(string username)
}
```

### **🖊️ Biometric Capture Service**
```csharp
public class WacomStuService
{
    // Captura biométrica
    public async Task<bool> InitializeAsync()
    public async Task<StrokeCollection?> CapturarFirmaAsync(int timeoutSegundos = 30)

    // Conversión de formatos
    public byte[] ConvertirTrazosAImagen(StrokeCollection strokes, int ancho = 400, int alto = 200)
    public string ConvertirTrazosADataUrl(StrokeCollection strokes, int ancho = 400, int alto = 200)

    // Eventos de dispositivo
    public event EventHandler<WacomPenDataEventArgs>? PenDataReceived
    public event EventHandler<WacomDeviceEventArgs>? DeviceChanged
}
```

## 🎨 **UI/UX Design**

### **MVVM Pattern Implementation**
```csharp
// ViewModel con INotifyPropertyChanged
public class MainViewModel : INotifyPropertyChanged
{
    private string _busqueda = string.Empty;
    public string Busqueda
    {
        get => _busqueda;
        set
        {
            _busqueda = value;
            OnPropertyChanged(nameof(Busqueda));
        }
    }

    public ICommand BuscarCommand { get; }
}

// Data Binding en XAML
<TextBox Text="{Binding Busqueda, UpdateSourceTrigger=PropertyChanged}"
         Width="300" Height="35"/>
<Button Command="{Binding BuscarCommand}"
        Content="Buscar"/>
```

### **Command Pattern (RelayCommand)**
```csharp
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}

// Uso en ViewModel
BuscarCommand = new RelayCommand(
    async () => await BuscarUsuariosAsync(),
    () => !IsLoading && IsAuthenticated
);
```

## 🔧 **Configuración**

### **appsettings.json**
```json
{
  "Keycloak": {
    "Url": "https://keycloak.gedsys.co",
    "Realm": "development",
    "ClientId": "gedsys-firmas",
    "RedirectUri": "firmasapp://callback"
  },
  "GedsysApi": {
    "BaseUrl": "https://api.development.gedsys.app",
    "TimeoutSeconds": 30
  },
  "WacomStu": {
    "EnableBiometricData": true,
    "AutoConnect": true,
    "DllPath": "libs/wgssSTU.dll",
    "ConnectionTimeoutMs": 5000,
    "CaptureTimeoutSeconds": 30,
    "EnablePressure": true,
    "MinPressureThreshold": 10,
    "UseSimulation": false
  }
}
```

## 📋 **Funcionalidades Principales**

### **✅ Autenticación Segura**
- Login via Keycloak (OAuth 2.0 / OIDC)
- Token management automático
- Token refresh y validación
- Custom protocol registration para callbacks

### **✅ Gestión de Usuarios**
- Listado de empleados con paginación
- Búsqueda y filtrado en tiempo real
- Verificación de estado de firmas
- Información completa del empleado

### **✅ Captura de Firmas**
- **Soporte Mouse:** Canvas de dibujo con mouse/touch
- **Soporte Wacom:** Captura biométrica con tablets STU-430
- **Datos Biométricos:** Presión, posición, timestamp
- **Conversión de Formatos:** StrokeCollection → PNG → Base64
- **Simulación:** Fallback automático sin dispositivo

### **✅ Gestión de Firmas**
- Visualización de firmas existentes
- Captura de nuevas firmas
- Guardado en servidor API
- Eliminación de firmas
- DataURL format para almacenamiento

## 🛠️ **Instalación y Desarrollo**

### **Requisitos Previos**
- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download)
- **Visual Studio 2022** (opcional) o VS Code
- **Wacom STU SDK 2.15.4** (para captura biométrica)
- **Windows 10/11 x64** - Plataforma objetivo

### **Instalación**
```bash
# Clonar repositorio
git clone <repository-url>
cd FirmasApp

# Restaurar dependencias
dotnet restore

# Configurar appsettings.json con tus credenciales

# Ejecutar aplicación
dotnet run
```

### **Instalación SDK Wacom**
1. Descargar Wacom STU SDK desde [Wacom Developer](https://developer-docs.wacom.com/docs/stu-sdk/)
2. Copiar DLLs al directorio `libs/`:
   - `wgssSTU.dll`
   - `libcrypto-1_1-x64.dll`
   - `libssl-1_1-x64.dll`

## 🧪 **Testing y Debugging**

### **Logging System**
```csharp
// Logs guardados en: %LocalAppData%\FirmasApp\debug.log
AppLog.Debug("Source", "Mensaje debug");
AppLog.Info("Source", "Mensaje informativo");
AppLog.Warn("Source", "Mensaje advertencia");
AppLog.Error("Source", "Mensaje error", exception);
```

### **Modos de Operación**
```json
// Modo Producción (con dispositivo Wacom)
"WacomStu": {
  "UseSimulation": false
}

// Modo Desarrollo (sin dispositivo Wacom)
"WacomStu": {
  "UseSimulation": true
}
```

## 📊 **Arquitectura de Seguridad**

### **OAuth 2.0 / OIDC Flow**
```
1. Usuario inicia login
2. Redirect a Keycloak con Authorization Code flow
3. Usuario autentica en Keycloak
4. Callback via custom protocol: firmasapp://callback
5. Exchange authorization code por access token
6. Token guardado y usado para API calls
7. Token refresh automático
```

### **API Security**
```csharp
// Bearer Token Authentication
private async Task<bool> AddAuthHeaderAsync()
{
    var token = await _keycloakAuth.GetAccessTokenAsync();
    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    return true;
}
```

## 🔄 **Ciclo de Vida de la Aplicación**

### **Startup Sequence**
```
1. App.xaml.cs → OnStartup()
2. Configuration Builder → appsettings.json
3. Service Collection → DI Registration
4. ServiceProvider → Build
5. MainViewModel → InitializeAsync()
6. MainWindow → Show()
```

### **Shutdown Sequence**
```
1. App.xaml.cs → OnExit()
2. Pipe Server → Stop
3. Mutex → Release
4. Services → Dispose()
5. Application → Exit
```

## 📈 **Métricas y Optimización**

### **Performance**
- ✅ Async/Await para operaciones I/O
- ✅ Data binding optimizado con UpdateSourceTrigger
- ✅ Lazy loading de ViewModels
- ✅ Memory management con IDisposable pattern
- ✅ Thread-safe operations con locks

### **User Experience**
- ✅ Responsive UI con async operations
- ✅ Loading states y feedback visual
- ✅ Error handling amigable
- ✅ Fallback graceful cuando hardware no disponible
- ✅ Real-time search con debouncing

## 🏆 **Calidad del Código**

### **Code Metrics**
- ✅ **Compilación sin warnings**
- ✅ **Consistencia en nomenclatura**
- ✅ **Proper error handling**
- ✅ **Thread safety implementado**
- ✅ **Memory leak prevention**
- ✅ **Documentation comprehensiva**

### **Testing Coverage**
- ✅ **Manual testing** de flujos principales
- ✅ **Integration testing** con APIs reales
- ✅ **Hardware testing** con Wacom STU-430
- ✅ **Fallback testing** sin dispositivo

## 📚 **Documentación Adicional**

- `WACOM_IMPLEMENTATION_SUMMARY.md` - Guía detallada de implementación Wacom
- `libs/README.txt` - Instrucciones de instalación SDK Wacom
- `appsettings.json` - Configuración con comentarios
- Code comments XML en APIs públicas

## 👥 **Contribución**

### **Clean Code Guidelines**
1. **SOLID Principles** - Siempre
2. **DRY** - Don't Repeat Yourself
3. **KISS** - Keep It Simple, Stupid
4. **YAGNI** - You Aren't Gonna Need It
5. **Proper Error Handling** - Try/catch con logging
6. **Thread Safety** - Lock para shared state
7. **Disposal Pattern** - Implementar IDisposable
8. **Async Best Practices** - ConfigureAwait, proper cancellation

### **Code Review Checklist**
- [ ] Principios SOLID aplicados
- [ ] Nomenclatura clara y consistente
- [ ] Funciones pequeñas y enfocadas
- [ ] Proper error handling
- [ ] Thread safety considerations
- [ ] Memory management proper
- [ ] Documentation adequada
- [ ] No code duplication
- [ ] Async/await usado correctamente

## 🎯 **Roadmap y Mejoras Futuras**

### **Planned Features**
- [ ] Unit Testing con xUnit
- [ ] E2E Testing con SpecFlow
- [ ] Performance profiling
- [ ] Additional biometric devices support
- [ ] Offline mode con sincronización
- [ ] Advanced signature verification
- [ ] Multi-language support

---

**Desarrollado con Clean Architecture, Clean Code y SOLID Principles**

**Versión:** 1.0.0
**Plataforma:** .NET 8.0 / WPF / Windows x64
**Estado:** Production Ready

---
*Última actualización: 2025-07-03*