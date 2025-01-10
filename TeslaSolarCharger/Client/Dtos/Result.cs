using Microsoft.AspNetCore.Mvc;

namespace TeslaSolarCharger.Client.Dtos;

public record Result<T>(T? Data, string? ErrorMessage, ProblemDetails? ProblemDetails)
{
    // Convenience property to check if it's an error
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
}
