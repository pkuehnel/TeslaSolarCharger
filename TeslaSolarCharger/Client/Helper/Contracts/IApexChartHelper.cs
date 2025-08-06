using ApexCharts;

namespace TeslaSolarCharger.Client.Helper.Contracts;

public interface IApexChartHelper
{
    ApexChartOptions<T> GetDefaultChartOptions<T>(bool showLegend) where T : class;
}
