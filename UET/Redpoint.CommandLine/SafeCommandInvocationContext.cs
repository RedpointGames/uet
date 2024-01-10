namespace Redpoint.CommandLine
{
    using System.CommandLine.Invocation;
    using System.CommandLine.Parsing;

    /// <summary>
    /// The invocation context API is set to change with the stable release of System.CommandLine, so this ensures that
    /// consumers of Redpoint.CommandLineBuilder can continue to use a stable invocation context to access the
    /// cancellation token, even when System.CommandLine refactor its API.
    /// </summary>
    internal class SafeCommandInvocationContext : ICommandInvocationContext
    {
        private readonly InvocationContext _invocationContext;

        public SafeCommandInvocationContext(InvocationContext invocationContext)
        {
            _invocationContext = invocationContext;
        }

        public ParseResult ParseResult => _invocationContext.ParseResult;

        public CancellationToken GetCancellationToken()
        {
            return _invocationContext.GetCancellationToken();
        }
    }
}
