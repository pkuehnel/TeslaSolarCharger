namespace TeslaSolarCharger.Server.Contracts;

public interface INewVersionCheckService
{
    Task CheckForNewVersion();
}
