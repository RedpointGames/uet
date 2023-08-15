namespace Redpoint.ProcessExecution.Windows.Ntdll
{
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/secauthz/access-mask.
    /// </remarks>
    [Flags]
    internal enum ACCESS_MASK : uint
    {
        // https://learn.microsoft.com/en-us/windows/win32/devnotes/ntopendirectoryobject
        DIRECTORY_QUERY = 1u,
        DIRECTORY_TRAVERSE = 1u << 1,
        DIRECTORY_CREATE_OBJECT = 1u << 2,
        DIRECTORY_CREATE_SUBDIRECTORY = 1u << 3,
        DIRECTORY_ALL_ACCESS = 
            STANDARD_RIGHTS_REQUIRED |
            DIRECTORY_QUERY |
            DIRECTORY_TRAVERSE |
            DIRECTORY_CREATE_OBJECT |
            DIRECTORY_CREATE_SUBDIRECTORY,

        // Everything below is standard across all access masks, and covered in:
        // https://learn.microsoft.com/en-us/windows/win32/secauthz/access-mask-format
        DELETE = 1u << 16,
        READ_CONTROL = 1u << 17,
        WRITE_DAC = 1u << 18,
        WRITE_OWNER = 1u << 19,
        SYNCHRONIZE = 1u << 20,

        STANDARD_RIGHTS_REQUIRED = DELETE | READ_CONTROL | WRITE_DAC | WRITE_OWNER,

        STANDARD_RIGHTS_READ = READ_CONTROL,
        STANDARD_RIGHTS_WRITE = READ_CONTROL,
        STANDARD_RIGHTS_EXECUTE = READ_CONTROL,

        STANDARD_RIGHTS_ALL = STANDARD_RIGHTS_REQUIRED | WRITE_OWNER,

        SPECIFIC_RIGHTS_ALL = 0x0000FFFFu,

        ACCESS_SYSTEM_SECURITY = 1u << 24,
        MAXIMUM_ALLOWED = 1u << 25,

        // 26 - Reserved
        // 27 - Reserved

        GENERIC_ALL = 1u << 28,
        GENERIC_EXECUTE = 1u << 29,
        GENERIC_WRITE = 1u << 30,
        GENERIC_READ = 1u << 31,
    }
}
