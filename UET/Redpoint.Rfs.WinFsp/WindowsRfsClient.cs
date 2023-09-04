namespace Redpoint.Rfs.WinFsp
{
    using Fsp;
    using Fsp.Interop;
    using Google.Protobuf;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading.Tasks;
    using FspFileInfo = Fsp.Interop.FileInfo;

    [SupportedOSPlatform("windows6.2")]
    public class WindowsRfsClient : FileSystemBase
    {
        private readonly WindowsRfs.WindowsRfsClient _client;
        private FileSystemHost? _host;

        public WindowsRfsClient(
            WindowsRfs.WindowsRfsClient client)
        {
            _client = client;
        }

        private class HandleFileNode
        {
            public required long Handle;
        }

        private static FspFileInfo ConvertFileInfo(FileInfo fileInfo)
        {
            return new FspFileInfo
            {
                FileAttributes = fileInfo.FileAttributes,
                ReparseTag = fileInfo.ReparseTag,
                AllocationSize = fileInfo.AllocationSize,
                FileSize = fileInfo.FileSize,
                CreationTime = fileInfo.CreationTime,
                LastAccessTime = fileInfo.LastAccessTime,
                LastWriteTime = fileInfo.LastWriteTime,
                ChangeTime = fileInfo.ChangeTime,
                IndexNumber = fileInfo.IndexNumber,
                HardLinks = fileInfo.HardLinks,
            };
        }

        public override int ExceptionHandler(Exception ex)
        {
            // @todo
        }

        public override int Init(object host)
        {
            _host = (FileSystemHost)host;
            // @todo
        }

        public override int GetVolumeInfo(out VolumeInfo VolumeInfo)
        {
            // @todo
        }

        public override int GetSecurityByName(
            string fileName, 
            out uint fileAttributes, 
            ref byte[] securityDescriptor)
        {
            var response = _client.GetSecurityByName(new GetSecurityByNameRequest
            {
                FileName = fileName,
                SecurityDescriptor = ByteString.CopyFrom(securityDescriptor),
            });
            fileAttributes = response.FileAttributes;
            securityDescriptor = response.SecurityDescriptor.ToByteArray();
            return response.Result;
        }

        public override int Create(
            string fileName,
            uint createOptions,
            uint grantedAccess,
            uint fileAttributes,
            byte[] securityDescriptor,
            ulong allocationSize,
            out object fileNode,
            out object fileDesc,
            out FspFileInfo fileInfo,
            out string normalizedName)
        {
            var response = _client.Create(new CreateRequest
            {
                FileName = fileName,
                CreateOptions = createOptions,
                GrantedAccess = grantedAccess,
                FileAttributes = fileAttributes,
                SecurityDescriptor = ByteString.CopyFrom(securityDescriptor),
                AllocationSize = allocationSize,
            });
            if (response.Success)
            {
                fileNode = new HandleFileNode
                {
                    Handle = response.Handle,
                };
                fileDesc = default!;
                fileInfo = ConvertFileInfo(response.FileInfo);
                normalizedName = response.NormalizedName;
            }
            else
            {
                fileNode = default!;
                fileDesc = default!;
                fileInfo = default!;
                normalizedName = default!;
            }
            return response.Result;
        }

        public override int Open(
            string fileName, 
            uint createOptions, 
            uint grantedAccess, 
            out object fileNode, 
            out object fileDesc,
            out FspFileInfo fileInfo,
            out string normalizedName)
        {
            var response = _client.Open(new OpenRequest
            {
                FileName = fileName,
                CreateOptions = createOptions,
                GrantedAccess = grantedAccess,
            });
            if (response.Success)
            {
                fileNode = new HandleFileNode
                {
                    Handle = response.Handle,
                };
                fileDesc = default!;
                fileInfo = ConvertFileInfo(response.FileInfo);
                normalizedName = response.NormalizedName;
            }
            else
            {
                fileNode = default!;
                fileDesc = default!;
                fileInfo = default!;
                normalizedName = default!;
            }
            return response.Result;
        }

        public override int Overwrite(
            object fileNode, 
            object fileDesc, 
            uint fileAttributes, 
            bool replaceFileAttributes, 
            ulong allocationSize, 
            out FspFileInfo fileInfo)
        {
            var response = _client.Overwrite(new OverwriteRequest
            {
                Handle = ((HandleFileNode)fileNode).Handle,
                FileAttributes = fileAttributes,
                ReplaceFileAttributes = replaceFileAttributes,
                AllocationSize = allocationSize,
            });
            if (response.Success)
            {
                fileInfo = ConvertFileInfo(response.FileInfo);
            }
            else
            {
                fileInfo = default!;
            }
            return response.Result;
        }

        public override void Cleanup(
            object fileNode, 
            object fileDesc, 
            string fileName, 
            uint flags)
        {
            _client.Cleanup(new CleanupRequest
            {
                Handle = ((HandleFileNode)fileNode).Handle,
                FileName = fileName,
                Flags = flags,
            });
        }

        // @todo: Continue implementing from
        // https://github.com/winfsp/winfsp/blob/master/tst/passthrough-dotnet/Program.cs

        public override int Read(
            object fileNode, 
            object fileDesc, 
            nint buffer, 
            ulong offset,
            uint length, 
            out uint bytesTransferred)
        {
            ulong operationHint = _host!.GetOperationRequestHint();
            _ = Task.Run(async () =>
            {
                var result = await _client.ReadAsync(new ReadRequest
                {
                    Handle = ((HandleFileNode)fileNode).Handle,
                    Offset = offset,
                    Length = length,
                });
                if (result.Success)
                {
                    unsafe
                    {
                        result.Buffer.Span.CopyTo(new Span<byte>((void*)buffer, unchecked((int)length)));
                    }
                }
                _host.SendReadResponse(
                    operationHint,
                    result.Result,
                    result.BytesTransferred);
            });
            bytesTransferred = 0;
            return STATUS_PENDING;
        }

        // @todo: Continue implementing from
        // https://github.com/winfsp/winfsp/blob/master/tst/passthrough-dotnet/Program.cs

        public override int ReadDirectory(
            object fileNode,
            object fileDesc, 
            string pattern,
            string marker, 
            nint buffer, 
            uint length, 
            out uint bytesTransferred)
        {
            // @note: This can return STATUS_PENDING.
        }

        public override int Write(
            object fileNode, 
            object fileDesc, 
            nint buffer,
            ulong offset, 
            uint length,
            bool writeToEndOfFile, 
            bool constrainedIo, 
            out uint bytesTransferred, 
            out FspFileInfo fileInfo)
        {
            // @note: This can return STATUS_PENDING.
        }
    }
}
