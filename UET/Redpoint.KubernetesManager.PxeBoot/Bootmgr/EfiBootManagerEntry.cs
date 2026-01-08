
namespace Redpoint.KubernetesManager.PxeBoot.Bootmgr
{
    public record EfiBootManagerEntry
    {
        public required int BootId { get; set; }

        public required string Name { get; set; }

        public required string Path { get; set; }

        public required bool Active { get; set; }
    }
}
