namespace Redpoint.CommandLine
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Specifies a delegate that is used to register runtime services with a <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="builder">The command line builder instance.</param>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="context">The invocation context for the command being executed.</param>
    public delegate void CommandRuntimeServiceRegistration(ICommandLineBuilder builder, IServiceCollection services, ICommandInvocationContext context);

    /// <summary>
    /// Specifies a delegate that is used to register runtime services with a <see cref="IServiceCollection"/>, with the additional context object provided.
    /// </summary>
    /// <typeparam name="TGlobalContext">The global context type used when building the command line.</typeparam>
    /// <param name="builder">The command line builder instance.</param>
    /// <param name="services">The service collection to register services with.</param>
    /// <param name="context">The invocation context for the command being executed.</param>
    public delegate void CommandRuntimeServiceRegistration<TGlobalContext>(ICommandLineBuilder<TGlobalContext> builder, IServiceCollection services, ICommandInvocationContext context) where TGlobalContext : class;
}
