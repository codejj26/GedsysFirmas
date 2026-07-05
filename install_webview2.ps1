# Instalar WebView2 Runtime - Ejecutar como Administrador

Write-Host "Descargando WebView2 Runtime installer..." -ForegroundColor Cyan
$installerUrl = "https://go.microsoft.com/fwlink/?linkid=2124701"
$installerPath = "$env:TEMP\WebView2Installer.exe"

# Descargar usando .NET WebClient
$webClient = New-Object System.Net.WebClient
$webClient.DownloadFile($installerUrl, $installerPath)

Write-Host "Instalando WebView2 Runtime..." -ForegroundColor Green
Start-Process $installerPath -ArgumentList "/silent", "/install" -Wait

Write-Host "Verificando instalación..." -ForegroundColor Yellow
Start-Sleep -Seconds 2

# Verificar instalación
$regPath = "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
if (Test-Path $regPath) {
    $version = (Get-ItemProperty $regPath -ErrorAction SilentlyContinue).pv
    Write-Host "✅ WebView2 Runtime instalado correctamente" -ForegroundColor Green
    if ($version) {
        Write-Host "Versión: $version" -ForegroundColor Cyan
    }
} else {
    Write-Host "⚠️ No se pudo verificar la instalación. Reinicia tu computadora." -ForegroundColor Yellow
}

# Limpiar
Remove-Item $installerPath -Force -ErrorAction SilentlyContinue

Write-Host "Por favor, reinicia la aplicación FirmasApp." -ForegroundColor Cyan
