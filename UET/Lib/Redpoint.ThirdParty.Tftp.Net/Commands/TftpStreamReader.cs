using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Sockets;

namespace Tftp.Net
{
    class TftpStreamReader
    {
        private readonly Stream stream;

        public TftpStreamReader(Stream stream)
        {
            this.stream = stream;
        }

        public ushort ReadUInt16()
        {
            int byte1 = stream.ReadByte();
            int byte2 = stream.ReadByte();
            return (ushort)((byte)byte1 << 8 | (byte)byte2);
        }

        public byte ReadByte()
        {
            int nextByte = stream.ReadByte();

            if (nextByte == -1)
                throw new IOException();

            return (byte)nextByte;
        }

        public byte[] ReadBytes(int maxBytes)
        {
            byte[] buffer = new byte[maxBytes];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            if (bytesRead == -1)
                throw new IOException();

            Array.Resize(ref buffer, bytesRead);
            return buffer;
        }
    }
}
