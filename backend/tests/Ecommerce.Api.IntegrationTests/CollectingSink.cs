using Serilog.Core;
using Serilog.Events;

namespace Ecommerce.Api.IntegrationTests;

public sealed class CollectingSink : ILogEventSink
{
    private readonly List<LogEvent> _events = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<LogEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return _events.ToList();
            }
        }
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_gate)
        {
            _events.Add(logEvent);
        }
    }
}
