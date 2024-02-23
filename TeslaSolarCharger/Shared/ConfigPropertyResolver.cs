using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Shared;

public class ConfigPropertyResolver : DefaultContractResolver
{
    private bool IgnoreStatusProperties = true;

    private readonly List<string> _configPropertyNames = new()
    {
        nameof(DtoCar.CarConfiguration),
        nameof(DtoCar.CarConfiguration.LatestTimeToReachSoC),
        nameof(DtoCar.CarConfiguration.MinimumSoC),
        nameof(DtoCar.CarConfiguration.ChargeMode),
        nameof(DtoCar.CarConfiguration.MinimumAmpere),
        nameof(DtoCar.CarConfiguration.MaximumAmpere),
        nameof(DtoCar.CarConfiguration.UsableEnergy),
        nameof(DtoCar.CarConfiguration.ShouldBeManaged),
        nameof(DtoCar.CarConfiguration.ShouldSetChargeStartTimes),
        nameof(DtoCar.Id),
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
