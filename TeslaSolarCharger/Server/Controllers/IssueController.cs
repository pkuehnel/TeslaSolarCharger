using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class IssueController : ControllerBase
    {
        private readonly IIssueValidationService _issueValidationService;

        public IssueController(IIssueValidationService issueValidationService)
        {
            _issueValidationService = issueValidationService;
        }

        /// <summary>
        /// Refresh issues. Note: This call results in multiple calls in the Backend to validate all possible issues.
        /// </summary>
        /// <returns>List of current active issues</returns>
        [HttpGet]
        public List<Issue> RefreshIssues() => _issueValidationService.RefreshIssues();
    }
}
