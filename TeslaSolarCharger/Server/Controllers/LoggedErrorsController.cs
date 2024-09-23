using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.SharedBackend.Abstracts;
using TeslaSolarCharger.SharedBackend.Extensions;

namespace TeslaSolarCharger.Server.Controllers;

public class LoggedErrorsController(IErrorHandlingService service) : ApiBaseController
{
    [HttpGet]
    public async Task<IActionResult> GetActiveLoggedErrors()
    {
        var result = await service.GetActiveLoggedErrors().ConfigureAwait(false);
        return result.ToOk();

    }

    [HttpPost]
    public async Task<IActionResult> DismissError([FromBody] DtoValue<int> errorId)
    {
        var result = await service.DismissError(errorId.Value);
        return result.ToOk();
    }
}
