﻿using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Dtos;

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
        public Task<List<Issue>> RefreshIssues() => _issueValidationService.RefreshIssues();

        /// <summary>
        /// Get number of current active error issues.
        /// </summary>
        /// <returns>Inter value with number of active errors</returns>
        [HttpGet]
        public Task<int> ErrorCount() => _issueValidationService.ErrorCount();

        /// <summary>
        /// Get number of current active warning issues.
        /// </summary>
        /// <returns>Inter value with number of active warnings</returns>
        [HttpGet]
        public Task<int> WarningCount() => _issueValidationService.WarningCount();
    }
}
