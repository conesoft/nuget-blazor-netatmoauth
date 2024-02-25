using System.Text.Json.Serialization;

namespace Conesoft.Blazor.NetdiscoAuth;

public partial class NetatmoAuthorization
{
    public record AuthCode(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("state")] string State
    );
}
