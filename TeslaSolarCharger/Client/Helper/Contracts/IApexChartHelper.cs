using ApexCharts;

namespace TeslaSolarCharger.Client.Helper.Contracts;

public interface IApexChartHelper
{
    ApexChartOptions<T> GetDefaultChartOptions<T>() where T : class;
}
