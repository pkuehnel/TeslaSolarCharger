using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using TeslaSolarCharger.Client.Resources;

namespace TeslaSolarCharger.Client.Localization;

public abstract class LocalizedComponentBase : ComponentBase
{
    [Inject]
    protected IStringLocalizer<SharedResource> Localizer { get; set; } = default!;

    protected string T(string englishText) => Localizer[englishText];
}
