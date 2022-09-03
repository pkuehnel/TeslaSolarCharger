using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class IssueValidationService
{
    private readonly ILogger<IssueValidationService> _logger;
    private readonly ITeslaService _teslaService;
    private readonly IPvValueService _pvValueService;
    private readonly ISettings _settings;

    public IssueValidationService(ILogger<IssueValidationService> logger,
        ITeslaService teslaService, IPvValueService pvValueService, ISettings settings)
    {
        _logger = logger;
        _teslaService = teslaService;
        _pvValueService = pvValueService;
        _settings = settings;
    }

    public void RefreshIssues()
    {
        var issueList = new List<Issue>();
    }
}
