using TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.ApiServices.Contracts;

public interface IIndexService
{
    Task<DtoPvValues> GetPvValues();
    Task UpdateCarFleetApiState(int carId, TeslaCarFleetApiState fleetApiState);
}
