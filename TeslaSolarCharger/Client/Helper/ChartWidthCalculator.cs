using MudBlazor;
using MudBlazor.Services;
using TeslaSolarCharger.Client.Helper.Contracts;

namespace TeslaSolarCharger.Client.Helper;

public class ChartWidthCalculator : IBrowserViewportObserver, IAsyncDisposable, IChartWidthCalculator
{
    private readonly ILogger<ChartWidthCalculator> _logger;
    private readonly IBrowserViewportService _browserViewportService;

    // current width as CSS string
    public string ChartWidth { get; private set; } = "600px";

    public ResizeOptions ResizeOptions { get; } = new ResizeOptions { ReportRate = 500, NotifyOnBreakpointOnly = false };

    // fire this when ChartWidth changes
    public event Func<ValueTask>? ChartWidthChanged;

    public Guid Id { get; } = Guid.NewGuid();

    public ChartWidthCalculator(
        ILogger<ChartWidthCalculator> logger,
        IBrowserViewportService browserViewportService)
    {
        _logger = logger;
        _browserViewportService = browserViewportService;
    }

    // call once to hook into viewport updates
    public async Task InitAsync()
    {
        _logger.LogDebug("Initializing ChartWidthCalculator {Id}", Id);
        await _browserViewportService.SubscribeAsync(this);
    }

    public async Task NotifyBrowserViewportChangeAsync(
        BrowserViewportEventArgs browserViewportEventArgs)
    {
        var browserWidth = browserViewportEventArgs.BrowserWindowSize.Width;
        _logger.LogTrace("Recalculating chart width for window width {Width}", browserWidth);

        ChartWidth = GetChartWidth(browserWidth);

        if (ChartWidthChanged is { })
        {
            // invoke all subscribers
            foreach (var handler in ChartWidthChanged.GetInvocationList())
            {
                await ((Func<ValueTask>)handler)();
            }
        }
    }

    private string GetChartWidth(int browserWidth)
    {
        _logger.LogTrace("GetChartWidth({BrowserWidth})", browserWidth);

        var isMenuVisible = browserWidth > 640;
        if (isMenuVisible)
        {
            return $"{browserWidth - 450}px";
        }
        else
        {
            return $"{browserWidth - 50}px";
        }
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogDebug("Disposing ChartWidthCalculator {Id}", Id);
        await _browserViewportService.UnsubscribeAsync(this);
    }
}
