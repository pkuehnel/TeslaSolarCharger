using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.SharedBackend.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class TscConfigurationService : ITscConfigurationService
{
    private readonly ILogger<TscConfigurationService> _logger;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly IConstants _constants;

    public TscConfigurationService(ILogger<TscConfigurationService> logger, ITeslaSolarChargerContext teslaSolarChargerContext, IConstants constants)
    {
        _logger = logger;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _constants = constants;
    }

    public async Task<Guid> GetInstallationId()
    {
        _logger.LogTrace("{method}()", nameof(GetInstallationId));
        var configurationIdString = _teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == _constants.InstallationIdKey)
            .Select(c => c.Value)
            .FirstOrDefault();

        if (configurationIdString != default)
        {
            return Guid.Parse(configurationIdString);
        }

        var installationIdConfiguration = new TscConfiguration()
        {
            Key = _constants.InstallationIdKey,
            Value = Guid.NewGuid().ToString(),
        };
        _teslaSolarChargerContext.TscConfigurations.Add(installationIdConfiguration);
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        return Guid.Parse(installationIdConfiguration.Value);
    }
}
