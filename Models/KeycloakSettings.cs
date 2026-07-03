using System.Text.Json.Serialization;

namespace FirmasApp.Models;

public class KeycloakSettings
{
    public string Url { get; set; } = "https://keycloak.gedsys.co";
    public string Realm { get; set; } = "development";
    public string ClientId { get; set; } = "gedsys-firmas";
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "firmasapp://callback";
    public string AuthorizationEndpoint { get; set; } = "/protocol/openid-connect/auth";
    public string TokenEndpoint { get; set; } = "/protocol/openid-connect/token";
    public string Scope { get; set; } = "openid profile email";
}

public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("id_token")]
    public string IdToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;

    public DateTime ExpiresAt => DateTime.UtcNow.AddSeconds(ExpiresIn - 30); // 30s de margen para refresh
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

public class UserInfo
{
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("preferred_username")]
    public string PreferredUsername { get; set; } = string.Empty;

    [JsonPropertyName("given_name")]
    public string GivenName { get; set; } = string.Empty;

    [JsonPropertyName("family_name")]
    public string FamilyName { get; set; } = string.Empty;
}
