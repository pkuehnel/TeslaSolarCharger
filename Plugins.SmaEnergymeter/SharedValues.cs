namespace Plugins.SmaEnergymeter;

public class SharedValues
{
    public int OverageW { get; set; }

    public int PowerToGridW { get; set; }
    public int PowerFromGridW { get; set; }

    public decimal TotalEnergyToGridWh { get; set; }
    public decimal TotalEnergyFromGridWh { get; set; }

    public decimal TotalEnergyToGridkWh => TotalEnergyToGridWh / 1000;
    public decimal TotalEnergyFromGridkWh => TotalEnergyFromGridWh / 1000;

    public DateTime LastValuesFrom { get; set; }
}
