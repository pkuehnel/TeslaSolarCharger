using Plugins.SolarEdge.Dtos.CloudApi;

namespace Plugins.SolarEdge;

public class SharedValues
{
    public SharedValues()
    {
        CloudApiValues = new Dictionary<DateTime, CloudApiValue>();
    }
    public Dictionary<DateTime, CloudApiValue> CloudApiValues { get; set; }

    public DateTime? LastTooManyRequests = null;
}
