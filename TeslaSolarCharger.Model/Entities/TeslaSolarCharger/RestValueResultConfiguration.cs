using TeslaSolarCharger.Model.BaseClasses;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class RestValueResultConfiguration : JsonXmlResultConfigurationBase
{
    public int RestValueConfigurationId { get; set; }
    public RestValueConfiguration RestValueConfiguration { get; set; }
}
