namespace Redpoint.CommandLine
{
    /// <summary>
    /// Provides static methods for creating a new <see cref="ICommandLineBuilder"/> or <see cref="ICommandLineBuilder{TContext}"/>, with which you can build root commands.
    /// </summary>
    public static class CommandLineBuilder
    {
        /// <summary>
        /// Create a new command line builder that has no global context object.
        /// </summary>
        /// <returns>The new command line builder.</returns>
        public static ICommandLineBuilder NewBuilder()
        {
            return new DefaultCommandLineBuilder<EmptyGlobalContext>(new EmptyGlobalContext());
        }

        /// <summary>
        /// Create a new command line builder that uses <paramref name="globalContext"/> as the global context.
        /// </summary>
        /// <typeparam name="TGlobalContext">The global context type used when building the command line.</typeparam>
        /// <param name="globalContext">The context object to use.</param>
        /// <returns>The new command line builder.</returns>
        public static ICommandLineBuilder<TGlobalContext> NewBuilder<TGlobalContext>(TGlobalContext globalContext) where TGlobalContext : class
        {
            return new DefaultCommandLineBuilder<TGlobalContext>(globalContext);
        }
    }
}
