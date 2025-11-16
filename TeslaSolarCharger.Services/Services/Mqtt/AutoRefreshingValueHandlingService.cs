using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Services.Services.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;

namespace TeslaSolarCharger.Services.Services.Mqtt;

public class AutoRefreshingValueHandlingService : GenericValueHandlingServiceBase<IAutoRefreshingValue<decimal>, decimal, int>, IDecimalValueHandlingService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AutoRefreshingValueHandlingService> _logger;

    public AutoRefreshingValueHandlingService(ILogger<AutoRefreshingValueHandlingService> logger,
        IServiceScopeFactory serviceScopeFactory) : base(serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public override async Task RecreateValues(ConfigurationType? configurationType, params List<int> configurationIds)
    {
        _logger.LogTrace("{method}({configurationType}, {@confgiurationIds})", nameof(RecreateValues), configurationType, configurationIds);
        var valuesSnapshot = GetGenericValuesSnapshot();

        var valuesToCancel = valuesSnapshot
            .Where(r => configurationType == default || r.SourceValueKey.ConfigurationType == configurationType)
            .Where(r => configurationIds.Count == 0 || configurationIds.Contains(r.SourceValueKey.SourceId))
            .ToList();

        foreach (var valueToCancel in valuesToCancel)
        {
            valueToCancel.Cancel();
        }

        await RemoveValuesAsync(valuesToCancel);

        using var setupScope = _serviceScopeFactory.CreateScope();
        var autoRefreshungSetupServices = setupScope.ServiceProvider.GetServices<IAutoRefreshingValueSetupService>();
        var newAutoRefreshings = new List<IAutoRefreshingValue<decimal>>();
        foreach (var autoRefreshungSetupService in autoRefreshungSetupServices)
        {
            if (configurationType != default && autoRefreshungSetupService.ConfigurationType != configurationType)
            {
                continue;
            }
            var autoRefreshings = await autoRefreshungSetupService.GetDecimalAutoRefreshingValuesAsync(configurationIds);
            newAutoRefreshings.AddRange(autoRefreshings);
        }
        AddGenericValues(newAutoRefreshings);
    }
}
