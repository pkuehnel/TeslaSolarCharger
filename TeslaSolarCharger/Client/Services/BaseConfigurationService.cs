using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Client.Services;

public class BaseConfigurationService : IBaseConfigurationService
{
    private readonly ILogger<BaseConfigurationService> _logger;
    private readonly IHttpClientHelper _httpClientHelper;

    public BaseConfigurationService(ILogger<BaseConfigurationService> logger,
        IHttpClientHelper httpClientHelper)
    {
        _logger = logger;
        _httpClientHelper = httpClientHelper;
    }

    public async Task<bool> HomeBatteryValuesAvailable()
    {
        _logger.LogTrace("{method}()", nameof(HomeBatteryValuesAvailable));
        var result = await _httpClientHelper.SendGetRequestWithSnackbarAsync<DtoValue<bool>>("api/BaseConfiguration/HomeBatteryValuesAvailable").ConfigureAwait(false);
        return result?.Value == true;
    }
}
