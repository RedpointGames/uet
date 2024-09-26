namespace Redpoint.Uba.Native
{
    /// <summary>
    /// An exception that is thrown when <see cref="UbaNative.Init(string)"/> has not yet been called.
    /// </summary>
    public class UbaNotInitializedException : Exception
    {
        /// <summary>
        /// Constructs an exception that should only be thrown when <see cref="UbaNative.Init(string)"/> has not yet been called.
        /// </summary>
        public UbaNotInitializedException() : base("The path to the UbaHost library has not been set. Call 'UbaNative.Init' with the directory path before accessing any UBA functions.")
        {
        }
    }
}
