using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Reflection;

namespace TeslaSolarCharger.Server.Helper;

public class IgnorePropertiesResolver(IEnumerable<string> propNamesToIgnore) : DefaultContractResolver
{
    private readonly HashSet<string> _ignoreProps = new(propNamesToIgnore);

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);

        if (property.PropertyName != null && _ignoreProps.Contains(property.PropertyName))
        {
            property.ShouldSerialize = _ => false;
        }

        return property;
    }
}
