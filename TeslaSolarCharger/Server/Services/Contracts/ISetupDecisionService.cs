using TeslaSolarCharger.Shared.Dtos.Setup;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ISetupDecisionService
{
    /// <summary>
    /// Works out which steps apply to this installation, what the user should do next, what can be configured
    /// automatically and what is still missing or contradictory. The single source of these rules: screens and
    /// server side validation both read them from here instead of each keeping their own copy.
    /// </summary>
    Task<DtoSetupDecision> Evaluate(DtoSetupState setupState);
}
