using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Rest.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Template.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class TemplateValueConfigurationController : ApiBaseController
{
    private readonly ITemplateValueConfigurationService _service;
    private readonly IValueOverviewService _valueOverviewService;
    private readonly IGenericValueService _genericValueHandlingService;

    public TemplateValueConfigurationController(ITemplateValueConfigurationService service,
        IValueOverviewService valueOverviewService,
        IGenericValueService genericValueHandlingService)
    {
        _service = service;
        _valueOverviewService = valueOverviewService;
        _genericValueHandlingService = genericValueHandlingService;
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
        await _genericValueHandlingService.RecreateValues(ConfigurationType.TemplateValue, id).ConfigureAwait(false);
        return Ok(new DtoValue<int>(id));
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteConfiguration(int id)
    {
        await _service.DeleteAsync(id);
        await _genericValueHandlingService.RecreateValues(ConfigurationType.TemplateValue, id);
        return Ok();
    }
}
