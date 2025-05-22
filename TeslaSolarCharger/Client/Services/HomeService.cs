using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Client.Services;

public class HomeService
{
    private readonly ILogger<HomeService> _logger;
    private readonly IHttpClientHelper _httpClientHelper;

    public HomeService(ILogger<HomeService> logger,
        IHttpClientHelper httpClientHelper)
    {
        _logger = logger;
        _httpClientHelper = httpClientHelper;
    }

    public async Task<List<DtoLoadPointOverview>> GetPluggedInLoadPoints()
    {
        _logger.LogTrace("{method}()", nameof(GetPluggedInLoadPoints));
        return new List<DtoLoadPointOverview>();
    }
}
