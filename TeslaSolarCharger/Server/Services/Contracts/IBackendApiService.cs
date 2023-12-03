using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IBackendApiService
{
    Task<DtoValue<string>> StartTeslaOAuth(string locale);
}
