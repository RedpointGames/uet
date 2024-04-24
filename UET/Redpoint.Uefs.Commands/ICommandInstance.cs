namespace Redpoint.Uefs.Commands
{
    using System.CommandLine.Invocation;

    // @note: Before this is deleted, CommandExtensions from Redpoint.Uefs.Commands needs to move service registration into UET general registration since we can't register two globals.
    internal interface ICommandInstance
    {
        Task<int> ExecuteAsync(InvocationContext context);
    }
}