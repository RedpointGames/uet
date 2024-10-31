namespace UET.Commands.Internal.CMakeUbaServer
{
    using k8s.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal class KubernetesNodeState
    {
        public string? NodeId;
        public V1Node? KubernetesNode;
        public List<V1Pod> KubernetesPods = new List<V1Pod>();
        public readonly List<KubernetesNodeWorker> AllocatedBlocks = new List<KubernetesNodeWorker>();

        public ulong MemoryTotal
        {
            get
            {
                return KubernetesNode!.Status.Capacity["memory"].ToUInt64();
            }
        }

        public ulong MemoryNonUba
        {
            get
            {
                return KubernetesPods
                    .Where(x => x.GetLabel("uba") != "true")
                    .SelectMany(x => x.Spec.Containers)
                    .Select(x => x?.Resources?.Requests != null && x.Resources.Requests.TryGetValue("memory", out var quantity) ? quantity.ToUInt64() : 0)
                    .Aggregate((a, b) => a + b);
            }
        }

        public ulong MemoryAllocated
        {
            get
            {
                return AllocatedBlocks
                    .Select(x => (ulong)x.AllocatedCores * KubernetesConstants.MemoryBytesPerCore)
                    .DefaultIfEmpty((ulong)0)
                    .Aggregate((a, b) => a + b);
            }
        }

        public ulong MemoryAvailable
        {
            get
            {
                var memory = MemoryTotal;
                memory -= MemoryNonUba;
                memory -= MemoryAllocated;
                return Math.Max(0, memory);
            }
        }

        public double CoresTotal
        {
            get
            {
                return Math.Floor(KubernetesNode!.Status.Capacity["cpu"].ToDouble());
            }
        }

        public double CoresNonUba
        {
            get
            {
                return KubernetesPods
                    .Where(x => x.GetLabel("uba") != "true")
                    .SelectMany(x => x.Spec.Containers)
                    .Select(x => x?.Resources?.Requests != null && x.Resources.Requests.TryGetValue("cpu", out var quantity) ? quantity.ToDouble() : 0)
                    .Sum();
            }
        }

        public double CoresAllocated
        {
            get
            {
                return AllocatedBlocks.Sum(x => x.AllocatedCores);
            }
        }

        public int CoresAvailable
        {
            get
            {
                var cores = CoresTotal;
                cores -= CoresNonUba;
                cores -= CoresAllocated;
                return (int)Math.Max(0, Math.Floor(cores));
            }
        }

        public int CoresAllocatable
        {
            get
            {
                var realCoresAvailable = CoresAvailable;
                var memoryConstrainedCoresAvailable = MemoryAvailable / KubernetesConstants.MemoryBytesPerCore;
                return Math.Min(realCoresAvailable, (int)memoryConstrainedCoresAvailable);
            }
        }
    }
}
