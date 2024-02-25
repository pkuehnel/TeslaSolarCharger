using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Contracts;

public interface IConfigService
{
    ISettings GetSettings();
    Task<List<CarBasicConfiguration>> GetCarBasicConfigurations();
}
