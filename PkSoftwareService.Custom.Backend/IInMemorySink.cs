namespace PkSoftwareService.Custom.Backend;

public interface IInMemorySink
{
    /// <summary>
    /// Returns a snapshot of the current log messages.
    /// </summary>
    List<string> GetLogs(int? tail = null);

    int GetCapacity();
    void UpdateCapacity(int newCapacity);
    Task StreamLogsAsync(StreamWriter writer);
}
