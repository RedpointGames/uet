namespace Redpoint.Uet.Automation.SystemResources
{
    using System;
    using System.Threading.Tasks;

    internal sealed class NullSystemResources : ISystemResources
    {
        public bool CanQuerySystemResources => false;

        public ValueTask<(ulong availableMemoryBytes, ulong totalMemoryBytes)> GetMemoryInfo()
        {
            throw new NotSupportedException();
        }
    }
}
