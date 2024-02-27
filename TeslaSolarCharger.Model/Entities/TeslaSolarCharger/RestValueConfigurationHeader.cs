namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class RestValueConfigurationHeader
{
    public int Id { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }

    public int RestValueConfigurationId { get; set; }
    public RestValueConfiguration RestValueConfiguration { get; set; }
}
