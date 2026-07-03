# 🔧 WEBVIEW2 RUNTIME - GUÍA DE INSTALACIÓN

## ⚠️ IMPORTANTE

El Microsoft Edge WebView2 Runtime **NO está incluido** en el repositorio `GedsysFirmas` debido a limitaciones de tamaño de archivos de GitHub:

- ❌ `msedge.dll` (315.97 MB) - Supera el límite de 100MB de GitHub
- ❌ `MicrosoftEdgeWebView2RuntimeInstallerX64.exe` (194.26 MB) - Supera el límite

## 📥 INSTALACIÓN REQUERIDA

**El WebView2 Runtime es OBLIGATORIO** para el funcionamiento de la aplicación, específicamente para:
- ✅ Autenticación web con Keycloak
- ✅ Diálogos de login en línea
- ✅ Visualización de contenido web seguro

## 🎯 MÉTODOS DE INSTALACIÓN

### **OPCIÓN A: Instalación Automática (Recomendada)** 🌟

```powershell
# Ejecutar el script de instalación incluido
.\Tools\Install-WebView2Runtime.ps1
```

**Este script:**
- Descarga automáticamente la versión más reciente
- Verifica si ya está instalado
- Realiza instalación silenciosa
- Confirma la instalación exitosa

### **OPCIÓN B: Instalación Manual**

#### **1. Descargar el instalador oficial:**
- **Enlace directo:** https://go.microsoft.com/fwlink/?linkid=2124701
- **Archivo:** `MicrosoftEdgeWebView2RuntimeInstallerX64.exe`
- **Tamaño:** ~1.9 MB (descarga rápida)

#### **2. Ejecutar el instalador:**
```bash
# Opción A: Línea de comandos
.\Tools\MicrosoftEdgeWebView2RuntimeInstallerX64.exe /silent

# Opción B: Interfaz gráfica (doble clic)
.\Tools\MicrosoftEdgeWebView2RuntimeInstallerX64.exe
```

#### **3. Verificar instalación:**
```bash
# Ejecutar script de verificación
.\Tools\Test-WebView2Runtime.ps1
```

### **OPCIÓN C: Instalación vía winget (Recomendado para desarrolladores)**

```bash
# Instalar via winget
winget install Microsoft.Edge.WebView2Runtime

# Verificar instalación
winget list Microsoft.Edge.WebView2Runtime
```

## ✅ VERIFICACIÓN DE INSTALACIÓN

Después de la instalación, verifica que la aplicación funcione correctamente:

1. **Ejecutar la aplicación:**
   ```bash
   dotnet run
   ```

2. **Probar el login:**
   - Si aparece el diálogo de login web → ✅ WebView2 instalado correctamente
   - Si hay errores sobre WebView2 → ❌ Reinstalar el runtime

3. **Verificar versión instalada:**
   - Abrir **Panel de Control** → **Programas y características**
   - Buscar **Microsoft Edge WebView2 Runtime**
   - Versión recomendada: **150.0.4078.48** o superior

## 🛠️ SOLUCIÓN DE PROBLEMAS

### **Problema: "WebView2 no está disponible"**
```bash
# Solución 1: Reinstalar manualmente
.\Tools\MicrosoftEdgeWebView2RuntimeInstallerX64.exe

# Solución 2: Instalar última versión vía winget
winget install --force Microsoft.Edge.WebView2Runtime
```

### **Problema: "La aplicación requiere una versión más reciente"**
```bash
# Desinstalar versión anterior
winget uninstall Microsoft.Edge.WebView2Runtime

# Instalar versión más reciente
winget install Microsoft.Edge.WebView2Runtime
```

### **Problema: Errores durante el script de instalación**
```bash
# Verificar conexión a internet
Test-NetConnection google.com

# Ejecutar como Administrador
# Clic derecho → "Ejecutar como administrador"
```

## 📋 NOTAS PARA DESARROLLADORES

### **Para incluir en instalaciones desplegadas:**

Puedes incluir el runtime como parte de tu instalador usando:

```xml
<!-- En tu instalador setup.exe -->
<PackageGroup>
    <PackageGroup>
        <PackageName>Microsoft Edge WebView2 Runtime</PackageName>
    </PackageGroup>
</PackageGroup>
```

### **Bootstrapper script:**

```powershell
# Agregar a tu script de instalación
Write-Host "Instalando WebView2 Runtime..."

# Verificar si está instalado
$webView2Installed = Get-AppxPackage -Name "*Microsoft.WebView2*" -ErrorAction SilentlyContinue

if (-not $webView2Installed) {
    Write-Host "Descargando WebView2 Runtime..."
    Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/?linkid=2124701" -OutFile "WebView2Installer.exe"

    Write-Host "Instalando WebView2 Runtime..."
    Start-Process "WebView2Installer.exe" -ArgumentList "/silent" -Wait
}
```

## 🔗 RECURSOS ADICIONALES

- **Documentación oficial:** https://learn.microsoft.com/en-us/microsoft-edge/webview2/
- **Release notes:** https://blogs.windows.com/windowsdeveloper/2022/09/20/
- **Soporte técnico:** https://stackoverflow.com/questions/tagged/webview2

## ⚡ RENDIMIENTO DE INSTALACIÓN

**El WebView2 Runtime se instala una vez y puede ser usado por todas las aplicaciones** que lo requieran. No es necesario reinstalarlo para cada proyecto.

**Tamaño aproximado después de la instalación:** ~150-200 MB

**Versiones compatibles:**
- Windows 10 1803+
- Windows 11 (todas las versiones)
- Windows Server 2016+