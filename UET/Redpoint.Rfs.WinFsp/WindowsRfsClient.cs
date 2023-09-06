namespace Redpoint.Rfs.WinFsp
{
    using Fsp;
    using Fsp.Interop;
    using Google.Protobuf;
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using System;
    using System.Collections;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Security.AccessControl;
    using System.Text;
    using System.Threading.Tasks;
    using FspFileInfo = Fsp.Interop.FileInfo;

    [SupportedOSPlatform("windows6.2")]
    public class WindowsRfsClient : FileSystemBase
    {
        private static readonly DateTimeOffset _rootCreationTime = DateTimeOffset.UtcNow;
        private static DirectoryEntryComparer _directoryEntryComparer =
            new DirectoryEntryComparer();
        private readonly ILogger _logger;
        private readonly WindowsRfs.WindowsRfsClient _client;
        private readonly Dictionary<string, int> _reparsePoints;
        private readonly byte[] _rootSecurityDescriptor;
        private HashSet<string> _additionalSubdirectories;
        private Dictionary<string, HashSet<string>> _additionalSubdirectoryEntries;
        private Dictionary<string, string> _additionalJunctions;
        private Dictionary<string, Dictionary<string, string>> _additionalJunctionEntries;
        private FileSystemHost? _host;

        public WindowsRfsClient(
            ILogger logger,
            WindowsRfs.WindowsRfsClient client)
        {
            _logger = logger;
            _client = client;
            _reparsePoints = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            _additionalSubdirectories = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            _additionalSubdirectoryEntries = new Dictionary<string, HashSet<string>>(StringComparer.InvariantCultureIgnoreCase);
            _additionalJunctions = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            _additionalJunctionEntries = new Dictionary<string, Dictionary<string, string>>(StringComparer.InvariantCultureIgnoreCase);
            var securityDescriptor = new RawSecurityDescriptor("O:WDG:WDD:PAI(A;;FA;;;WD)");
            _rootSecurityDescriptor = new byte[securityDescriptor.BinaryLength];
            securityDescriptor.GetBinaryForm(_rootSecurityDescriptor, 0);
        }

        private void RecomputeReparsePointIndex()
        {
            var additionalSubdirectories = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var additionalSubdirectoryEntries = new Dictionary<string, HashSet<string>>(StringComparer.InvariantCultureIgnoreCase);
            var additionalJunctions = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            var additionalJunctionEntries = new Dictionary<string, Dictionary<string, string>>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var reparsePoint in _reparsePoints.Keys)
            {
                var components = reparsePoint.TrimEnd('\\').Split('\\');
                components[0] = components[0][0].ToString(); // @note: Drive letter; remove the ':' character.
                var parentPath = "\\";
                var currentPath = string.Empty;
                for (var c = 0; c < components.Length; c++)
                {
                    currentPath += "\\" + components[c];
                    if (c == components.Length - 1)
                    {
                        if (!additionalJunctionEntries.ContainsKey(parentPath))
                        {
                            additionalJunctionEntries.Add(parentPath, new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase));
                        }
                        additionalJunctionEntries[parentPath].Add(components[c], DosDevicePath.GetFullyQualifiedDosDevicePath(reparsePoint));
                        additionalJunctions[currentPath] = DosDevicePath.GetFullyQualifiedDosDevicePath(reparsePoint);
                    }
                    else
                    {
                        if (!additionalSubdirectoryEntries.ContainsKey(parentPath))
                        {
                            additionalSubdirectoryEntries.Add(parentPath, new HashSet<string>(StringComparer.InvariantCultureIgnoreCase));
                        }
                        additionalSubdirectoryEntries[parentPath].Add(components[c]);
                        additionalSubdirectories.Add(currentPath);
                    }
                    parentPath = currentPath;
                }
            }
            _additionalSubdirectories = additionalSubdirectories;
            _additionalSubdirectoryEntries = additionalSubdirectoryEntries;
            _additionalJunctions = additionalJunctions;
            _additionalJunctionEntries = additionalJunctionEntries;
        }

        public void AddAdditionalReparsePoints(string[] reparsePoints)
        {
            foreach (var reparsePoint in reparsePoints)
            {
                if (!_reparsePoints.ContainsKey(reparsePoint))
                {
                    _reparsePoints[reparsePoint] = 0;
                }
                _reparsePoints[reparsePoint]++;
            }
            RecomputeReparsePointIndex();
        }

        public void RemoveAdditionalReparsePoints(string[] reparsePoints)
        {
            foreach (var reparsePoint in reparsePoints)
            {
                if (_reparsePoints.ContainsKey(reparsePoint))
                {
                    _reparsePoints[reparsePoint]--;
                    if (_reparsePoints[reparsePoint] <= 0)
                    {
                        _reparsePoints.Remove(reparsePoint);
                    }
                }
            }
            RecomputeReparsePointIndex();
        }

        private class HandleFileNode
        {
            public required long Handle;
            public required string FileName;
        }

        private class VirtualFileNode
        {
            public required DirectoryInfo DirectoryInfo;
            public required HashSet<string> Subdirectories { get; set; }
            public required Dictionary<string, string> Junctions { get; set; }
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

        public override int ExceptionHandler(Exception ex)
        {
            var hresult = ex.HResult;
            if (0x80070000 == (hresult & 0xFFFF0000))
            {
                // _logger.LogError(ex, ex.Message);
                return FileSystemBase.NtStatusFromWin32((UInt32)hresult & 0xFFFF);
            }
            else
            {
                _logger.LogError(ex, ex.Message);
                return FileSystemBase.STATUS_UNEXPECTED_IO_ERROR;
            }
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
            _host.ReparsePoints = true;
            _host.ReparsePointsAccessCheck = true;
            return STATUS_SUCCESS;
        }

        public override int GetReparsePointByName(string fileName, bool isDirectory, ref byte[] reparseData)
        {
            // _logger.LogInformation($"GetReparsePointByName: {fileName}");

            var junctions = _additionalJunctions;
            if (junctions.ContainsKey(fileName))
            {
                var target = junctions[fileName];
                var targetByteLength = (ushort)(target.Length * 2);

                using (var memory = new MemoryStream())
                {
                    memory.Write(BitConverter.GetBytes((uint)2684354563U));
                    memory.Write(BitConverter.GetBytes((ushort)(targetByteLength + 12)));
                    memory.Write(BitConverter.GetBytes((ushort)0));
                    memory.Write(BitConverter.GetBytes((ushort)0));
                    memory.Write(BitConverter.GetBytes((ushort)targetByteLength));
                    memory.Write(BitConverter.GetBytes((ushort)(targetByteLength + 2)));
                    memory.Write(BitConverter.GetBytes((ushort)0));
                    memory.Write(Encoding.Unicode.GetBytes(target));
                    memory.Write(new byte[] { 0, 0 });
                    memory.Write(new byte[] { 0, 0 });
                    reparseData = memory.ToArray();
                }

                return STATUS_SUCCESS;
            }
            else
            {
                return STATUS_IO_DEVICE_ERROR;
            }
        }

        public override int GetVolumeInfo(out VolumeInfo volumeInfo)
        {
            // _logger.LogInformation($"GetVolumeInfo");

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
            if (_additionalSubdirectories.Contains(fileName))
            {
                fileAttributes = (uint)FileAttributes.Directory;
                securityDescriptor = _rootSecurityDescriptor;
                // _logger.LogInformation($"GetSecurityByName: {fileName} = STATUS_SUCCESS");
                return STATUS_SUCCESS;
            }

            if (_additionalJunctions.ContainsKey(fileName))
            {
                fileAttributes = (uint)(FileAttributes.ReparsePoint | FileAttributes.Directory);
                securityDescriptor = _rootSecurityDescriptor;
                // _logger.LogInformation($"GetSecurityByName: {fileName} = STATUS_SUCCESS");
                return STATUS_SUCCESS;
            }

            if (_additionalJunctions.Any(x => fileName.StartsWith(x.Key + "\\")))
            {
                fileAttributes = 0;
                securityDescriptor = _rootSecurityDescriptor;
                // _logger.LogInformation($"GetSecurityByName: {fileName} = STATUS_REPARSE");
                return STATUS_REPARSE;
            }

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
            // _logger.LogInformation($"GetSecurityByName: {fileName} = {response.Result:X}");
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
                    FileName = fileName,
                };
                fileDesc = default!;
                fileInfo = ConvertFileInfo(response.FileInfo);
                normalizedName = response.NormalizedName;
                // _logger.LogInformation($"Create: {fileName} (normalized name: {response.NormalizedName})");
            }
            else
            {
                fileNode = default!;
                fileDesc = default!;
                fileInfo = default!;
                normalizedName = default!;
            }
            // _logger.LogInformation($"Create: {fileName} = {response.Result:X}");
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
                    FileName = fileName,
                };
                fileDesc = default!;
                fileInfo = ConvertFileInfo(response.FileInfo);
                normalizedName = response.NormalizedName;
                // _logger.LogInformation($"Open: {fileName} (normalized name: {response.NormalizedName})");
            }
            else if (_additionalSubdirectories.Contains(fileName))
            {
                // This is an entirely client-side directory.
                fileNode = new VirtualFileNode
                {
                    DirectoryInfo = new DirectoryInfo(WindowsRfsUtil.RealPath(fileName)),
                    Subdirectories = _additionalSubdirectoryEntries.ContainsKey(fileName) ? _additionalSubdirectoryEntries[fileName] : new HashSet<string>(),
                    Junctions = _additionalJunctionEntries.ContainsKey(fileName) ? _additionalJunctionEntries[fileName] : new Dictionary<string, string>(),
                };
                fileDesc = default!;
                fileInfo = WindowsRfsVirtual.GetVirtualDirectoryOnClient(WindowsRfsUtil.RealPath(fileName));
                normalizedName = default!;
                // _logger.LogInformation($"Open: {fileName} = STATUS_SUCCESS");
                return STATUS_SUCCESS;
            }
            else
            {
                fileNode = default!;
                fileDesc = default!;
                fileInfo = default!;
                normalizedName = default!;
            }
            // _logger.LogInformation($"Open: {fileName} = {response.Result:X}");
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
            if (fileNode is HandleFileNode handleFileNode)
            {
                var response = _client.Overwrite(new OverwriteRequest
                {
                    Handle = handleFileNode.Handle,
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
            else if (fileNode is VirtualFileNode virtualFileNode)
            {
                fileInfo = WindowsRfsVirtual.GetVirtualDirectoryOnClient(virtualFileNode.DirectoryInfo.FullName);
                return STATUS_ACCESS_DENIED;
            }
            else
            {
                fileInfo = default;
                return STATUS_ACCESS_DENIED;
            }
        }

        public override void Cleanup(
            object fileNode,
            object fileDesc,
            string? fileName,
            uint flags)
        {
            if (fileNode is HandleFileNode handleFileNode)
            {
                _client.Cleanup(new CleanupRequest
                {
                    Handle = handleFileNode.Handle,
                    FileName = fileName ?? string.Empty,
                    Flags = flags,
                });
            }
        }

        public override void Close(
            object fileNode,
            object fileDesc)
        {
            if (fileNode is HandleFileNode handleFileNode)
            {
                _client.Close(new CloseRequest
                {
                    Handle = handleFileNode.Handle,
                });
            }
        }

        public override int Read(
            object fileNode,
            object fileDesc,
            nint buffer,
            ulong offset,
            uint length,
            out uint bytesTransferred)
        {
            if (fileNode is HandleFileNode handleFileNode)
            {
#if ASYNC
                ulong operationHint = _host!.GetOperationRequestHint();
                _ = Task.Run(async () =>
                {
                    var didSendResult = false;
                    try
                    {
                        var result = await _client.ReadAsync(new ReadRequest
                        {
                            Handle = handleFileNode.Handle,
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
#else
                var result = _client.Read(new ReadRequest
                {
                    Handle = handleFileNode.Handle,
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
                bytesTransferred = result.BytesTransferred;
                return result.Result;
#endif
            }
            else
            {
                bytesTransferred = 0;
                return STATUS_ACCESS_DENIED;
            }
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
            if (fileNode is HandleFileNode handleFileNode)
            {
#if ASYNC
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
                            Handle = handleFileNode.Handle,
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
#else
                ByteString data;
                unsafe
                {
                    data = ByteString.CopyFrom(new ReadOnlySpan<byte>((void*)buffer, unchecked((int)length)));
                }
                var result = _client.Write(new WriteRequest
                {
                    Handle = handleFileNode.Handle,
                    Offset = offset,
                    Length = length,
                    Buffer = data,
                });
                bytesTransferred = result.BytesTransferred;
                if (result.Success)
                {
                    fileInfo = ConvertFileInfo(result.FileInfo);
                }
                else
                {
                    fileInfo = default;
                }
                return result.Result;
#endif
            }
            else if (fileNode is VirtualFileNode virtualFileNode)
            {
                bytesTransferred = 0;
                fileInfo = WindowsRfsVirtual.GetVirtualDirectoryOnClient(virtualFileNode.DirectoryInfo.FullName);
                return STATUS_ACCESS_DENIED;
            }
            else
            {
                bytesTransferred = 0;
                fileInfo = default;
                return STATUS_ACCESS_DENIED;
            }
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

            if (fileNode is HandleFileNode handleFileNode)
            {
                var result = _client.Flush(new FlushRequest
                {
                    Handle = handleFileNode.Handle,
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
            else if (fileNode is VirtualFileNode virtualFileNode)
            {
                fileInfo = WindowsRfsVirtual.GetVirtualDirectoryOnClient(virtualFileNode.DirectoryInfo.FullName);
                return STATUS_SUCCESS;
            }
            else
            {
                fileInfo = default;
                return STATUS_NOT_FOUND;
            }
        }

        public override int GetFileInfo(
            object fileNode,
            object fileDesc,
            out FspFileInfo fileInfo)
        {
            if (fileNode is HandleFileNode handleFileNode)
            {
                var result = _client.GetFileInfo(new GetFileInfoRequest
                {
                    Handle = handleFileNode.Handle,
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
            else if (fileNode is VirtualFileNode virtualFileNode)
            {
                fileInfo = WindowsRfsVirtual.GetVirtualDirectoryOnClient(virtualFileNode.DirectoryInfo.FullName);
                return STATUS_SUCCESS;
            }
            else
            {
                fileInfo = default;
                return STATUS_NOT_FOUND;
            }
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
            if (fileNode is HandleFileNode handleFileNode)
            {
                var result = _client.SetBasicInfo(new SetBasicInfoRequest
                {
                    Handle = handleFileNode.Handle,
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
            else if (fileNode is VirtualFileNode virtualFileNode)
            {
                fileInfo = WindowsRfsVirtual.GetVirtualDirectoryOnClient(virtualFileNode.DirectoryInfo.FullName);
                return STATUS_SUCCESS;
            }
            else
            {
                fileInfo = default;
                return STATUS_NOT_FOUND;
            }
        }

        public override int SetFileSize(
            object fileNode,
            object fileDesc,
            ulong newSize,
            bool setAllocationSize,
            out FspFileInfo fileInfo)
        {
            if (fileNode is HandleFileNode handleFileNode)
            {
                var result = _client.SetFileSize(new SetFileSizeRequest
                {
                    Handle = handleFileNode.Handle,
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
            else if (fileNode is VirtualFileNode virtualFileNode)
            {
                fileInfo = WindowsRfsVirtual.GetVirtualDirectoryOnClient(virtualFileNode.DirectoryInfo.FullName);
                return STATUS_SUCCESS;
            }
            else
            {
                fileInfo = default;
                return STATUS_NOT_FOUND;
            }
        }

        public override int CanDelete(
            object fileNode,
            object fileDesc,
            string fileName)
        {
            if (fileNode is HandleFileNode handleFileNode)
            {
                var result = _client.CanDelete(new CanDeleteRequest
                {
                    Handle = handleFileNode.Handle,
                });
                return result.Result;
            }
            else if (fileNode is VirtualFileNode)
            {
                return STATUS_SUCCESS;
            }
            else
            {
                return STATUS_NOT_FOUND;
            }
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
            if (fileNode is HandleFileNode handleFileNode)
            {
                var result = _client.GetSecurity(new GetSecurityRequest
                {
                    Handle = handleFileNode.Handle,
                });
                if (result.Success)
                {
                    securityDescriptor = result.SecurityDescriptor.ToByteArray();
                }
                return result.Result;
            }
            else if (fileNode is VirtualFileNode)
            {
                return STATUS_SUCCESS;
            }
            else
            {
                return STATUS_NOT_FOUND;
            }
        }

        public override int SetSecurity(
            object fileNode,
            object fileDesc,
            AccessControlSections sections,
            byte[] securityDescriptor)
        {
            if (fileNode is HandleFileNode handleFileNode)
            {
                var result = _client.SetSecurity(new SetSecurityRequest
                {
                    Handle = handleFileNode.Handle,
                    Sections = (int)sections,
                    SecurityDescriptor = ByteString.CopyFrom(securityDescriptor),
                });
                return result.Result;
            }
            else if (fileNode is VirtualFileNode)
            {
                return STATUS_SUCCESS;
            }
            else
            {
                return STATUS_NOT_FOUND;
            }
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
            if (fileNode is HandleFileNode handleFileNode)
            {
#if ASYNC
                ulong operationHint = _host!.GetOperationRequestHint();
#endif
                var request = new ReadDirectoryRequest
                {
                    Handle = handleFileNode.Handle,
                    HasPattern = pattern != null,
                    Pattern = pattern ?? string.Empty,
                    HasMarker = marker != null,
                    Marker = marker ?? string.Empty,
                };
                if (_additionalSubdirectoryEntries.ContainsKey(handleFileNode.FileName) &&
                    WindowsRfsUtil.IsRealPath(handleFileNode.FileName))
                {
                    foreach (var entry in _additionalSubdirectoryEntries[handleFileNode.FileName])
                    {
                        WindowsFileDesc.GetFileInfoFromFileSystemInfo(
                            new DirectoryInfo(Path.Combine(WindowsRfsUtil.RealPath(handleFileNode.FileName), entry)),
                            out var fileInfo);
                        request.AdditionalEntries.Add(new ReadDirectoryVirtualEntry
                        {
                            Name = entry,
                            IsDirectory = true,
                            CreationTime = fileInfo.CreationTime,
                            ChangeTime = fileInfo.ChangeTime,
                            LastAccessTime = fileInfo.LastAccessTime,
                            LastWriteTime = fileInfo.LastWriteTime,
                        });
                    }
                }
                if (_additionalJunctionEntries.ContainsKey(handleFileNode.FileName) &&
                    WindowsRfsUtil.IsRealPath(handleFileNode.FileName))
                {
                    foreach (var entry in _additionalJunctionEntries[handleFileNode.FileName].Keys)
                    {
                        WindowsFileDesc.GetFileInfoFromFileSystemInfo(
                            new DirectoryInfo(Path.Combine(WindowsRfsUtil.RealPath(handleFileNode.FileName), entry)),
                            out var fileInfo);
                        request.AdditionalEntries.Add(new ReadDirectoryVirtualEntry
                        {
                            Name = entry,
                            IsDirectory = true,
                            CreationTime = fileInfo.CreationTime,
                            ChangeTime = fileInfo.ChangeTime,
                            LastAccessTime = fileInfo.LastAccessTime,
                            LastWriteTime = fileInfo.LastWriteTime,
                        });
                    }
                }
#if ASYNC
                var stream = _client.ReadDirectory(request);
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
#else
                var result = _client.ReadDirectory(request);
                var iterationEndedDueToFullBuffer = false;
                bytesTransferred = 0;
                foreach (var entry in result.Entries)
                {
                    var dirInfo = default(DirInfo);
                    dirInfo.FileInfo = ConvertFileInfo(entry.FileInfo);
                    dirInfo.SetFileNameBuf(entry.FileName);
                    var localResult = Fsp.Interop.Api.FspFileSystemAddDirInfo(
                        ref dirInfo,
                        buffer,
                        length,
                        out bytesTransferred);
                    if (!localResult)
                    {
                        iterationEndedDueToFullBuffer = true;
                    }
                }
                if (!iterationEndedDueToFullBuffer)
                {
                    Api.FspFileSystemEndDirInfo(buffer, length, out bytesTransferred);
                }
                return result.Result;
#endif
            }
            else if (fileNode is VirtualFileNode virtualFileNode)
            {
                bytesTransferred = 0;

                if (pattern != null)
                    pattern = pattern.Replace('<', '*').Replace('>', '?').Replace('"', '.');
                else
                    pattern = "*";
                SortedList list = new SortedList();
                list.Add(".", "subdir");
                list.Add("..", "parent");
                foreach (var name in virtualFileNode.Subdirectories)
                {
                    list.Add(name, "subdir");
                }
                foreach (var name in virtualFileNode.Junctions)
                {
                    list.Add(name.Key, "junction");
                }
                var fsInfos = new DictionaryEntry[list.Count];
                list.CopyTo(fsInfos, 0);

                var index = 0;
                if (marker != null)
                {
                    index = Array.BinarySearch(
                        fsInfos,
                        new DictionaryEntry(marker, null),
                        _directoryEntryComparer);
                    if (0 <= index)
                        index++;
                    else
                        index = ~index;
                }

                bool iterationEndedDueToFullBuffer = false;
                while (fsInfos.Length > index)
                {
                    var fileName = (String)fsInfos[index].Key;
                    FspFileInfo fileInfo;
                    if (fsInfos[index].Value is string k)
                    {
                        if (k == "subdir")
                        {
                            fileInfo = WindowsRfsVirtual.GetVirtualDirectoryOnClient(
                                Path.Combine(virtualFileNode.DirectoryInfo.FullName, fileName));
                        }
                        else if (k == "junction")
                        {
                            fileInfo = WindowsRfsVirtual.GetVirtualJunctionOnClient(
                                Path.Combine(virtualFileNode.DirectoryInfo.FullName, fileName));
                        }
                        else if (k == "parent")
                        {
                            var parentResult = _client.GetFileInfo(new GetFileInfoRequest
                            {
                                FileName = Path.Combine(virtualFileNode.DirectoryInfo.FullName, fileName)
                            });
                            if (parentResult.Success)
                            {
                                // The parent is a normal directory on the remote.
                                fileInfo = ConvertFileInfo(parentResult.FileInfo);
                            }
                            else
                            {
                                // The parent is also virtualised.
                                fileInfo = WindowsRfsVirtual.GetVirtualDirectoryOnClient(virtualFileNode.DirectoryInfo.Parent!.FullName);
                            }
                        }
                        else
                        {
                            index = index + 1;
                            continue;
                        }
                    }
                    else
                    {
                        index = index + 1;
                        continue;
                    }

                    var dirInfo = default(DirInfo);
                    dirInfo.FileInfo = fileInfo;
                    dirInfo.SetFileNameBuf(fileName);
                    var result = Fsp.Interop.Api.FspFileSystemAddDirInfo(
                        ref dirInfo,
                        buffer,
                        length,
                        out bytesTransferred);
                    if (!result)
                    {
                        iterationEndedDueToFullBuffer = true;
                    }
                }

                if (!iterationEndedDueToFullBuffer)
                {
                    Fsp.Interop.Api.FspFileSystemEndDirInfo(
                        buffer,
                        length,
                        out bytesTransferred);
                }

                return STATUS_SUCCESS;
            }
            else
            {
                bytesTransferred = 0;
                return STATUS_ACCESS_DENIED;
            }
        }
    }
}
