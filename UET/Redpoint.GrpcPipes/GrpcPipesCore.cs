namespace Redpoint.GrpcPipes
{
    using Grpc.Core;

    public static class GrpcPipesCore
    {
        public static T CreateClient<T>(string pipeName, Func<CallInvoker, T> constructor)
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsGrpcPipeFactory().CreateClient(pipeName, constructor);
            }
            else if (OperatingSystem.IsMacOS() ||
                OperatingSystem.IsLinux())
            {
                return new UnixGrpcPipeFactory().CreateClient(pipeName, constructor);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }
    }
}