using MudBlazor;
using TeslaSolarCharger.Client.Helper.Contracts;

namespace TeslaSolarCharger.Client.Services;

public class TemplateValueConfigurationService
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
}
