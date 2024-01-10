namespace Redpoint.CommandLine
{
    /// <summary>
    /// Implemented by types that provide the global context object.
    /// </summary>
    /// <typeparam name="TGlobalContext">The global context type used when building the command line.</typeparam>
    public interface IHasGlobalContext<TGlobalContext> where TGlobalContext : class
    {
        /// <summary>
        /// The global context object used when building the command line.
        /// </summary>
        TGlobalContext GlobalContext { get; }
    }
}
