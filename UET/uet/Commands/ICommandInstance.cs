namespace UET.Commands
{
    using System.CommandLine.Invocation;

    internal interface ICommandInstance
    {
        Task<int> ExecuteAsync(InvocationContext context);
    }
}
