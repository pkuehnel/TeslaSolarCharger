using TeslaSolarCharger.Model.Enums;

namespace TeslaSolarCharger.Server.Dtos.TscBackend;

public class DtoTeslaTscDeliveryToken
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public string IdToken { get; set; }
    public TeslaFleetApiRegion Region { get; set; }
    public int ExpiresIn { get; set; }
}
