namespace TeslaSolarCharger.Client.Dtos;

/// <summary>
/// Minimal client-side representation of an RFC 7807 (validation) problem details response.
/// Replaces the ASP.NET Core MVC type so the Blazor WebAssembly client does not need to
/// reference the framework-only / legacy MVC packages.
/// </summary>
public class ValidationProblemDetails
{
    public string? Detail { get; set; }

    public IDictionary<string, string[]> Errors { get; set; } = new Dictionary<string, string[]>();
}
