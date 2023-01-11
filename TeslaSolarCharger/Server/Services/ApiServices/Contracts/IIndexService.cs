using TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;

namespace TeslaSolarCharger.Server.Services.ApiServices.Contracts;

public interface IIndexService
{
    DtoPvValues GetPvValues();
    Task<List<DtoCarBaseStates>> GetCarBaseStatesOfEnabledCars();
    Task<string?> GetVinByCarId(int carId);
    Dictionary<int, DtoCarBaseSettings> GetCarBaseSettingsOfEnabledCars();
    void UpdateCarBaseSettings(DtoCarBaseSettings carBaseSettings);
    Dictionary<string, string> GetToolTipTexts();
}
