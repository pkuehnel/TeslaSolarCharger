using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SmartTeslaAmpSetter.Shared.Dtos;

namespace SmartTeslaAmpSetter.Shared;

public class ConfigPropertyResolver : DefaultContractResolver
{
    private bool IgnoreStatusProperties = true;

    private List<string> ConfigPropertyNames = new()
    {
        nameof(Car.CarConfiguration),
        nameof(Car.Id),
    };

    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        var allProps = base.CreateProperties(type, memberSerialization);
        if (!IgnoreStatusProperties)
        {
            return allProps;
        }

        return allProps.Where(p => ConfigPropertyNames.Any(c => c.Equals(p.PropertyName))).ToList();
    }
}