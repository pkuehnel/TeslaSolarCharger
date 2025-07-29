namespace TeslaSolarCharger.Client.Helper.Contracts;

public interface IChartWidthCalculator
{
    string ChartWidth { get; }
    event Func<ValueTask>? ChartWidthChanged;
    Task InitAsync();
}
