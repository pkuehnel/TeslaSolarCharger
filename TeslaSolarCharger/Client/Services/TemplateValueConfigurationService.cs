using MudBlazor;
using TeslaSolarCharger.Client.Dtos;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;

namespace TeslaSolarCharger.Client.Services;

public class TemplateValueConfigurationService : ITemplateValueConfigurationService
{
    private readonly ILogger<TemplateValueConfigurationService> _logger;
    private readonly IHttpClientHelper _httpClientHelper;
    private readonly ISnackbar _snackbar;

    public TemplateValueConfigurationService(ILogger<TemplateValueConfigurationService> logger,
        IHttpClientHelper httpClientHelper,
        ISnackbar snackbar)
    {
        _logger = logger;
        _httpClientHelper = httpClientHelper;
        _snackbar = snackbar;
    }


    public async Task<Result<DtoTemplateValueConfigurationBase>> GetAsync(int id)
    {
        _logger.LogTrace("{method}({id})", nameof(GetAsync), id);
        var result = await _httpClientHelper.SendGetRequestAsync<DtoTemplateValueConfigurationBase>($"api/TemplateValueConfiguration/GetConfiguration?id={id}");
        if (result.HasError)
        {
            _snackbar.Add($"Could not load configuration: {result.ErrorMessage}");
        }
        return result;
    }

    public async Task<List<DtoValueConfigurationOverview>> GetOverviews()
    {
        _logger.LogTrace("{method}()", nameof(GetOverviews));
        var result = await _httpClientHelper.SendGetRequestAsync<List<DtoValueConfigurationOverview>>($"api/TemplateValueConfiguration/GetTemplateValueOverviews");
        if (result.HasError)
        {
            _snackbar.Add($"Could not load configuration: {result.ErrorMessage}");
        }
        return result.Data ?? new();
    }
}
