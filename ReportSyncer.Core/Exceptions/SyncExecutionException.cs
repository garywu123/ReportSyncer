namespace ReportSyncer.Core.Exceptions;

public class SyncExecutionException : Exception
{
    public SyncExecutionException(string message) : base(message) { }
    public SyncExecutionException(string message, Exception inner) : base(message, inner) { }
}