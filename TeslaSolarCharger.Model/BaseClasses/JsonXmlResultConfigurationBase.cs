namespace TeslaSolarCharger.Model.BaseClasses;

public abstract class JsonXmlResultConfigurationBase : ResultConfigurationBase
{
    public string? NodePattern { get; set; }
    public string? XmlAttributeHeaderName { get; set; }
    public string? XmlAttributeHeaderValue { get; set; }
    public string? XmlAttributeValueName { get; set; }
}
