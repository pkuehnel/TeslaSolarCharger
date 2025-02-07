namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IErrorDetectionService
{
    Task DetectErrors();
}
