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

    public FirmaService(GedsysApiSettings settings, KeycloakAuthService keycloakAuth)
    {
        _settings = settings;
        _keycloakAuth = keycloakAuth;
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
    /// GET /core/api/v1/firmas-almacenadas/usuarios/{username}
    /// </summary>
    public async Task<string?> ObtenerFirmaComoDataUrlAsync(string username)
    {
        try
        {
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
            return $"data:image/png;base64,{base64}";
        }
        catch (HttpRequestException ex)
        {
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
    /// Guarda o actualiza la firma de un usuario
    /// PUT /core/api/v1/firmas-almacenadas/usuarios/{username}
    /// </summary>
    public async Task<FirmaAlmacenadaInfo?> GuardarFirmaAsync(string username, string dataUrl)
    {
        try
        {
            if (!await AddAuthHeaderAsync())
            {
                throw new Exception("No hay token de autenticación válido");
            }

            var (contenido, mimeType) = DataUrlToBase64(dataUrl);

            var payload = new
            {
                contenido,
                mimeType
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"/core/api/v1/firmas-almacenadas/usuarios/{Uri.EscapeDataString(username)}", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<FirmaAlmacenadaInfo>(responseContent, _jsonOptions);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error guardando firma: {response.StatusCode} - {errorContent}");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Error de conexión al guardar firma: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al guardar firma de {username}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Elimina la firma almacenada de un usuario
    /// DELETE /core/api/v1/firmas-almacenadas/usuarios/{username}
    /// </summary>
    public async Task<bool> EliminarFirmaAsync(string username)
    {
        try
        {
            if (!await AddAuthHeaderAsync())
            {
                throw new Exception("No hay token de autenticación válido");
            }

            var response = await _httpClient.DeleteAsync($"/core/api/v1/firmas-almacenadas/usuarios/{Uri.EscapeDataString(username)}");
            return response.IsSuccessStatusCode;
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