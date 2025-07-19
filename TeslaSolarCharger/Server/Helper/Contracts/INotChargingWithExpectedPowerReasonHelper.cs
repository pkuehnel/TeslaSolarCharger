using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Helper.Contracts;

public interface INotChargingWithExpectedPowerReasonHelper
{
    void AddGenericReason(DtoNotChargingWithExpectedPowerReason reason);
    void AddLoadPointSpecificReason(int? carId, int? connectorId, DtoNotChargingWithExpectedPowerReason reason);

    void UpdateReasonsInSettings();

    List<DtoNotChargingWithExpectedPowerReason> GetReasons(int? carId, int? connectorId);
}
