namespace Redpoint.ProcessExecution.Windows.Ntdll
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal readonly unsafe struct OBJECT_ATTRIBUTES
    {
        public readonly uint Length;
        public readonly nint RootDirectory;
        public readonly UNICODE_STRING* ObjectName;
        public readonly OBJECT_ATTRIBUTES_FLAGS Attributes;
        public readonly nint SecurityDescriptor;
        public readonly nint SecurityQualityOfService;

        public OBJECT_ATTRIBUTES(
            UNICODE_STRING* objectName, 
            OBJECT_ATTRIBUTES_FLAGS flags)
        {
            Length = (uint)sizeof(OBJECT_ATTRIBUTES);
            RootDirectory = nint.Zero;
            ObjectName = objectName;
            Attributes = flags;
            SecurityDescriptor = nint.Zero;
            SecurityQualityOfService = nint.Zero;
        }

        public OBJECT_ATTRIBUTES(
            UNICODE_STRING* objectName,
            OBJECT_ATTRIBUTES_FLAGS flags,
            nint rootDirectoryHandle)
        {
            Length = (uint)sizeof(OBJECT_ATTRIBUTES);
            RootDirectory = rootDirectoryHandle;
            ObjectName = objectName;
            Attributes = flags;
            SecurityDescriptor = nint.Zero;
            SecurityQualityOfService = nint.Zero;
        }
    }
}
