namespace Redpoint.CommandLine
{
    /// <summary>
    /// The command builder which can be used to add subcommands.
    /// </summary>
    public interface ICommandBuilder : ICommandBuilderApi<ICommandBuilder>
    {
    }

    /// <summary>
    /// The command builder which can be used to add subcommands.
    /// </summary>
    /// <typeparam name="TGlobalContext">The global context type used when building the command line.</typeparam>
    public interface ICommandBuilder<TGlobalContext> : ICommandBuilderApi<TGlobalContext, ICommandBuilder<TGlobalContext>> where TGlobalContext : class
    {
    }
}
