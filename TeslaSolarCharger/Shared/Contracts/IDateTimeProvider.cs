namespace TeslaSolarCharger.Shared.Contracts;

public interface IDateTimeProvider
{
    //ToDO: need to check if DateTime is really needed
    DateTime Now();
    DateTime UtcNow();


    DateTimeOffset DateTimeOffSetNow();
    DateTimeOffset DateTimeOffSetUtcNow();
}
