using System.Text.Json.Serialization;

namespace Conesoft.Blazor.NetatmoAuth;

public partial class NetatmoAuthorization
{
    class StationsData
    {
        public record UserData(string Mail);
        public record DeviceData([property:JsonPropertyName("station_name")] string StationName);
        public record BodyData(UserData User, DeviceData[] Devices);
        public record Result(BodyData Body);
    }
}
