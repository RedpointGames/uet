using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Tftp.Net.Transfer;

namespace Tftp.Net
{
    /// <summary>
    /// Parses a ITftpCommand.
    /// </summary>
    class CommandParser
    {
        /// <summary>
        /// Parses an ITftpCommand from the given byte array. If the byte array cannot be parsed for some reason, a TftpParserException is thrown.
        /// </summary>
        public ITftpCommand Parse(byte[] message)
        {
            try
            {
                return ParseInternal(message);
            }
            catch (TftpParserException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new TftpParserException(e);
            }
        }

        private ITftpCommand ParseInternal(byte[] message)
        {
            TftpStreamReader reader = new TftpStreamReader(new MemoryStream(message));

            ushort opcode = reader.ReadUInt16();
            switch (opcode)
            {
                case ReadRequest.OpCode:
                    return ParseReadRequest(reader);

                case WriteRequest.OpCode:
                    return ParseWriteRequest(reader);

                case Data.OpCode:
                    return ParseData(reader);

                case Acknowledgement.OpCode:
                    return ParseAcknowledgement(reader);

                case Error.OpCode:
                    return ParseError(reader);

                case OptionAcknowledgement.OpCode:
                    return ParseOptionAcknowledgement(reader);

                default:
                    throw new TftpParserException("Invalid opcode");
            }
        }

        private OptionAcknowledgement ParseOptionAcknowledgement(TftpStreamReader reader)
        {
            IEnumerable<TransferOption> options = ParseTransferOptions(reader);
            return new OptionAcknowledgement(options);
        }

        private Error ParseError(TftpStreamReader reader)
        {
            ushort errorCode = reader.ReadUInt16();
            String message = ParseNullTerminatedString(reader);
            return new Error(errorCode, message);
        }

        private Acknowledgement ParseAcknowledgement(TftpStreamReader reader)
        {
            ushort blockNumber = reader.ReadUInt16();
            return new Acknowledgement(blockNumber);
        }

        private Data ParseData(TftpStreamReader reader)
        {
            ushort blockNumber = reader.ReadUInt16();
            byte[] data = reader.ReadBytes(10000);
            return new Data(blockNumber, data);
        }

        private WriteRequest ParseWriteRequest(TftpStreamReader reader)
        {
            String filename = ParseNullTerminatedString(reader);
            TftpTransferMode mode = ParseModeType(ParseNullTerminatedString(reader));
            IEnumerable<TransferOption> options = ParseTransferOptions(reader);
            return new WriteRequest(filename, mode, options);
        }

        private ReadRequest ParseReadRequest(TftpStreamReader reader)
        {
            String filename = ParseNullTerminatedString(reader);
            TftpTransferMode mode = ParseModeType(ParseNullTerminatedString(reader));
            IEnumerable<TransferOption> options = ParseTransferOptions(reader);
            return new ReadRequest(filename, mode, options);
        }

        private List<TransferOption> ParseTransferOptions(TftpStreamReader reader)
        {
            List<TransferOption> options = new List<TransferOption>();

            while (true)
            {
                String name;

                try
                {
                    name = ParseNullTerminatedString(reader);
                }
                catch (IOException)
                {
                    name = "";
                }

                if (name.Length == 0)
                    break;

                string value = ParseNullTerminatedString(reader);
                options.Add(new TransferOption(name, value));
            }
            return options;
        }

        private String ParseNullTerminatedString(TftpStreamReader reader)
        {
            byte b;
            StringBuilder str = new StringBuilder();
            while ((b = reader.ReadByte()) > 0)
            {
                str.Append((char)b);
            }

            return str.ToString();
        }

        private TftpTransferMode ParseModeType(String mode)
        {
            mode = mode.ToLowerInvariant();

            if (mode == "netascii")
                return TftpTransferMode.netascii;

            if (mode == "mail")
                return TftpTransferMode.mail;

            if (mode == "octet")
                return TftpTransferMode.octet;

            throw new TftpParserException("Unknown mode type: " + mode);
        }
    }

    [Serializable]
    class TftpParserException : Exception
    {
        public TftpParserException(String message)
            : base(message) { }

        public TftpParserException(Exception e)
            : base("Error while parsing message.", e) { }
    }
}
