namespace Redpoint.Windows.HCS.Tests
{
    using Redpoint.Windows.HCS.v2;

    public class HCSTests
    {
        [Fact]
        public void CanEnumerateComputeSystems()
        {
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