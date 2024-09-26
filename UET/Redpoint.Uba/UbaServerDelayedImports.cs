namespace Redpoint.Uba
{
    using Redpoint.Uba.Native;
    using System.Runtime.InteropServices;

    internal partial class UbaServerDelayedImports
    {
        static UbaServerDelayedImports()
        {
            UbaNative.ThrowIfNotInitialized();
        }

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial nint CreateServer(
            nint logger,
            int workerCount,
            int sendSize,
            int receiveTimeoutSeconds,
            [MarshalAs(UnmanagedType.I1)] bool useQuic);
    }
}
