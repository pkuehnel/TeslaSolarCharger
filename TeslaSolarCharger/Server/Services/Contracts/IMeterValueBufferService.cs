using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IMeterValueBufferService
{
    void Add(MeterValue meterValue);

    /// <summary>
    /// Retrieves and removes all items from the buffer.
    /// </summary>
    List<MeterValue> DrainAll();
}
