namespace Redpoint.CloudFramework.Tests
{
    using System;
    using System.Collections.Generic;

    internal static class ContainerConfiguration
    {
        internal static readonly IReadOnlyList<(string type, string name, string image, string[] arguments, int port)> _expectedContainers = new List<(string type, string name, string image, string[] arguments, int port)>
        {
            (
                "redis",
                $"rcftest-redis",
                "redis:6.0.10",
                Array.Empty<string>(),
                6379
            ),
            (
                "pubsub",
                $"rcftest-pubsub",
                "gcr.io/google.com/cloudsdktool/cloud-sdk:latest",
                new[] {
                    "gcloud",
                    "beta",
                    "emulators",
                    "pubsub",
                    "start",
                    "--host-port=0.0.0.0:9000"
                },
                9000
            ),
            (
                "datastore",
                $"rcftest-datastore",
                "gcr.io/google.com/cloudsdktool/cloud-sdk:latest",
                new[] {
                    "gcloud",
                    "beta",
                    "emulators",
                    "datastore",
                    "start",
                    // Firestore guarantees strong consistency now, so this
                    // should be reasonably safe.
                    "--consistency=1.0",
                    "--host-port=0.0.0.0:9001",
                    "--no-store-on-disk"
                },
                9001
            ),
        };
    }
}
