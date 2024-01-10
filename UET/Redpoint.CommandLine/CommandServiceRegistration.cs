namespace Redpoint.CommandLine
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Specifies a delegate that is used to register services with a <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="builder">The command line builder instance.</param>
    /// <param name="services">The service collection to register services with.</param>
    public delegate void CommandServiceRegistration(ICommandLineBuilder builder, IServiceCollection services);

    /// <summary>
    /// Specifies a delegate that is used to register services with a <see cref="IServiceCollection"/>, with the additional context object provided.
    /// </summary>
    /// <typeparam name="TGlobalContext">The global context type used when building the command line.</typeparam>
    /// <param name="builder">The command line builder instance.</param>
    /// <param name="services">The service collection to register services with.</param>
    public delegate void CommandServiceRegistration<TGlobalContext>(ICommandLineBuilder<TGlobalContext> builder, IServiceCollection services) where TGlobalContext : class;
}
