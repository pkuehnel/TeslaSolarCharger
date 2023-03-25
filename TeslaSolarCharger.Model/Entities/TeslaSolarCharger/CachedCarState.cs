namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class CachedCarState
{
    public int Id { get; set; }
    public int CarId { get; set; }
#pragma warning disable CS8618
    public string Key { get; set; }
#pragma warning restore CS8618
    //Needs to be nullable for new/unused cars
    public string? CarStateJson { get; set; }
    public DateTime LastUpdated { get; set; }
}
