namespace TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;

public class DtoCarTopicValues
{
#pragma warning disable CS8618
    public List<DtoCarTopicValue> NonDateValues { get; set; }
    public List<DtoCarDateTopics> DateValues { get; set; }
#pragma warning restore CS8618

}
