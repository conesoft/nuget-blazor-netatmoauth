﻿using System.Text.Json.Serialization;

namespace Conesoft.Blazor.NetatmoAuth;

public partial class NetatmoAuthorization
{
    public record AuthToken(
        [property: JsonPropertyName("scope")] IReadOnlyList<string> Scope,
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string RefreshToken
    );
}
