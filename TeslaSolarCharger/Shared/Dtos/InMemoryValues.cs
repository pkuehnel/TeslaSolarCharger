using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Shared.Dtos;

public class InMemoryValues : IInMemoryValues
{
    public List<int> OverageValues { get; set; }

    public InMemoryValues()
    {
        OverageValues = new List<int>();
    }
}