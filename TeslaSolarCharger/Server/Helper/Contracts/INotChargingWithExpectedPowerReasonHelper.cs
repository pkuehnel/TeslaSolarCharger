using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Localization;

namespace TeslaSolarCharger.Server.Helper.Contracts;

public interface INotChargingWithExpectedPowerReasonHelper
{
    void AddGenericReason(LocalizedText reason, params object[] formatArguments);

    void AddLoadPointSpecificReason(int? carId, int? connectorId, LocalizedText reason, DateTimeOffset? reasonEndTime = null, params object[] formatArguments);

    Task UpdateReasonsInSettings();

    List<DtoNotChargingWithExpectedPowerReason> GetReasons(int? carId, int? connectorId);
}
