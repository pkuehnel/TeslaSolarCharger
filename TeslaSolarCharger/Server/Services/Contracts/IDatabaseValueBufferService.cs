
namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IDatabaseValueBufferService
{
    void Add<T>(T item);
    List<T> DrainAll<T>();
}
