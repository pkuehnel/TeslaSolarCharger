using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class BackendApiController (IBackendApiService backendApiService) : ApiBaseController
{
    [HttpGet]
    public async Task<DtoValue<bool>> HasValidBackendToken() => new(await backendApiService.HasValidBackendToken());


    [HttpPost]
    public Task LoginToBackend(DtoBackendLogin login) => backendApiService.GetToken(login);
}
