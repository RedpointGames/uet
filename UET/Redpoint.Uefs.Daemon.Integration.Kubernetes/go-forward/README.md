# go-forward

Running gRPC over UNIX sockets is currently broken in C# / ASP.NET Core. Go clients (like the Kubelet) can't talk to gRPC services written in C# running on a UNIX socket.

go-forward simply proxies a UNIX socket to a TCP socket, since gRPC on TCP works totally fine in ASP.NET Core. The UEFS daemon will automatically start this process when it detects Kubernetes is running.