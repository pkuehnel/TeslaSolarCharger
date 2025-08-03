using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

namespace TeslaSolarCharger.Server.Dtos;

public class DtoTempChargingDetail : ChargingDetail
{
    public int? CarId { get; set; }
    public int? ChargingConnectorId { get; set; }
}
