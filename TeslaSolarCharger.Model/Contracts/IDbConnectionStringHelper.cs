namespace TeslaSolarCharger.Model.Contracts;

public interface IDbConnectionStringHelper
{
    string GetTeslaMateConnectionString();
    string GetTeslaSolarChargerDbPath();
}