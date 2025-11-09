using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Services.Services.Template.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.TemplateConfiguration.Sma;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class TemplateValueConfigurationController : ApiBaseController
{
    private readonly ITemplateValueConfigurationService _service;

    public TemplateValueConfigurationController(ITemplateValueConfigurationService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> SaveSmaInverterTemplate(DtoSmaInverterTemplateValueConfiguration configuration)
    {
        var id = await _service.SaveAsync(configuration);
        return Ok(new DtoValue<int>(id));
    }
}
