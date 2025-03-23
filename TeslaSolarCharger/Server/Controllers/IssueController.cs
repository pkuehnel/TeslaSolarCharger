using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers
{
    public class IssueController(IErrorHandlingService issueValidationService) : ApiBaseController
    {
        /// <summary>
        /// Get number of current active error issues.
        /// </summary>
        /// <returns>Inter value with number of active errors</returns>
        [HttpGet]
        public Task<DtoValue<int>> ErrorCount() => issueValidationService.ErrorCount();
    }
}
