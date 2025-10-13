﻿using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Helper.Contracts;

public interface INotChargingWithExpectedPowerReasonHelper
{
    void AddGenericReason(NotChargingWithExpectedPowerReasonTemplate reason);
    void AddLoadPointSpecificReason(int? carId, int? connectorId, NotChargingWithExpectedPowerReasonTemplate reason);

    Task UpdateReasonsInSettings();

    List<DtoNotChargingWithExpectedPowerReason> GetReasons(int? carId, int? connectorId);
}
