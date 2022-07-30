using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Server.Services;

public class BaseConfigurationService : IBaseConfigurationService
{
    private readonly ILogger<BaseConfigurationService> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly JobManager _jobManager;

    public BaseConfigurationService(ILogger<BaseConfigurationService> logger, IConfigurationWrapper configurationWrapper, JobManager jobManager)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _jobManager = jobManager;
    }

    public async Task UpdateBaseConfigurationAsync(DtoBaseConfiguration baseConfiguration)
    {
        await _jobManager.StopJobs().ConfigureAwait(false);
        await _configurationWrapper.UpdateBaseConfigurationAsync(baseConfiguration).ConfigureAwait(false);
        await _jobManager.StartJobs().ConfigureAwait(false);
    }
}