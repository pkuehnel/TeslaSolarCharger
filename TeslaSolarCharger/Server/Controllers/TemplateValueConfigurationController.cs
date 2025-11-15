using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.Template.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class TemplateValueConfigurationController : ApiBaseController
{
    private readonly ITemplateValueConfigurationService _service;
    private readonly IValueOverviewService _valueOverviewService;
    private readonly IRefreshableValueHandlingService _refreshableValueHandlingService;

    public TemplateValueConfigurationController(ITemplateValueConfigurationService service,
        IValueOverviewService valueOverviewService,
        IRefreshableValueHandlingService refreshableValueHandlingService)
    {
        _service = service;
        _valueOverviewService = valueOverviewService;
        _refreshableValueHandlingService = refreshableValueHandlingService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTemplateValueOverviews()
    {
        var values = await _valueOverviewService.GetTemplateValueOverviews().ConfigureAwait(false);
        return Ok(values);
    }

    [HttpGet]
    public async Task<IActionResult> GetConfiguration(int id)
    {
        var configuration = await _service.GetAsync(id).ConfigureAwait(false);
        return Ok(configuration);
    }


    [HttpPost]
    public async Task<IActionResult> SaveConfiguration(DtoTemplateValueConfigurationBase configuration)
    {
        var id = await _service.SaveAsync(configuration).ConfigureAwait(false);
        await _refreshableValueHandlingService.RecreateRefreshables(ConfigurationType.TemplateValue, id).ConfigureAwait(false);
        return Ok(new DtoValue<int>(id));
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteConfiguration(int id)
    {
        await _service.DeleteAsync(id);
        await _refreshableValueHandlingService.RecreateRefreshables(ConfigurationType.TemplateValue, id);
        return Ok();
    }
}
