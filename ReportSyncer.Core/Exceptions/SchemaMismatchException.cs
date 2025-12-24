namespace ReportSyncer.Core.Exceptions
{
    public class SchemaMismatchException : Exception
    {
        public SchemaMismatchException(string message) : base(message) { }
        public SchemaMismatchException(string message, Exception inner) : base(message, inner) { }
    }
}
