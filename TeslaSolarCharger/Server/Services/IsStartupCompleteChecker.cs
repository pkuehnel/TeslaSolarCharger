using TeslaSolarCharger.Client.Contracts;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class IsStartupCompleteChecker : IIsStartupCompleteChecker
{
    private readonly ICoreService _coreService;

    public IsStartupCompleteChecker(ICoreService coreService)
    {
        _coreService = coreService;
    }

    public async Task<bool> IsStartupCompleteAsync()
    {
        return _coreService.IsStartupCompleted();
    }
}
