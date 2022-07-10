using SolarTeslaCharger.Shared.Dtos.Contracts;

namespace SolarTeslaCharger.Shared.Dtos;

public class InMemoryValues : IInMemoryValues
{
    public List<int> OverageValues { get; set; }

    public InMemoryValues()
    {
        OverageValues = new List<int>();
    }
}