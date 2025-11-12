using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.Template.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class TemplateValueConfigurationController : ApiBaseController
{
    private readonly ITemplateValueConfigurationService _service;
    private readonly IValueOverviewService _valueOverviewService;

    public TemplateValueConfigurationController(ITemplateValueConfigurationService service, IValueOverviewService valueOverviewService)
    {
        _service = service;
        _valueOverviewService = valueOverviewService;
    }
    [HttpGet]
    public async Task<ActionResult<ITemplateValueConfigurationDto>> GetTemplateValueConfiguration(int id)
    {
        var configuration = await _service.GetAsync(id);
        if (configuration == null)
        {
            return NotFound();
        }

        return Ok(configuration);
    }


    [HttpPost]
    public async Task<IActionResult> SaveSmaInverterTemplate(DtoSmaInverterTemplateValueConfiguration configuration)
    {
        var id = await _service.SaveAsync(configuration);
        return Ok(new DtoValue<int>(id));
    }

    [HttpPost]
    public async Task<IActionResult> SaveSmaHybridTemplate(DtoSmaHybridInverterTemplateValueConfiguration configuration)
    {
        var id = await _service.SaveAsync(configuration);
        return Ok(new DtoValue<int>(id));
    }
}
