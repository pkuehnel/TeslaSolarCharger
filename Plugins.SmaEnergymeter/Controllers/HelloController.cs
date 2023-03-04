using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace Plugins.SmaEnergymeter.Controllers;

public class HelloController : ApiBaseController
{
    [HttpGet]
    public Task<bool> IsAlive() => Task.FromResult(true);
}
