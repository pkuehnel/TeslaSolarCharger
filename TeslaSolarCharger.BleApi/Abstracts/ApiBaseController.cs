using Microsoft.AspNetCore.Mvc;

namespace TeslaSolarCharger.BleApi.Abstracts;

[Route("api/[controller]/[action]")]
[ApiController]
public abstract class ApiBaseController : ControllerBase
{
}