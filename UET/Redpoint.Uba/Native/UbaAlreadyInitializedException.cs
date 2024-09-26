namespace Redpoint.Uba.Native
{
    /// <summary>
    /// An exception that is thrown when <see cref="UbaNative.Init(string)"/> has already been called before.
    /// </summary>
    public class UbaAlreadyInitializedException : Exception
    {
        /// <summary>
        /// Constructs an exception that should only be thrown when <see cref="UbaNative.Init(string)"/> has already been called before.
        /// </summary>
        public UbaAlreadyInitializedException(string ubaPath) : base($"'UbaNative.Init' was called more than once; UBA is already initialized with the path {ubaPath} so you don't need to initialize it again.")
        {
            UbaPath = ubaPath;
        }

        /// <summary>
        /// The path that <see cref="UbaNative.Init(string)"/> has previously been initialized with.
        /// </summary>
        public string UbaPath { get; }
    }
}
