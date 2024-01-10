namespace Redpoint.GrpcPipes.Transport.Tcp
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

    internal sealed class TcpGrpcPipeServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T> : IGrpcPipeServer<T> where T : class
    {
        public Task StartAsync()
        {
            throw new NotImplementedException();
        }

        public Task StopAsync()
        {
            throw new NotImplementedException();
        }
    }
}
