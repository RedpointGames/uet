# Redpoint.Windows.HostNetworkingSerivce

This library provides managed access to the [Host Networking Service (HNS)](https://learn.microsoft.com/en-us/virtualization/windowscontainers/container-networking/architecture#container-network-management-with-host-network-service) in a way that is compatible with trimming and AOT.

This API currently allows you to:

- List networks
- Create a new network
- Delete an existing network
- Get a list of endpoints
- Get a list of policy lists
- Delete an existing policy list