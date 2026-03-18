using Microsoft.AspNetCore.Components;
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
    private ILogger<ChartComponentBase<TItem>> Logger { get; set; } = default!;

    protected ApexChart<TItem>? Chart;
    protected ApexChartOptions<TItem>? Options;
    //This values was determined by testing. Is used to not get an error when setting series values to null and to redraw the chart after a browser width change.
    protected const int RedrawDelayMilliseconds = 50;

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

    private async Task OnDarkModeChangedHandler(bool isDarkMode)
    {
        try
        {
            if (Options?.Theme != null && Chart != null)
            {
                Options.Theme.Mode = isDarkMode ? Mode.Dark : Mode.Light;
                await InvokeAsync(async () =>
                {
                    StateHasChanged();
                    await Task.Delay(RedrawDelayMilliseconds);
                    await Chart.UpdateOptionsAsync(false, false, false);
                    await Task.Delay(RedrawDelayMilliseconds);
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
            if (Chart != null)
            {
                await InvokeAsync(async () =>
                {
                    StateHasChanged();
                    await Task.Delay(RedrawDelayMilliseconds);
                    await Chart.UpdateOptionsAsync(false, false, false);
                    await Task.Delay(RedrawDelayMilliseconds);
                    StateHasChanged();
                });
                
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
        Chart?.Dispose();
        GC.SuppressFinalize(this);
    }
}
