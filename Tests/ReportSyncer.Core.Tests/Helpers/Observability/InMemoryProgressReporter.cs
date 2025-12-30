using ReportSyncer.Core.Observability;
using ReportSyncer.Core.Sync.Contracts;

namespace ReportSyncer.Core.Tests.Helpers.Observability;

internal sealed class InMemoryProgressReporter : IJobProgressReporter
{
    public List<JobProgressEvent> JobEvents { get; } = new();
    public List<TableProgressEvent> TableEvents { get; } = new();

    public void Report(JobProgressEvent evt)
    {
        if (evt is not null)
            JobEvents.Add(evt);
    }

    public void Report(TableProgressEvent evt)
    {
        if (evt is not null)
            TableEvents.Add(evt);
    }
}
