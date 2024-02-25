using System.Text.Json.Serialization;

namespace Conesoft.Blazor.NetdiscoAuth;

public partial class NetatmoAuthorization
{
    public record AuthState(
        [property: JsonPropertyName("state")] string State
    );
}
