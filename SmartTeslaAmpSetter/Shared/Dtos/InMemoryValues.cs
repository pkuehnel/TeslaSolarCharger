using SmartTeslaAmpSetter.Shared.Dtos.Contracts;

namespace SmartTeslaAmpSetter.Shared.Dtos;

public class InMemoryValues : IInMemoryValues
{
    public List<int> OverageValues { get; set; }

    public InMemoryValues(List<int> overageValues)
    {
        OverageValues = overageValues;
    }
}