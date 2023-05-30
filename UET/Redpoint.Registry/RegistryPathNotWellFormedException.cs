namespace Redpoint.Registry
{
    using System;

    /// <summary>
    /// The provided registry path was not well formed.
    /// </summary>
    [Serializable]
    public class RegistryPathNotWellFormedException : Exception
    {
        internal RegistryPathNotWellFormedException() : base("The specified registry path is not well formed.")
        {
        }
    }
}