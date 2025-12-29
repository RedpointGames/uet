using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Tftp.Net.Transfer;

namespace Tftp.Net
{
    class OptionAcknowledgement : ITftpCommand
    {
        public const ushort OpCode = 6;
        public IEnumerable<TransferOption> Options { get; private set; }

        public OptionAcknowledgement(IEnumerable<TransferOption> options)
        {
            this.Options = options;
        }

        public Task Visit(ITftpCommandVisitor visitor)
        {
            return visitor.OnOptionAcknowledgement(this);
        }
    }
}
