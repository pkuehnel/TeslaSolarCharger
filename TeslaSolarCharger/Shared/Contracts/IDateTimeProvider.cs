namespace TeslaSolarCharger.Shared.Contracts;

public interface IDateTimeProvider
{
    DateTime Now();
    DateTime UtcNow();
}
