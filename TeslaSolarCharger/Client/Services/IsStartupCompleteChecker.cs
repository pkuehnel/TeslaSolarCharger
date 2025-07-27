using System.Net.Http.Json;
using TeslaSolarCharger.Client.Contracts;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Client.Services;

public class IsStartupCompleteChecker : IIsStartupCompleteChecker
{
    private readonly HttpClient _httpClient;

    public IsStartupCompleteChecker(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    public async Task<bool> IsStartupCompleteAsync()
    {
        var result = (await _httpClient.GetFromJsonAsync<DtoValue<bool>>("api/Hello/IsStartupCompleted"))?.Value;
        return result == true;
    }
}
