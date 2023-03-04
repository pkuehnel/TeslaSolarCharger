using TeslaSolarCharger.SharedBackend.Dtos;

namespace TeslaSolarCharger.SharedBackend.Contracts;

public interface ICurrentValuesService
{
    Task<DtoCurrentPvValues> GetCurrentPvValues();
}
