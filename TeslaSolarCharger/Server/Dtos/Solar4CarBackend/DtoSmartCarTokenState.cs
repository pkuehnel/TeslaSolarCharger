namespace TeslaSolarCharger.Server.Dtos.Solar4CarBackend;

public class DtoSmartCarTokenState
{
    public int Id { get; set; }
    public HashSet<string> Vins { get; set; } = new();
}
