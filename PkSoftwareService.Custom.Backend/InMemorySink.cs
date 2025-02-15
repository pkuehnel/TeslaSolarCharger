using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace PkSoftwareService.Custom.Backend;

public class InMemorySink : ILogEventSink, IInMemorySink
{
    private int _capacity;
    private Queue<string> _logMessages;
    private readonly object _syncRoot = new object();
    private readonly MessageTemplateTextFormatter _formatter;

    /// <summary>
    /// Creates a new InMemorySink.
    /// </summary>
    /// <param name="outputTemplate">The output template (should match your Console sink).</param>
    /// <param name="formatProvider">Optional format provider.</param>
    /// <param name="capacity">Max number of messages to store.</param>
    public InMemorySink(string outputTemplate, int capacity, IFormatProvider? formatProvider = null)
    {
        _capacity = capacity;
        _logMessages = new Queue<string>(capacity);
        _formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
    }

    public void Emit(LogEvent logEvent)
    {
        // Format the log event into a string.
        using var writer = new StringWriter();
        _formatter.Format(logEvent, writer);
        var message = writer.ToString();

        // Ensure thread safety while enqueuing/dequeuing.
        lock (_syncRoot)
        {
            if (_logMessages.Count >= _capacity)
            {
                _logMessages.Dequeue(); // remove oldest
            }
            _logMessages.Enqueue(message);
        }
    }

    /// <summary>
    /// Returns a snapshot of the current log messages.
    /// </summary>
    public List<string> GetLogs()
    {
        lock (_syncRoot)
        {
            return _logMessages.Select(x => x.Trim()).ToList();
        }
    }

    public int GetCapacity()
    {
        return _capacity;
    }

    public void UpdateCapacity(int newCapacity)
    {
        if (newCapacity < 1)
        {
            throw new ArgumentException("Log capacity can not be lower than 1");
        }

        if (newCapacity < _capacity)
        {
            lock (_syncRoot)
            {
                _logMessages = new(_logMessages.TakeLast(newCapacity));
            }
        }
        _capacity = newCapacity;
    }
}
