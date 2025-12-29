using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tftp.Net
{
    /// <summary>
    /// Serializes an ITftpCommand into a stream of bytes.
    /// </summary>
    class CommandSerializer
    {
        /// <summary>
        /// Call this method to serialize the given <code>command</code> using the given <code>writer</code>.
        /// </summary>
        public void Serialize(ITftpCommand command, Stream stream)
        {
            CommandComposerVisitor visitor = new CommandComposerVisitor(stream);
            command.Visit(visitor);
        }

        private class CommandComposerVisitor : ITftpCommandVisitor
        {
            private readonly TftpStreamWriter writer;

            public CommandComposerVisitor(Stream stream)
            {
                this.writer = new TftpStreamWriter(stream);
            }

            private void OnReadOrWriteRequest(ReadOrWriteRequest command)
            {
                writer.WriteBytes(Encoding.ASCII.GetBytes(command.Filename));
                writer.WriteByte(0);
                writer.WriteBytes(Encoding.ASCII.GetBytes(command.Mode.ToString()));
                writer.WriteByte(0);

                if (command.Options != null)
                {
                    foreach (var option in command.Options)
                    {
                        writer.WriteBytes(Encoding.ASCII.GetBytes(option.Name));
                        writer.WriteByte(0);
                        writer.WriteBytes(Encoding.ASCII.GetBytes(option.Value));
                        writer.WriteByte(0);
                    }
                }
            }

            public Task OnReadRequest(ReadRequest command)
            {
                writer.WriteUInt16(ReadRequest.OpCode);
                OnReadOrWriteRequest(command);
                return Task.CompletedTask;
            }

            public Task OnWriteRequest(WriteRequest command)
            {
                writer.WriteUInt16(WriteRequest.OpCode);
                OnReadOrWriteRequest(command);
                return Task.CompletedTask;
            }

            public Task OnData(Data command)
            {
                writer.WriteUInt16(Data.OpCode);
                writer.WriteUInt16(command.BlockNumber);
                writer.WriteBytes(command.Bytes);
                return Task.CompletedTask;
            }

            public Task OnAcknowledgement(Acknowledgement command)
            {
                writer.WriteUInt16(Acknowledgement.OpCode);
                writer.WriteUInt16(command.BlockNumber);
                return Task.CompletedTask;
            }

            public Task OnError(Error command)
            {
                writer.WriteUInt16(Error.OpCode);
                writer.WriteUInt16(command.ErrorCode);
                writer.WriteBytes(Encoding.ASCII.GetBytes(command.Message));
                writer.WriteByte(0);
                return Task.CompletedTask;
            }

            public Task OnOptionAcknowledgement(OptionAcknowledgement command)
            {
                writer.WriteUInt16(OptionAcknowledgement.OpCode);

                foreach (var option in command.Options)
                {
                    writer.WriteBytes(Encoding.ASCII.GetBytes(option.Name));
                    writer.WriteByte(0);
                    writer.WriteBytes(Encoding.ASCII.GetBytes(option.Value));
                    writer.WriteByte(0);
                }

                return Task.CompletedTask;
            }
        }
    }
}
