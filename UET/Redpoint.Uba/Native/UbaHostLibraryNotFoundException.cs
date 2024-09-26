namespace Redpoint.Uba.Native
{
    /// <summary>
    /// An exception that is thrown when the path passed to <see cref="UbaNative.Init(string)"/> is missing an expected file.
    /// </summary>
    public class UbaHostLibraryNotFoundException : Exception
    {
        /// <summary>
        /// Constructs an exception that should only be thrown when the path passed to <see cref="UbaNative.Init(string)"/> is missing an expected file.
        /// </summary>
        public UbaHostLibraryNotFoundException(
            string ubaPath,
            string ubaHostFileName) : base($"The specified path {ubaPath} does not contain a file called {ubaHostFileName}, which should be the UBA host library.")
        {
            UbaPath = ubaPath;
            UbaHostFileName = ubaHostFileName;
        }

        /// <summary>
        /// The path that was passed to <see cref="UbaNative.Init(string)"/>, plus any architecture-specific subdirectory.
        /// </summary>
        public string UbaPath { get; }

        /// <summary>
        /// The name of the file that was expected to exist within <see cref="UbaPath"/>, but does not.
        /// </summary>
        public string UbaHostFileName { get; }
    }
}
