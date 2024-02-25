using System.Text.Json.Serialization;

namespace Conesoft.Blazor.NetdiscoAuth;

public partial class NetatmoAuthorization
{
    public record AuthToken(
        [property: JsonPropertyName("scope")] IReadOnlyList<string> Scope,
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("expire_in")] int ExpireIn,
        [property: JsonPropertyName("refresh_token")] string RefreshToken
    );
}
