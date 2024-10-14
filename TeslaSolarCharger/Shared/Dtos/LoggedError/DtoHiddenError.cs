using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.LoggedError;

public class DtoHiddenError : DtoLoggedError
{
    public LoggedErrorHideReason HideReason { get; set; }
}
