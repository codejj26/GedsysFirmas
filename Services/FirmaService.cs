using FirmasApp.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FirmasApp.Services;

public class FirmaService
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakAuthService _keycloakAuth;
    private readonly GedsysApiSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SyncCoordinatorService _syncCoordinator;
    private readonly LocalDbService _localDb;

    public FirmaService(
        GedsysApiSettings settings,
        KeycloakAuthService keycloakAuth,
        SyncCoordinatorService syncCoordinator,
        LocalDbService localDb)
    {
        _settings = settings;
        _keycloakAuth = keycloakAuth;
        _syncCoordinator = syncCoordinator;
        _localDb = localDb;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(settings.BaseUrl),
            Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    private async Task<bool> AddAuthHeaderAsync()
    {
        var token = await _keycloakAuth.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        return true;
    }

    /// <summary>
    /// Obtiene la firma de un usuario como dataURL (imagen base64)
    /// Primero verifica BD local, luego servidor
    /// GET /core/api/v1/firmas-almacenadas/usuarios/{username}
    /// </summary>
    public async Task<string?> ObtenerFirmaComoDataUrlAsync(string username)
    {
        try
        {
            // Primero verificar BD local
            var firmaLocal = await _localDb.ObtenerAsync(username);
            if (firmaLocal != null && !string.IsNullOrWhiteSpace(firmaLocal.FirmaDataUrl))
            {
                return firmaLocal.FirmaDataUrl;
            }

            // Si no está en local, intentar del servidor
            if (!await AddAuthHeaderAsync())
            {
                throw new Exception("No hay token de autenticación válido");
            }

            var response = await _httpClient.GetAsync($"/core/api/v1/firmas-almacenadas/usuarios/{Uri.EscapeDataString(username)}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null; // Usuario no tiene firma almacenada
            }

            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var base64 = Convert.ToBase64String(bytes);
            var dataUrl = $"data:image/png;base64,{base64}";

            // Guardar en BD local para futuros usos
            await _localDb.GuardarAsync(username, username, dataUrl, EstadoFirma.ConFirma);

            return dataUrl;
        }
        catch (HttpRequestException ex)
        {
            // Si falla la conexión, verificar si hay versión local
            var firmaLocal = await _localDb.ObtenerAsync(username);
            if (firmaLocal != null && !string.IsNullOrWhiteSpace(firmaLocal.FirmaDataUrl))
            {
                return firmaLocal.FirmaDataUrl;
            }
            throw new Exception($"Error de conexión al obtener firma: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al obtener firma de {username}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Verifica si un usuario tiene firma almacenada
    /// </summary>
    public async Task<bool> TieneFirmaAsync(string username)
    {
        try
        {
            if (!await AddAuthHeaderAsync())
            {
                return false;
            }

            var response = await _httpClient.GetAsync($"/core/api/v1/firmas-almacenadas/usuarios/{Uri.EscapeDataString(username)}");
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Guarda o actualiza la firma de un usuario usando el sistema de sincronización
    /// PUT /core/api/v1/firmas-almacenadas/usuarios/{username}
    /// </summary>
    public async Task<FirmaAlmacenadaInfo?> GuardarFirmaAsync(string username, string nombreCompleto, string dataUrl)
    {
        try
        {
            // Usar el coordinador de sincronización que maneja online/offline
            var exito = await _syncCoordinator.SincronizarFirmaAsync(username, nombreCompleto, dataUrl);

            if (exito)
            {
                // Firma guardada exitosamente (subida al servidor)
                return new FirmaAlmacenadaInfo
                {
                    Username = username,
                    MimeType = "image/png",
                    TamanoBytes = dataUrl.Length / 2 // Estimación aprox para base64
                };
            }
            else
            {
                // Firma guardada localmente (en cola para sincronización)
                return new FirmaAlmacenadaInfo
                {
                    Username = username,
                    MimeType = "image/png",
                    TamanoBytes = dataUrl.Length / 2 // Estimación aprox
                };
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al guardar firma de {username}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Elimina la firma almacenada de un usuario usando el sistema de sincronización
    /// DELETE /core/api/v1/firmas-almacenadas/usuarios/{username}
    /// </summary>
    public async Task<bool> EliminarFirmaAsync(string username, string nombreCompleto)
    {
        try
        {
            // Primero intentar eliminar del servidor si hay conexión
            if (await _syncCoordinator.VerificarConexionAsync())
            {
                if (!await AddAuthHeaderAsync())
                {
                    throw new Exception("No hay token de autenticación válido");
                }

                var response = await _httpClient.DeleteAsync($"/core/api/v1/firmas-almacenadas/usuarios/{Uri.EscapeDataString(username)}");

                if (response.IsSuccessStatusCode)
                {
                    // Eliminar también de BD local
                    await _localDb.EliminarAsync(username);
                    return true;
                }
            }

            // Si no hay conexión o falló, encolar la eliminación
            await _syncCoordinator.EncolarEliminacionAsync(username, nombreCompleto);
            return false;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al eliminar firma de {username}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Convierte un dataURL a base64 puro y mimeType para enviar al servidor
    /// </summary>
    private (string contenido, string mimeType) DataUrlToBase64(string dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            throw new ArgumentException("DataURL inválido");
        }

        // Formato esperado: data:image/png;base64,iVBORw0KG...
        var match = System.Text.RegularExpressions.Regex.Match(dataUrl, @"^data:([^;]+);base64,(.+)$");

        if (!match.Success)
        {
            throw new ArgumentException("DataURL formato inválido. Se espera: data:image/png;base64,...");
        }

        var mimeType = match.Groups[1].Value;
        var base64 = match.Groups[2].Value;

        return (base64, mimeType);
    }

    /// <summary>
    /// Convierte bytes de imagen a dataURL
    /// </summary>
    public string BytesToDataUrl(byte[] bytes, string mimeType = "image/png")
    {
        var base64 = Convert.ToBase64String(bytes);
        return $"data:{mimeType};base64,{base64}";
    }
}

public class FirmaAlmacenadaInfo
{
    public string Username { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
}