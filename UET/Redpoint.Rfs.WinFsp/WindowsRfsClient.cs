namespace Redpoint.Rfs.WinFsp
{
    using Fsp;
    using Fsp.Interop;
    using Google.Protobuf;
    using System;
    using System.Runtime.Versioning;
    using System.Security.AccessControl;
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

        private static FspFileInfo ConvertFileInfo(RfsFileInfo fileInfo)
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

        public override int Init(object host)
        {
            _host = (FileSystemHost)host;
            _host.SectorSize = 4096;
            _host.SectorsPerAllocationUnit = 1;
            _host.MaxComponentLength = 255;
            _host.FileInfoTimeout = 1000;
            _host.CaseSensitiveSearch = false;
            _host.CasePreservedNames = true;
            _host.UnicodeOnDisk = true;
            _host.PersistentAcls = true;
            _host.PostCleanupWhenModifiedOnly = true;
            _host.PassQueryDirectoryPattern = true;
            _host.FlushAndPurgeOnCleanup = true;
            _host.VolumeCreationTime = _client.GetRootCreationTime(new GetRootCreationTimeRequest()).FileTime;
            _host.VolumeSerialNumber = 0;
            return STATUS_SUCCESS;
        }

        public override int GetVolumeInfo(out VolumeInfo volumeInfo)
        {
            volumeInfo = default;
            var result = _client.GetVolumeInfo(new GetVolumeInfoRequest());
            volumeInfo.TotalSize = result.TotalSize;
            volumeInfo.FreeSize = result.TotalFreeSpace;
            return STATUS_SUCCESS;
        }

        public override int GetSecurityByName(
            string fileName,
            out uint fileAttributes,
            ref byte[] securityDescriptor)
        {
            var response = _client.GetSecurityByName(new GetSecurityByNameRequest
            {
                FileName = fileName,
                IncludeSecurityDescriptor = securityDescriptor != null,
            });
            fileAttributes = response.FileAttributes;
            if (securityDescriptor != null)
            {
                securityDescriptor = response.SecurityDescriptor.ToByteArray();
            }
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

        public override void Close(
            object fileNode,
            object fileDesc)
        {
            _client.Close(new CloseRequest
            {
                Handle = ((HandleFileNode)fileNode).Handle,
            });
        }

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
                var didSendResult = false;
                try
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
                    didSendResult = true;
                }
                finally
                {
                    if (!didSendResult)
                    {
                        _host.SendReadResponse(
                            operationHint,
                            STATUS_IO_DEVICE_ERROR,
                            0);
                    }
                }
            });
            bytesTransferred = 0;
            return STATUS_PENDING;
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
            ulong operationHint = _host!.GetOperationRequestHint();
            _ = Task.Run(async () =>
            {
                var didSendResult = false;
                try
                {
                    ByteString protobufBuffer;
                    unsafe
                    {
                        protobufBuffer = ByteString.CopyFrom(new ReadOnlySpan<byte>((void*)buffer, unchecked((int)length)));
                    }

                    var result = await _client.WriteAsync(new WriteRequest
                    {
                        Handle = ((HandleFileNode)fileNode).Handle,
                        Offset = offset,
                        Length = length,
                        Buffer = protobufBuffer,
                        WriteToEndOfFile = writeToEndOfFile,
                        ConstrainedIo = constrainedIo,
                    });
                    FspFileInfo asyncFileInfo = default;
                    if (result.Success)
                    {
                        asyncFileInfo = ConvertFileInfo(result.FileInfo);
                    }
                    _host.SendWriteResponse(
                        operationHint,
                        result.Result,
                        result.BytesTransferred,
                        ref asyncFileInfo);
                    didSendResult = true;
                }
                finally
                {
                    if (!didSendResult)
                    {
                        FspFileInfo asyncFileInfo = default;
                        _host.SendWriteResponse(
                            operationHint,
                            STATUS_IO_DEVICE_ERROR,
                            0,
                            ref asyncFileInfo);
                    }
                }
            });
            bytesTransferred = 0;
            fileInfo = default;
            return STATUS_PENDING;
        }

        public override int Flush(
            object fileNode,
            object fileDesc,
            out FspFileInfo fileInfo)
        {
            if (fileDesc == null)
            {
                fileInfo = default;
                return STATUS_SUCCESS;
            }
            var result = _client.Flush(new FlushRequest
            {
                Handle = ((HandleFileNode)fileNode).Handle,
            });
            if (result.Success)
            {
                fileInfo = ConvertFileInfo(result.FileInfo);
            }
            else
            {
                fileInfo = default;
            }
            return result.Result;
        }

        public override int GetFileInfo(
            object fileNode,
            object fileDesc,
            out FspFileInfo fileInfo)
        {
            var result = _client.GetFileInfo(new GetFileInfoRequest
            {
                Handle = ((HandleFileNode)fileNode).Handle,
            });
            if (result.Success)
            {
                fileInfo = ConvertFileInfo(result.FileInfo);
            }
            else
            {
                fileInfo = default;
            }
            return result.Result;
        }

        public override int SetBasicInfo(
            object fileNode,
            object fileDesc,
            uint fileAttributes,
            ulong creationTime,
            ulong lastAccessTime,
            ulong lastWriteTime,
            ulong changeTime,
            out FspFileInfo fileInfo)
        {
            var result = _client.SetBasicInfo(new SetBasicInfoRequest
            {
                Handle = ((HandleFileNode)fileNode).Handle,
                FileAttributes = fileAttributes,
                CreationTime = creationTime,
                LastAccessTime = lastAccessTime,
                LastWriteTime = lastWriteTime,
                ChangeTime = changeTime,
            });
            if (result.Success)
            {
                fileInfo = ConvertFileInfo(result.FileInfo);
            }
            else
            {
                fileInfo = default;
            }
            return result.Result;
        }

        public override int SetFileSize(
            object fileNode,
            object fileDesc,
            ulong newSize,
            bool setAllocationSize,
            out FspFileInfo fileInfo)
        {
            var result = _client.SetFileSize(new SetFileSizeRequest
            {
                Handle = ((HandleFileNode)fileNode).Handle,
                NewSize = newSize,
                SetAllocationSize = setAllocationSize,
            });
            if (result.Success)
            {
                fileInfo = ConvertFileInfo(result.FileInfo);
            }
            else
            {
                fileInfo = default;
            }
            return result.Result;
        }

        public override int CanDelete(
            object fileNode,
            object fileDesc,
            string fileName)
        {
            var result = _client.CanDelete(new CanDeleteRequest
            {
                Handle = ((HandleFileNode)fileNode).Handle,
            });
            return result.Result;
        }

        public override int Rename(
            object fileNode,
            object fileDesc,
            string fileName,
            string newFileName,
            bool replaceIfExists)
        {
            var result = _client.Rename(new RenameRequest
            {
                FileName = fileName,
                NewFileName = newFileName,
                ReplaceIfExists = replaceIfExists,
            });
            return result.Result;
        }

        public override int GetSecurity(
            object fileNode,
            object fileDesc,
            ref byte[] securityDescriptor)
        {
            var result = _client.GetSecurity(new GetSecurityRequest
            {
                Handle = ((HandleFileNode)fileNode).Handle,
            });
            if (result.Success)
            {
                securityDescriptor = result.SecurityDescriptor.ToByteArray();
            }
            return result.Result;
        }

        public override int SetSecurity(
            object fileNode,
            object fileDesc,
            AccessControlSections sections,
            byte[] securityDescriptor)
        {
            var result = _client.SetSecurity(new SetSecurityRequest
            {
                Handle = ((HandleFileNode)fileNode).Handle,
                Sections = (int)sections,
                SecurityDescriptor = ByteString.CopyFrom(securityDescriptor),
            });
            return result.Result;
        }

        public override int ReadDirectory(
            object fileNode,
            object fileDesc,
            string? pattern,
            string? marker,
            nint buffer,
            uint length,
            out uint bytesTransferred)
        {
            ulong operationHint = _host!.GetOperationRequestHint();
            var stream = _client.ReadDirectory(new ReadDirectoryRequest
            {
                Handle = ((HandleFileNode)fileNode).Handle,
                HasPattern = pattern != null,
                Pattern = pattern ?? string.Empty,
                HasMarker = marker != null,
                Marker = marker ?? string.Empty,
            });
            _ = Task.Run(async () =>
            {
                bool iterationEndedDueToFullBuffer = false;
                uint asyncBytesTransferred = 0;
                var didSendResult = false;
                try
                {
                    while (await stream.ResponseStream.MoveNext(CancellationToken.None))
                    {
                        var current = stream.ResponseStream.Current;
                        switch (current.ResponseCase)
                        {
                            case ReadDirectoryResponse.ResponseOneofCase.Entry:
                                {
                                    var dirInfo = default(DirInfo);
                                    dirInfo.FileInfo = ConvertFileInfo(current.Entry.FileInfo);
                                    dirInfo.SetFileNameBuf(current.Entry.FileName);
                                    var result = Fsp.Interop.Api.FspFileSystemAddDirInfo(
                                        ref dirInfo,
                                        buffer,
                                        length,
                                        out asyncBytesTransferred);
                                    if (!result)
                                    {
                                        iterationEndedDueToFullBuffer = true;
                                    }
                                }
                                break;
                            case ReadDirectoryResponse.ResponseOneofCase.Result:
                                if (!iterationEndedDueToFullBuffer)
                                {
                                    Fsp.Interop.Api.FspFileSystemEndDirInfo(
                                        buffer,
                                        length,
                                        out asyncBytesTransferred);
                                }
                                _host.SendReadDirectoryResponse(
                                    operationHint,
                                    current.Result,
                                    asyncBytesTransferred);
                                didSendResult = true;
                                break;
                        }
                    }
                }
                finally
                {
                    if (!didSendResult)
                    {
                        _host.SendReadDirectoryResponse(
                            operationHint,
                            STATUS_IO_DEVICE_ERROR,
                            0);
                    }
                }
            });
            bytesTransferred = 0;
            return STATUS_PENDING;
        }
    }
}
