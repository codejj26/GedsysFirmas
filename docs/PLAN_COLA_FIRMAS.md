# Plan Técnico - Sistema de Cola de Firmas con BD Local

## 🎯 Objetivo

Implementar un sistema robusto de gestión de firmas con base de datos local para garantizar la continuidad del servicio cuando falle la conexión a internet o el servidor.

## 📋 Requisitos Funcionales

### **1. Base de Datos Local**
- ✅ **SQLite** (recomendado) por ser:
  - Servidorless, sin instalación adicional
  - Integración nativa con .NET
  - Rendimiento excelente para aplicaciones desktop
  - Fácil backup y migración

### **2. Sistema de Cola de Firmas**
- ✅ Guardar firmas localmente cuando falla la subida
- ✅ Reintentar subida automática cuando se restablezca la conexión
- ✅ Mostrar estado "En proceso" durante la subida
- ✅ Notificar cuando se completen las subidas pendientes

### **3. Estados de Firma**
- `SinFirma` → Usuario sin firma registrada
- `ConFirma` → Firma guardada en servidor y local
- `EnProceso` → Firma en cola esperando subirse
- `FallaSubida` → Firma local pero falló subida al servidor

## 🏗️ Arquitectura Propuesta

```
┌─────────────────────────────────────────────────────────┐
│                   FirmasApp UI                          │
│  - Mostrar estados de firma                             │
│  - Indicador de "En proceso"                            │
│  - Notificaciones de subida completada                 │
└────────────────┬────────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────────┐
│              FirmaService                                │
│  - GuardarFirmaAsync()                                  │
│  - VerificarColaPendiente()                             │
│  - ProcesarColaAsync()                                  │
└────────────────┬────────────────────────────────────────┘
                 │
    ┌────────────┴────────────┐
    │                         │
┌───▼──────────────┐  ┌────▼─────────────┐
│  QueueService     │  │  LocalDbService  │
│  - EncolarFirma() │  │  - Guardar()     │
│  - ProcesarCola() │  │  - Obtener()     │
│  - Reintentar()   │  │  - Actualizar()   │
└───┬──────────────┘  └────┬─────────────┘
    │                      │
┌───▼──────────────────────▼──────────────────────────────┐
│              SQLite Database                             │
│  - Tabla: firmas                                        │
│  - Tabla: cola_firmas                                    │
│  - Tabla: sincronizacion                                 │
└──────────────────────────────────────────────────────────┘
```

## 📊 Esquema de Base de Datos (SQLite)

### **Tabla: firmas**
```sql
CREATE TABLE firmas (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    username TEXT NOT NULL UNIQUE,
    nombre_completo TEXT,
    firma_data_url TEXT NOT NULL,  -- Base64
    estado_firma TEXT NOT NULL,    -- 'SinFirma', 'ConFirma', 'EnProceso', 'FallaSubida'
    fecha_local TEXT NOT NULL,     -- ISO8601
    fecha_servidor TEXT,           -- ISO8601, NULL si no se ha subido
    version INTEGER DEFAULT 1,
    creado_en TEXT NOT NULL,       -- Timestamp local
    actualizado_en TEXT NOT NULL   -- Timestamp local
);
```

### **Tabla: cola_firmas**
```sql
CREATE TABLE cola_firmas (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    username TEXT NOT NULL,
    operacion TEXT NOT NULL,       -- 'Guardar', 'Eliminar', 'Actualizar'
    firma_data_url TEXT,           -- Base64 (para Guardar/Actualizar)
    intentos INTEGER DEFAULT 0,
    max_intentos INTEGER DEFAULT 5,
    ultimo_error TEXT,
    estado TEXT NOT NULL,         -- 'Pendiente', 'Procesando', 'Fallido'
    creado_en TEXT NOT NULL,
    proximo_intento TEXT           -- ISO8601
);
```

### **Tabla: sincronizacion**
```sql
CREATE TABLE sincronizacion (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    ultima_sincro TEXT,
    pendientes INTEGER DEFAULT 0,
    procesados INTEGER DEFAULT 0,
    fallidos INTEGER DEFAULT 0,
    estado TEXT DEFAULT 'Sincronizado'
);
```

## 🔧 Componentes a Implementar

### **1. NuGet Packages**
```xml
<PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0"/>
<PackageReference Include="Dapper" Version="2.0.0"/>
```

### **2. Nuevos Servicios**

#### **LocalDbService.cs**
```csharp
public class LocalDbService
{
    // Inicializar SQLite
    // Crear tablas
    // CRUD de firmas
    // Gestión de cola
    // Consultas de estado
}
```

#### **QueueService.cs**
```csharp
public class QueueService
{
    // Encolar firma fallida
    // Procesar cola pendiente
    // Reintentar con backoff exponencial
    // Notificar progreso
}
```

#### **SyncCoordinatorService.cs**
```csharp
public class SyncCoordinatorService
{
    // Verificar conexión
    // Iniciar sincronización
    // Procesar cola en background
    // Notificar usuario
}
```

### **3. Modificaciones a Servicios Existentes**

#### **FirmaService.cs**
```csharp
public async Task<bool> GuardarFirmaAsync(string username, string firmaDataUrl)
{
    try
    {
        // 1. Intentar guardar en servidor
        var exito = await _apiService.GuardarFirmaEnServidor(username, firmaDataUrl);

        if (exito)
        {
            // 2. Guardar en BD local como confirmado
            await _localDbService.GuardarFirmaConfirmada(username, firmaDataUrl);
            return true;
        }
        else
        {
            // 3. Encolar para reintento
            await _queueService.EncolarFirma(username, firmaDataUrl, "Guardar");
            return false;
        }
    }
    catch (HttpRequestException ex)
    {
        // Sin conexión - guardar local y encolar
        await _localDbService.GuardarFirmaLocal(username, firmaDataUrl, "EnProceso");
        await _queueService.EncolarFirma(username, firmaDataUrl, "Guardar");
        throw;
    }
}
```

### **4. Modelo de Estados**

#### **EstadoFirma Enum**
```csharp
public enum EstadoFirma
{
    SinFirma = 0,
    EnProceso = 1,      // En cola o subiendo
    ConFirma = 2,       // Confirmado en servidor
    FallaSubida = 3     // Guardado local pero falló subida
}
```

### **5. ViewModel Modificado**

#### **Usuario.cs**
```csharp
public class Usuario
{
    // ... propiedades existentes
    public EstadoFirma EstadoFirmaEnum { get; set; }

    // Propiedad computada para UI
    public string EstadoFirmaDisplay => EstadoFirmaEnum switch
    {
        EstadoFirma.SinFirma => "Sin firma",
        EstadoFirma.EnProceso => "⏳ En proceso",
        EstadoFirma.ConFirma => "✓ Firmado",
        EstadoFirma.FallaSubida => "⚠️ Pendiente de subida"
    };

    // Color según estado
    public Brush EstadoFirmaColor => EstadoFirmaEnum switch
    {
        EstadoFirma.SinFirma => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
        EstadoFirma.EnProceso => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
        EstadoFirma.ConFirma => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
        EstadoFirma.FallaSubida => new SolidColorBrush(Color.FromRgb(255, 193, 7))
    };
}
```

## 🔄 Flujo de Trabajo

### **Escenario 1: Conexión Normal**
```
1. Usuario firma → FirmaCanvas
2. Click "Guardar"
3. Guardar en servidor API
4. ✅ Guardar en BD local (estado: ConFirma)
5. Actualizar UI: "✓ Firmado"
```

### **Escenario 2: Sin Conexión**
```
1. Usuario firma → FirmaCanvas
2. Click "Guardar"
3. ❌ Intento falla (sin conexión)
4. Guardar en BD local (estado: EnProceso)
5. Encolar en cola_firmas
6. Mostrar UI: "⏳ En proceso"
7. Background: Reintentar cada 30 segundos
8. Conexión restablecida → Procesar cola
9. ✅ Subida exitosa
10. Actualizar BD local (estado: ConFirma)
11. Notificar: "✅ Firma subida correctamente"
```

### **Escenario 3: Servidor Lento**
```
1. Usuario firma → FirmaCanvas
2. Click "Guardar"
3. Timeout en servidor (30s)
4. Guardar en BD local (estado: EnProceso)
5. Encolar para reintento
6. UI: "⏳ En proceso"
7. Background: Reintentos con backoff
8. Eventualmente: Subida exitosa o aviso de fallo
```

## 🔔 Sistema de Notificaciones

### **Tipos de Notificaciones**
```csharp
public enum TipoNotificacion
{
    FirmaEncolada,           // "Firma guardada localmente"
    SincronizacionIniciada,  // "Sincronizando firmas pendientes..."
    FirmaSubida,             // "Firma de [Usuario] subida correctamente"
    SincronizacionCompletada,// "X firmas sincronizadas correctamente"
    SincronizacionFallida    // "X firmas no pudieron subirse"
}
```

### **Canal de Notificaciones**
- Toast/Notification en UI
- StatusBar con progreso
- Log de eventos (opcional)

## 📐 Interfaz de Usuario Modificada

### **Estado Visual de Firma**
```xml
<Ellipse Width="12" Height="12">
  <Ellipse.Fill>
    <SolidColorBrush Color="{Binding EstadoFirmaColor}"/>
  </Ellipse.Fill>
</Ellipse>
<TextBlock Text="{Binding EstadoFirmaDisplay}"/>
```

### **Indicador de Sincronización**
```xml
<Border Background="#FFF3E0"
        Visibility="{Binding HayFirmasEnCola, Converter={...}}">
  <StackPanel Orientation="Horizontal">
    <TextBlock Text="⏳"/>
    <TextBlock Text="{Binding FirmasEnColaCount}"/>
    <TextBlock Text="firmas pendientes de subida"/>
  </StackPanel>
</Border>
```

## 🧪 Estrategia de Testing

### **Unit Tests**
```csharp
// LocalDbServiceTests.cs
[Fact]
public async Task GuardarFirma_Local_CrearRegistroCorrecto()
[Fact]
public async Task EncolarFirma_CrearRegistroEnCola()

// QueueServiceTests.cs
[Fact]
public async Task ProcesarCola_SubirFirmasPendientes()
[Fact]
public async Task ReintentarConBackoff_RespetarDelay()

// SyncCoordinatorTests.cs
[Fact]
public async Task DetectarConexion_CuandoServidorDisponible()
```

### **Integration Tests**
```csharp
[Fact]
public async Task FlujoCompleto_SinConexion_A_Conexion()
{
    // 1. Simular sin conexión
    // 2. Guardar firma
    // 3. Verificar BD local
    // 4. Simular conexión
    // 5. Procesar cola
    // 6. Verificar servidor
}
```

## 📈 Métricas y Monitoreo

### **Métricas a Registrar**
- Tiempo de subida de firma
- Tasa de éxito/fracaso
- Tamaño de cola en el tiempo
- Tiempo de sincronización

### **Logs**
```csharp
AppLog.Info("Queue", $"Firma encolada: {username}");
AppLog.Warn("Queue", $"Fallo subida (intento {intentos}): {username}");
AppLog.Info("Sync", $"Cola procesada: {exitosas} exitosas, {fallidas} fallidas");
```

## 🚀 Plan de Implementación (Fases)

### **Fase 1: Base de Datos Local (1-2 días)**
1. Instalar paquetes NuGet
2. Crear LocalDbService
3. Implementar esquema SQLite
4. CRUD básico de firmas
5. Tests unitarios

### **Fase 2: Sistema de Cola (2-3 días)**
1. Crear QueueService
2. Implementar encolado
3. Implementar procesamiento de cola
4. Sistema de reintentos con backoff
5. Tests unitarios

### **Fase 3: Sincronización (2-3 días)**
1. Crear SyncCoordinatorService
2. Modificar FirmaService
3. Implementar detección de conexión
4. Procesamiento en background
5. Tests de integración

### **Fase 4: Interfaz de Usuario (1-2 días)**
1. Modificar estados de firma
2. Agregar indicadores visuales
3. Sistema de notificaciones
4. UX de sincronización

### **Fase 5: Testing y Optimización (2-3 días)**
1. Tests end-to-end
2. Pruebas de estrés
3. Optimización de performance
4. Documentación

## 📦 Paquetes NuGet Requeridos

```xml
<!-- Base de datos SQLite -->
<PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0"/>
<PackageReference Include="Dapper" Version="2.0.0"/>

<!-- Opcional: ORM -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0"/>
```

## 🎯 Consideraciones de Diseño

### **Thread Safety**
- Usar locks para acceso a BD
- SemaphoreSlim para limitar concurrencia
- Thread-safe queue processing

### **Performance**
- Índices en tablas críticas
- Batch processing para cola
- Async/await en todas las operaciones de BD

### **Resiliencia**
- Circuit breaker para reintentos
- Backoff exponencial: 30s, 60s, 120s, 300s, 600s
- Max reintentos configurable

### **Storage**
- Comprimir firmas antes de guardar
- Limpiar firmas antiguas (> 30 días)
- Compilar SQLite con opciones de performance

## 🔒 Seguridad

### **Datos Sensibles**
- Encriptar firmas en BD local (AES-256)
- Usar SecureString para claves
- No logs de datos de firma

### **Validaciones**
- Validar tamaño de firma antes de guardar
- Sanitizar usernames
- Validar integridad de datos

## 📝 Archivos a Crear/Modificar

### **Nuevos archivos:**
```
Services/
  LocalDbService.cs
  QueueService.cs
  SyncCoordinatorService.cs

Models/
  EstadoFirma.cs (enum)
  FirmaLocal.cs
  ColaFirma.cs
  Sincronizacion.cs

ViewModels/
  SyncViewModel.cs

Tests/
  LocalDbServiceTests.cs
  QueueServiceTests.cs
  SyncCoordinatorTests.cs
```

### **Modificar:**
```
Services/FirmaService.cs
Models/Usuario.cs
Models/MainViewModel.cs
Views/MainWindow.xaml
```

## ✅ Criterios de Éxito

1. ✅ Las firmas se guardan localmente sin conexión
2. ✅ Las firmas se suben automáticamente al restablecerse la conexión
3. ✅ El usuario ve "En proceso" durante la subida
4. ✅ El usuario recibe notificación de subida completada
5. ✅ No se pierden firmas bajo ninguna circunstancia
6. ✅ Performance: < 1s para guardar localmente
7. ✅ Tests: > 80% de cobertura

---

**Este plan garantiza un sistema robusto, resiliente y user-friendly para la gestión de firmas con y sin conexión a internet.**
