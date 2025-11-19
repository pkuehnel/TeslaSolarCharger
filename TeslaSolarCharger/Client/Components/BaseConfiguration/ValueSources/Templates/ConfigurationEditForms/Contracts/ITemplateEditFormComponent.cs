using Newtonsoft.Json.Linq;

namespace TeslaSolarCharger.Client.Components.BaseConfiguration.ValueSources.Templates.ConfigurationEditForms.Contracts;

public interface ITemplateEditFormComponent
{
    bool Validate();
    JObject? GetConfiguration();
}
