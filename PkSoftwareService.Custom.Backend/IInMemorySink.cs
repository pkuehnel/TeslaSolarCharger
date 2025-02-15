namespace PkSoftwareService.Custom.Backend;

public interface IInMemorySink
{
    /// <summary>
    /// Returns a snapshot of the current log messages.
    /// </summary>
    List<string> GetLogs();

    int GetCapacity();
    void UpdateCapacity(int newCapacity);
}
