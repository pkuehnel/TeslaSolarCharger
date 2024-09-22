using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;
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
}
