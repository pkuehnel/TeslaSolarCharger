using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Helper.Contracts;

public interface ICarPropertyUpdateHelper
{
    void UpdateDtoCarProperty(DtoCar car, CarValueLog carValueLog);
}
