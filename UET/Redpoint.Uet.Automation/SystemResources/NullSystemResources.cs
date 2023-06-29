namespace Redpoint.Uet.Automation.SystemResources
{
    using System;
    using System.Threading.Tasks;

    internal class NullSystemResources : ISystemResources
    {
        public bool CanQuerySystemResources => false;

        public ValueTask<(ulong availableMemoryBytes, ulong totalMemoryBytes)> GetMemoryInfo()
        {
            throw new NotSupportedException();
        }
    }
}
