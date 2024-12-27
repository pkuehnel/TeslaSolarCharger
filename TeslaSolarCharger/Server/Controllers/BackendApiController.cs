using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class BackendApiController (IBackendApiService backendApiService) : ApiBaseController
{
    [HttpGet]
    public Task<DtoValue<bool>> HasValidBackendToken() => backendApiService.HasValidBackendToken();

    [HttpPost]
    public Task<DtoValue<bool>> GenerateUserAccount(string emailAddress) => backendApiService.GenerateUserAccount(emailAddress);
}
