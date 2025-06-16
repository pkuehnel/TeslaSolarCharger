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
                DropShadow = new DropShadow
                {
                    Enabled = true,
                    Color = "",
                    Top = 18,
                    Left = 7,
                    Blur = 10,
                    Opacity = 0.2d,
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

            Markers = new Markers { Shape = ShapeEnum.Circle, Size = 5, FillOpacity = new Opacity(0.8d), },
            Stroke = new Stroke { Curve = Curve.Smooth },
            Legend = new Legend
            {
                Position = LegendPosition.Bottom,
                HorizontalAlign = ApexCharts.Align.Center,
                Floating = false,
            },
            Xaxis = new XAxis()
            {
                Max = 23,
            },

        };
    }
}
