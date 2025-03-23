using LanguageExt;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.LoggedError;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IErrorHandlingService
{
    Task HandleError(string source, string methodName, string headline, string message, string issueKey, string? vin,
        string? stackTrace);

    Task HandleErrorResolved(string issueKey, string? vin);
    Task SendTelegramMessages();
    Task<List<DtoLoggedError>> GetActiveLoggedErrors();
    Task<DtoValue<int>> ErrorCount();
    Task<DtoValue<int>> WarningCount();
    Task<int> DismissError(int errorIdValue);
    Task RemoveInvalidLoggedErrorsAsync();
    Task<List<DtoHiddenError>> GetHiddenErrors();
}
