namespace Redpoint.Windows.HCS.Tests
{
    using Redpoint.Windows.HCS.v2;
    using System.Runtime.Versioning;

    public class HCSTests
    {
        [SkippableFact]
        [SupportedOSPlatform("windows6.2")]
        public void CanEnumerateComputeSystems()
        {
            Skip.IfNot(OperatingSystem.IsWindowsVersionAtLeast(6, 2));

            var computeSystems = HCSClient.EnumerateComputeSystems();
            foreach (var computeSystemRef in computeSystems)
            {
                if (!computeSystemRef.IsRuntimeTemplate)
                {
                    using (var computeSystem = HCSClient.OpenComputeSystem(computeSystemRef.Id!))
                    {
                        HCSClient.GetComputeSystemProperties(computeSystem, new Service_PropertyQuery
                        {
                            PropertyTypes = new GetPropertyType[]
                            {
                                GetPropertyType.Basic
                            }
                        });
                    }
                }
            }
        }
    }
}