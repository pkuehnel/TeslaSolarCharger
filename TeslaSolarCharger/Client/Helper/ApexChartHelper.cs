using ApexCharts;
using TeslaSolarCharger.Client.Helper.Contracts;

namespace TeslaSolarCharger.Client.Helper;

public class ApexChartHelper : IApexChartHelper
{
    public ApexChartOptions<T> GetDefaultChartOptions<T>(bool showLegend) where T : class
    {
        var options = new ApexChartOptions<T>
        {
            Chart = new Chart
            {
                Toolbar = new Toolbar
                {
                    Show = false,
                },
                Zoom = new Zoom
                {
                    Enabled = false,
                },
            },
            Grid = new Grid
            {
                BorderColor = "#e7e7e7",
                Row = new GridRow
                {
                    Colors = new List<string> { "#f3f3f3", "transparent" },
                    Opacity = 0.5d,
                },
            },
            Markers = new Markers
            {
                Size = 5,
            },
            Stroke = new Stroke { Curve = Curve.Smooth },
        };
        if (showLegend)
        {
            options.Legend = new Legend
            {
                Position = LegendPosition.Bottom,
                HorizontalAlign = Align.Center,
                ShowForNullSeries = false,
                ClusterGroupedSeries = false,
                Floating = false,
            };
        }
        else
        {
            options.Legend = new Legend
            {
                Show = false,
            };
        }
        return options;
    }
}
