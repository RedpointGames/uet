namespace Redpoint.CommandLine
{
    using System.CommandLine;

    /// <summary>
    /// Constructs an instance of <see cref="Command"/> which describes the command, such as it's name, aliases and full description.
    /// 
    /// This callback should not add options to the command, nor should it set a handler. These will be handled by the <see cref="ICommandBuilder"/> upon the <see cref="Command"/> instance being returned from the callback.
    /// 
    /// You can call <see cref="ICommandBuilderApi{TSelfType}.AddCommand{TCommand, TOptions}(CommandDescriptorFactory, CommandServiceRegistration?, CommandServiceRegistration?)"/> on <paramref name="builder"/> to add a subcommand to the command.
    /// </summary>
    /// <param name="builder">The command builder for this particular command being constructed.</param>
    /// <returns>The command descriptor.</returns>
    public delegate Command CommandDescriptorFactory(ICommandBuilder builder);

    /// <summary>
    /// Constructs an instance of <see cref="Command"/> which describes the command, such as it's name, aliases and full description.
    /// 
    /// This callback should not add options to the command, nor should it set a handler. These will be handled by the <see cref="ICommandBuilder{TGlobalContext}"/> upon the <see cref="Command"/> instance being returned from the callback.
    /// 
    /// You can call <see cref="ICommandBuilderApi{TGlobalContext, TSelfType}.AddCommand{TCommand, TOptions}(CommandDescriptorFactory{TGlobalContext}, CommandServiceRegistration{TGlobalContext}?, CommandServiceRegistration{TGlobalContext}?)"/> on <paramref name="builder"/> to add a subcommand to the command.
    /// </summary>
    /// <param name="builder">The command builder for this particular command being constructed.</param>
    /// <returns>The command descriptor.</returns>
    public delegate Command CommandDescriptorFactory<TGlobalContext>(ICommandBuilder<TGlobalContext> builder) where TGlobalContext : class;
}
