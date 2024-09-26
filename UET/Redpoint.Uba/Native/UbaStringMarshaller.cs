namespace Redpoint.Uba.Native
{
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.Marshalling;

    /// <summary>
    /// A custom string marshaller for [LibraryImport] that handles UBA accepting UTF-16 or UTF-8 based on the current operating system. The <see cref="LibraryImportAttribute.StringMarshalling"/> parameter no longer accepts 'Auto', so we need to implement equivalent functionality with the custom marshaller here.
    /// </summary>
    [CustomMarshaller(typeof(string), MarshalMode.Default, typeof(UbaStringMarshaller))]
    internal static unsafe class UbaStringMarshaller
    {
        public static byte* ConvertToUnmanaged(string? managed)
        {
            if (OperatingSystem.IsWindows())
            {
                return (byte*)Utf16StringMarshaller.ConvertToUnmanaged(managed);
            }
            else
            {
                return Utf8StringMarshaller.ConvertToUnmanaged(managed);
            }
        }

        public static string? ConvertToManaged(byte* unmanaged)
        {
            if (OperatingSystem.IsWindows())
            {
                return Utf16StringMarshaller.ConvertToManaged((ushort*)unmanaged);
            }
            else
            {
                return Utf8StringMarshaller.ConvertToManaged(unmanaged);
            }
        }

        public static void Free(byte* unmanaged)
        {
            if (OperatingSystem.IsWindows())
            {
                Utf16StringMarshaller.Free((ushort*)unmanaged);
            }
            else
            {
                Utf8StringMarshaller.Free(unmanaged);
            }
        }
    }
}
