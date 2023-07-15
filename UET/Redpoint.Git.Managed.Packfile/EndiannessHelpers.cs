namespace Redpoint.Git.Managed.Packfile
{
    using System.Buffers.Binary;

    internal static class EndiannessHelpers
    {
        public static uint ConvertFromNetworkByteOrder(uint v)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BinaryPrimitives.ReverseEndianness(v);
            }
            return v;
        }

        public static ulong ConvertFromNetworkByteOrder(ulong v)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BinaryPrimitives.ReverseEndianness(v);
            }
            return v;
        }
    }
}