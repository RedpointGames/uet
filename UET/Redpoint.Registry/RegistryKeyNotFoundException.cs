namespace Redpoint.Registry
{
    using System;

    /// <summary>
    /// No registry key exists as the specified registry path.
    /// </summary>
    [Serializable]
    public class RegistryKeyNotFoundException : Exception
    {
        internal RegistryKeyNotFoundException() : base("The specified registry path does not exist.")
        {
        }
    }
}