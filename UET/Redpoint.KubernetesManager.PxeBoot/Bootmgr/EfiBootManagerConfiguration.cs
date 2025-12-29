
namespace Redpoint.KubernetesManager.PxeBoot.Bootmgr
{
    using System.Collections.Generic;

    public record EfiBootManagerConfiguration
    {
        public required int BootCurrentId { get; set; }

        public required int Timeout { get; set; }

        public required IList<int> BootOrder { get; set; }

        public required Dictionary<int, EfiBootManagerEntry> BootEntries { get; set; }

    }
}
