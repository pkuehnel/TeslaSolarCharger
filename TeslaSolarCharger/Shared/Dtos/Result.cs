namespace TeslaSolarCharger.Shared.Dtos;

public record Result<T>(T? Data, string? ErrorMessage)
{
    // Convenience property to check if it's an error
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
}
