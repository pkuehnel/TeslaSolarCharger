using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Shared;

public class ConfigPropertyResolver : DefaultContractResolver
{
    private bool IgnoreStatusProperties = true;

    private readonly List<string> _configPropertyNames = new()
    {
        nameof(Car.CarConfiguration),
        nameof(Car.CarConfiguration.LatestTimeToReachSoC),
        nameof(Car.CarConfiguration.MinimumSoC),
        nameof(Car.CarConfiguration.ChargeMode),
        nameof(Car.CarConfiguration.MinimumAmpere),
        nameof(Car.CarConfiguration.MaximumAmpere),
        nameof(Car.CarConfiguration.UsableEnergy),
        nameof(Car.CarConfiguration.ShouldBeManaged),
        nameof(Car.Id),
    };

    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        var allProps = base.CreateProperties(type, memberSerialization);
        if (!IgnoreStatusProperties)
        {
            return allProps;
        }

        return allProps.Where(p => _configPropertyNames.Any(c => c.Equals(p.PropertyName))).ToList();
    }
}
