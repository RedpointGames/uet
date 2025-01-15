namespace Redpoint.CloudFramework.Tests
{
    using Docker.DotNet.Models;
    using System;

    internal class NullLogProgress : IProgress<JSONMessage>
    {
        public void Report(JSONMessage value)
        {
        }
    }
}
