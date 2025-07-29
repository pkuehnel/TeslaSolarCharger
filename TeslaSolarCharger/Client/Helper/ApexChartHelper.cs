using ApexCharts;
using TeslaSolarCharger.Client.Helper.Contracts;

namespace TeslaSolarCharger.Client.Helper;

public class ApexChartHelper : IApexChartHelper
{
    public ApexChartOptions<T> GetDefaultChartOptions<T>() where T : class
    {
        return new ApexChartOptions<T>
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
            Legend = new Legend
            {
                Position = LegendPosition.Bottom,
                HorizontalAlign = ApexCharts.Align.Center,
                Floating = false,
            },
        };
    }
}
