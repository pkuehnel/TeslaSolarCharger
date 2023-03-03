using Microsoft.AspNetCore.Mvc;

namespace TeslaSolarCharger.SharedBackend.Abstracts
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public abstract class ApiBaseController : ControllerBase
    {
    }
}
