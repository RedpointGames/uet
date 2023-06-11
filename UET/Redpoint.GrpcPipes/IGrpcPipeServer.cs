namespace Redpoint.GrpcPipes
{
    using System.Diagnostics.CodeAnalysis;

    public interface IGrpcPipeServer<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] T> where T : class
    {
        void Start();

        Task StopAsync();
    }
}