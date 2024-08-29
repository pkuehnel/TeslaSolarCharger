namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IErrorHandlingService
{
    Task HandleError(string source, string methodName, string message, string issueKey, string? vin,
        string? stackTrace);

    Task HandleErrorResolved(string issueKey, string? vin);
}
