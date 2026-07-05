using FirmasApp.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace FirmasApp.Services;

public class UsuarioService
{
    private readonly HttpClient _httpClient;
    private readonly GedsysApiSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly KeycloakAuthService _keycloakAuth;
    private readonly FirmaService _firmaService;

    public UsuarioService(GedsysApiSettings settings, KeycloakAuthService keycloakAuth, FirmaService firmaService)
    {
        _settings = settings;
        _keycloakAuth = keycloakAuth;
        _firmaService = firmaService;
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

    public async Task<PaginatedResult<Usuario>> GetUsuariosAsync(string? busqueda = null, int page = 0, int size = 50)
    {
        try
        {
            Console.WriteLine($"[DEBUG UsuarioService] GetUsuariosAsync iniciado - page: {page}, size: {size}");

            if (!await AddAuthHeaderAsync())
            {
                throw new Exception("No hay token de autenticación válido");
            }

            // Usar el endpoint correcto: /core/api/v1/empleados
            var endpoint = "/core/api/v1/empleados";

            // Construir query parameters
            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                // Buscar por múltiples campos según la API del Angular
                queryParams.Add($"nombres={Uri.EscapeDataString(busqueda)}");
            }

            // Agregar parámetros de paginación
            queryParams.Add($"page={page}");
            queryParams.Add($"size={size}");

            if (queryParams.Any())
            {
                endpoint += "?" + string.Join("&", queryParams);
            }

            Console.WriteLine($"[DEBUG UsuarioService] Endpoint: {endpoint}");
            var response = await _httpClient.GetAsync(endpoint);
            Console.WriteLine($"[DEBUG UsuarioService] Response status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG UsuarioService] Response content length: {content.Length}");

                // La API devuelve una respuesta paginada: { content: [...], totalElements: X, ... }
                var paginatedResult = JsonSerializer.Deserialize<PaginatedResult<Usuario>>(content, _jsonOptions)
                    ?? new PaginatedResult<Usuario>();

                var usuarios = paginatedResult.Content ?? new List<Usuario>();
                Console.WriteLine($"[DEBUG UsuarioService] Usuarios deserializados: {usuarios.Count}");

                if (usuarios.Any())
                {
                    Console.WriteLine($"[DEBUG UsuarioService] Verificando estado de firmas para {usuarios.Count} usuarios");
                    // Verificar estado de firma para cada usuario
                    await VerificarEstadoFirmasAsync(usuarios);
                    Console.WriteLine($"[DEBUG UsuarioService] Verificación de firmas completada");
                }

                // Actualizar el contenido con usuarios verificados
                paginatedResult.Content = usuarios;
                paginatedResult.Page = page;
                paginatedResult.Size = size;

                Console.WriteLine($"[DEBUG UsuarioService] Retornando {usuarios.Count} usuarios, total: {paginatedResult.TotalElements}");
                return paginatedResult;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG UsuarioService] Error 401: {errorContent}");
                throw new Exception("Error de autenticación. Token inválido o expirado");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG UsuarioService] Error {response.StatusCode}: {errorContent}");
                throw new Exception($"Error al obtener usuarios: {response.StatusCode} - {response.ReasonPhrase}. Detalles: {errorContent}");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[DEBUG UsuarioService] HttpRequestException: {ex.Message}");
            throw new Exception($"Error de conexión al backend gedsys2: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"[DEBUG UsuarioService] TaskCanceledException: {ex.Message}");
            throw new Exception("Timeout al conectar con el backend gedsys2", ex);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG UsuarioService] Exception: {ex.Message}");
            throw new Exception($"Error inesperado al obtener usuarios: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Verifica el estado de firma de cada usuario en paralelo
    /// </summary>
    private async Task VerificarEstadoFirmasAsync(List<Usuario> usuarios)
    {
        if (usuarios == null || usuarios.Count == 0)
        {
            return;
        }

        // Crear tareas para verificar firma de cada usuario en paralelo
        var tareas = usuarios.Select(async usuario =>
        {
            try
            {
                // Asumimos que el username está en el formato del documento o email
                var username = ObtenerUsernameDesdeUsuario(usuario);
                if (!string.IsNullOrWhiteSpace(username))
                {
                    Console.WriteLine($"[DEBUG UsuarioService] Verificando firma para {usuario.NombreCompleto} -> username: '{username}'");
                    var tieneFirma = await _firmaService.TieneFirmaAsync(username);
                    Console.WriteLine($"[DEBUG UsuarioService] Resultado para {username}: {(tieneFirma ? "TIENE FIRMA" : "NO TIENE FIRMA")}");
                    usuario.TieneFirma = tieneFirma;
                }
                else
                {
                    Console.WriteLine($"[DEBUG UsuarioService] No se pudo determinar username para {usuario.NombreCompleto}");
                    usuario.TieneFirma = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UsuarioService] Error verificando firma de {usuario.NombreCompleto}: {ex.Message}");
                usuario.TieneFirma = false; // Asumir que no tiene firma si hay error
            }
        });

        await Task.WhenAll(tareas);

        Console.WriteLine($"[DEBUG UsuarioService] Resumen de firmas:");
        foreach (var usuario in usuarios)
        {
            var username = ObtenerUsernameDesdeUsuario(usuario);
            Console.WriteLine($"  - {usuario.NombreCompleto} ({username}): {(usuario.TieneFirma ? "✓ TIENE" : "✗ NO TIENE")}");
        }
    }

    /// <summary>
    /// Extrae el username de un usuario (puede ser documento, email, o un campo específico)
    /// </summary>
    private string? ObtenerUsernameDesdeUsuario(Usuario usuario)
    {
        // Prioridad: CuentaUsuario > Email > Documento > Nombre
        if (!string.IsNullOrWhiteSpace(usuario.CuentaUsuario))
        {
            return usuario.CuentaUsuario;
        }

        if (!string.IsNullOrWhiteSpace(usuario.Email))
        {
            // Extraer username del email (parte antes del @)
            var emailParts = usuario.Email.Split('@');
            return emailParts[0];
        }

        if (!string.IsNullOrWhiteSpace(usuario.Documento))
        {
            return usuario.Documento;
        }

        // Si no hay cuenta, email ni documento, crear un username basado en el nombre
        if (!string.IsNullOrWhiteSpace(usuario.Nombres))
        {
            return usuario.Nombres.ToLower().Replace(" ", ".").Normalize();
        }

        return null;
    }

    public async Task<Usuario?> GetUsuarioAsync(string id)
    {
        try
        {
            if (!await AddAuthHeaderAsync())
            {
                throw new Exception("No hay token de autenticación válido");
            }

            var response = await _httpClient.GetAsync($"/core/api/v1/empleados/{id}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Usuario>(content, _jsonOptions);
            }

            return null;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al obtener usuario {id}: {ex.Message}", ex);
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            if (!await AddAuthHeaderAsync())
            {
                return false;
            }

            var response = await _httpClient.GetAsync("/core/api/v1/empleados?page=0&size=1");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public class PaginatedResult<T>
{
    public List<T> Content { get; set; } = new();
    public int Page { get; set; }
    public int Size { get; set; }
    public int TotalElements { get; set; }
    public int TotalPages { get; set; }
}
