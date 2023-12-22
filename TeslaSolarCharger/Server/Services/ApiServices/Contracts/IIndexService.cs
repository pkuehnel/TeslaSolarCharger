using TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services.ApiServices.Contracts;

public interface IIndexService
{
    DtoPvValues GetPvValues();
    Task<List<DtoCarBaseStates>> GetCarBaseStatesOfEnabledCars();
    Task<string?> GetVinByCarId(int carId);
    Dictionary<int, DtoCarBaseSettings> GetCarBaseSettingsOfEnabledCars();
    Task UpdateCarBaseSettings(DtoCarBaseSettings carBaseSettings);
    Dictionary<string, string> GetToolTipTexts();
    DtoCarTopicValues GetCarDetails(int carId);
    List<DtoChargingSlot> RecalculateAndGetChargingSlots(int carId);
    List<DtoChargingSlot> GetChargingSlots(int carId);
    Task ResetVehicleNotPaired();
}
