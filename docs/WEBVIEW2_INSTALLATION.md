# 🔧 WEBVIEW2 RUNTIME - GUÍA DE INSTALACIÓN

## ⚠️ IMPORTANTE

El Microsoft Edge WebView2 Runtime es **OBLIGATORIO** para el funcionamiento de la aplicación, específicamente para:
- ✅ Autenticación web con Keycloak
- ✅ Diálogos de login en línea
- ✅ Visualización de contenido web seguro

## 📥 MÉTODOS DE INSTALACIÓN

### **OPCIÓN A: Instalación con winget (Recomendada)** 🌟

```powershell
# Instalar automáticamente con winget
winget install Microsoft.Edge.WebView2Runtime
```

**Ventajas:**
- ✅ Instalación más rápida y sencilla
- ✅ Siempre la versión más reciente
- ✅ Gestión automática de actualizaciones
- ✅ Verificación de versión instalada:
```powershell
winget list Microsoft.Edge.WebView2Runtime
```

### **OPCIÓN B: Instalación Manual**

#### **1. Descargar el instalador oficial:**
- **Enlace directo:** https://go.microsoft.com/fwlink/?linkid=2124701
- **Archivo:** `MicrosoftEdgeWebView2RuntimeInstallerX64.exe`
- **Tamaño:** ~1.9 MB (descarga rápida)

#### **2. Ejecutar el instalador:**
```bash
# Ejecutar directamente (doble clic)
MicrosoftEdgeWebView2RuntimeInstallerX64.exe

# O desde línea de comandos (instalación silenciosa)
MicrosoftEdgeWebView2RuntimeInstallerX64.exe /silent
```

### **OPCIÓN C: Microsoft Store**

1. Abrir **Microsoft Store** en Windows
2. Buscar **"Microsoft Edge WebView2 Runtime"**
3. Hacer clic en **"Obtener"** o **"Instalar"**
4. Esperar instalación automática

## 🔍 VERIFICACIÓN

### **Método 1: Apps y características de Windows**
1. Presiona `Win + I` para abrir **Configuración**
2. Ve a **"Aplicaciones"** → **"Aplicaciones y características"**
3. Buscar **"Microsoft Edge WebView2 Runtime"**
4. **Si aparece con versión → ✅ Instalado correctamente**

### **Método 2: Línea de comandos**
```powershell
# Verificar versión instalada
Get-AppxPackage -Name "*Microsoft.WebView2*" | Select-Object Name, Version
```

### **Método 3: Panel de Control**
1. Abrir **Panel de control**
2. **"Programas y características"**
3. Buscar **"Microsoft Edge WebView2 Runtime"**

## 🛠️ SOLUCIÓN DE PROBLEMAS

### **Problema: "WebView2 Runtime no encontrado"**

**Síntomas:**
- Error al iniciar la aplicación
- Mensaje sobre WebView2 no disponible
- Login web no funciona

**Solución:**
```powershell
# Reinstalar WebView2 Runtime
winget install --force Microsoft.Edge.WebView2Runtime
```

**Si winget no funciona:**
1. Descargar instalador manual: https://go.microsoft.com/fwlink/?linkid=2124701
2. Ejecutar como administrador
3. Reiniciar la aplicación

### **Problema: "Versión incompatible"**

**Síntomas:**
- WebView2 instalado pero aplicación no lo reconoce
- Errores de versión

**Solución:**
```powershell
# Desinstalar versión existente
winget uninstall Microsoft.Edge.WebView2Runtime

# Instalar versión más reciente
winget install Microsoft.Edge.WebView2Runtime
```

### **Problema: "Instalación falla"**

**Causas comunes:**
- ❌ Permisos insuficientes (ejecutar como Administrador)
- ❌ Conexión a internet inestable
- ❌ Antivirus bloqueando instalación
- ❌ Espacio insuficiente en disco

**Solución:**
1. Ejecutar como **Administrador**
2. Deshabilitar antivirus temporalmente
3. Liberar espacio en disco (mínimo 1 GB)
4. Verificar conexión a internet

## 📋 INFORMACIÓN ADICIONAL

### **Características de WebView2 Runtime:**
- **Tamaño:** ~150-200 MB instalado
- **Requisitos:** Windows 10 o superior
- **Arquitectura:** x64 (64-bit)
- **Actualización:** Automática via Windows Update
- **Compatibilidad:** Usado por múltiples aplicaciones

### **Uso compartido:**
**⚡ Una vez instalado, WebView2 Runtime es compartido por todas las aplicaciones** que lo requieran. No es necesario reinstalarlo para cada proyecto.

### **Versiones soportadas:**
- ✅ **Windows 10** (versión 1803 o superior)
- ✅ **Windows 11** (todas las versiones)
- ❌ **Windows 8.1** o inferiores (no soportado)

### **Limpieza (si es necesario):**
```powershell
# Desinstalar completamente
winget uninstall Microsoft.Edge.WebView2Runtime

# Limpiar archivos temporales
Remove-Item "$env:LOCALAPPDATA\Microsoft\EdgeWebView2Runtime" -Recurse -Force
```

## 🎯 RESUMEN

**Para instalación rápida:**
```powershell
winget install Microsoft.Edge.WebView2Runtime
```

**Para verificar:**
```powershell
winget list Microsoft.Edge.WebView2Runtime
```

**Para problemas:**
```powershell
winget install --force Microsoft.Edge.WebView2Runtime
```

---
**Última actualización:** Julio 2026
**Versión WebView2 soportada:** 150.0.4078.48 o superior
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