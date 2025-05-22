using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Client.Services;

public class HomeService : IHomeService
{
    private readonly ILogger<HomeService> _logger;
    private readonly IHttpClientHelper _httpClientHelper;

    public HomeService(ILogger<HomeService> logger,
        IHttpClientHelper httpClientHelper)
    {
        _logger = logger;
        _httpClientHelper = httpClientHelper;
    }

    public async Task<List<DtoLoadPointOverview>?> GetPluggedInLoadPoints()
    {
        _logger.LogTrace("{method}()", nameof(GetPluggedInLoadPoints));
        var result = await _httpClientHelper.SendGetRequestAsync<List<DtoLoadPointOverview>>("api/Home/GetLoadPointOverviews");
        if (result.HasError)
        {
            _logger.LogError(result.ErrorMessage);
        }
        return result.Data;
    }
}
