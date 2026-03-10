using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using ApexCharts;

namespace TeslaSolarCharger.Client.BaseComponents;

public abstract class ChartComponentBase<TItem> : ComponentBase, IDisposable where TItem : class
{
    [Inject]
    protected IThemeStateService ThemeStateService { get; set; } = default!;

    [Inject]
    protected IChartWidthCalculator ChartWidthCalculator { get; set; } = default!;

    [Inject]
    protected ILogger<ChartComponentBase<TItem>> Logger { get; set; } = default!;

    protected ApexChart<TItem>? _chart;
    protected ApexChartOptions<TItem>? _options;

    protected override Task OnInitializedAsync()
    {
        ThemeStateService.OnDarkModeChanged += OnDarkModeChangedHandler;
        return base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            ChartWidthCalculator.ChartWidthChanged += OnChartWidthChangedHandlerAsync;
            await ChartWidthCalculator.InitAsync();
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    private async void OnDarkModeChangedHandler(bool isDarkMode)
    {
        try
        {
            if (_options?.Theme != null && _chart != null)
            {
                _options.Theme.Mode = isDarkMode ? Mode.Dark : Mode.Light;
                await InvokeAsync(async () =>
                {
                    await _chart.UpdateOptionsAsync(false, false, false);
                    StateHasChanged();
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not update darkmode in chart.");
        }
    }

    private async ValueTask OnChartWidthChangedHandlerAsync()
    {
        try
        {
            if (_chart != null)
            {
                await _chart.UpdateOptionsAsync(false, false, false);
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not update chart width.");
        }
    }

    public virtual void Dispose()
    {
        ThemeStateService.OnDarkModeChanged -= OnDarkModeChangedHandler;
        ChartWidthCalculator.ChartWidthChanged -= OnChartWidthChangedHandlerAsync;
        _chart?.Dispose();
        GC.SuppressFinalize(this);
    }
}
