#pragma warning disable CA1849
#pragma warning disable CA1062 // Validate arguments of public methods

namespace Redpoint.Rfs.WinFsp
{
    using Fsp;
    using Fsp.Interop;
    using Google.Protobuf;
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Runtime.Versioning;
    using System.Security.AccessControl;
    using System.Threading.Tasks;
    using FspFileInfo = Fsp.Interop.FileInfo;

    [SupportedOSPlatform("windows6.2")]
    public class WindowsRfsHost : WindowsRfs.WindowsRfsBase
    {
        internal static readonly DateTimeOffset _rootCreationTime = DateTimeOffset.UtcNow;
        private readonly ConcurrentDictionary<long, WindowsFileDesc> _openHandles;
        private readonly ILogger _logger;
        private readonly byte[] _rootSecurityDescriptor;
        private long _nextHandle = 1000;
        private static DirectoryEntryComparer _directoryEntryComparer =
            new DirectoryEntryComparer();

        public WindowsRfsHost(ILogger logger)
        {
            _openHandles = new ConcurrentDictionary<long, WindowsFileDesc>();
            _logger = logger;
            var securityDescriptor = new RawSecurityDescriptor("O:WDG:WDD:PAI(A;;FA;;;WD)");
            _rootSecurityDescriptor = new byte[securityDescriptor.BinaryLength];
            securityDescriptor.GetBinaryForm(_rootSecurityDescriptor, 0);
        }

        private static FileAccess ConvertAccess(FileSystemRights rights)
        {
            var access = (FileAccess)0;
            if ((rights & FileSystemRights.Read) != 0)
            {
                access |= FileAccess.Read;
            }
            if ((rights & FileSystemRights.Write) != 0)
            {
                access |= FileAccess.Write;
            }
            return access;
        }

        private int GetResultForException(Exception ex)
        {
            var hresult = ex.HResult;
            if (0x80070000 == (hresult & 0xFFFF0000))
            {
                return FileSystemBase.NtStatusFromWin32((UInt32)hresult & 0xFFFF);
            }
            else
            {
                _logger.LogError(ex, ex.Message);
                return FileSystemBase.STATUS_UNEXPECTED_IO_ERROR;
            }
        }

        private static RfsFileInfo ConvertFileInfo(in FspFileInfo fileInfo)
        {
            return new RfsFileInfo
            {
                AllocationSize = fileInfo.AllocationSize,
                ChangeTime = fileInfo.ChangeTime,
                CreationTime = fileInfo.CreationTime,
                FileAttributes = fileInfo.FileAttributes,
                FileSize = fileInfo.FileSize,
                HardLinks = fileInfo.HardLinks,
                IndexNumber = fileInfo.IndexNumber,
                LastAccessTime = fileInfo.LastAccessTime,
                LastWriteTime = fileInfo.LastWriteTime,
                ReparseTag = fileInfo.ReparseTag,
            };
        }

        public override Task<GetRootCreationTimeResponse> GetRootCreationTime(
            GetRootCreationTimeRequest request,
            ServerCallContext context)
        {
            return Task.FromResult(new GetRootCreationTimeResponse
            {
                FileTime = unchecked((ulong)_rootCreationTime.ToFileTime()),
            });
        }

        public override Task<GetVolumeInfoResponse> GetVolumeInfo(
            GetVolumeInfoRequest request,
            ServerCallContext context)
        {
            var info = new DriveInfo("c");
            return Task.FromResult(new GetVolumeInfoResponse
            {
                TotalSize = unchecked((ulong)info.TotalSize),
                TotalFreeSpace = unchecked((ulong)info.TotalFreeSpace),
            });
        }

        public override Task<GetSecurityByNameResponse> GetSecurityByName(
            GetSecurityByNameRequest request,
            ServerCallContext context)
        {
            try
            {
                if (request.FileName.Length == 0)
                {
                    return Task.FromResult(new GetSecurityByNameResponse
                    {
                        Result = FileSystemBase.STATUS_NOT_FOUND,
                    });
                }
                else if (request.FileName == "\\")
                {
                    return Task.FromResult(new GetSecurityByNameResponse
                    {
                        Result = FileSystemBase.STATUS_SUCCESS,
                        SecurityDescriptor = request.IncludeSecurityDescriptor ? ByteString.CopyFrom(_rootSecurityDescriptor) : ByteString.Empty,
                        FileAttributes = (uint)FileAttributes.Directory,
                    });
                }
                else if (!WindowsRfsUtil.IsRealPath(request.FileName))
                {
                    return Task.FromResult(new GetSecurityByNameResponse
                    {
                        Result = FileSystemBase.STATUS_NOT_FOUND,
                    });
                }
                else if (request.FileName.Length == 2)
                {
                    var fileName = WindowsRfsUtil.RealPath(request.FileName);
                    var info = new System.IO.FileInfo(fileName);
                    var result = new GetSecurityByNameResponse
                    {
                        FileAttributes = (uint)info.Attributes,
                        Result = FileSystemBase.STATUS_SUCCESS,
                    };
                    if (request.IncludeSecurityDescriptor)
                    {
                        result.SecurityDescriptor = ByteString.CopyFrom(_rootSecurityDescriptor);
                    }
                    return Task.FromResult(result);
                }
                else
                {
                    var fileName = WindowsRfsUtil.RealPath(request.FileName);
                    var info = new System.IO.FileInfo(fileName);
                    var result = new GetSecurityByNameResponse
                    {
                        FileAttributes = (uint)info.Attributes,
                        Result = FileSystemBase.STATUS_SUCCESS,
                    };
                    if (request.IncludeSecurityDescriptor)
                    {
                        result.SecurityDescriptor = ByteString.CopyFrom(info.GetAccessControl().GetSecurityDescriptorBinaryForm());
                    }
                    return Task.FromResult(result);
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(new GetSecurityByNameResponse
                {
                    Result = GetResultForException(ex),
                });
            }
        }

        public override Task<CreateResponse> Create(
            CreateRequest request,
            ServerCallContext context)
        {
            var handle = Interlocked.Increment(ref _nextHandle);
            try
            {
                WindowsFileDesc? fileDesc = null;
                try
                {
                    var fileName = WindowsRfsUtil.RealPath(request.FileName);
                    if (0 == (request.CreateOptions & FileSystemBase.FILE_DIRECTORY_FILE))
                    {
                        FileSecurity? security = null;
                        if (request.HasSecurityDescriptor)
                        {
                            security = new FileSecurity();
                            security.SetSecurityDescriptorBinaryForm(request.SecurityDescriptor.ToByteArray());
                        }
                        var fileStream = new FileStream(
                            fileName,
                            FileMode.CreateNew,
                            ConvertAccess((FileSystemRights)request.GrantedAccess | FileSystemRights.WriteAttributes),
                            FileShare.Read | FileShare.Write | FileShare.Delete,
                            4096,
                            0);
                        if (security != null)
                        {
                            fileStream.SetAccessControl(security);
                        }
                        fileDesc = new WindowsFileDesc(fileStream);
                        fileDesc.SetFileAttributes(request.FileAttributes | (UInt32)System.IO.FileAttributes.Archive);
                    }
                    else
                    {
                        if (Directory.Exists(fileName))
                        {
                            return Task.FromResult(new CreateResponse
                            {
                                Success = false,
                                Result = FileSystemBase.STATUS_OBJECT_NAME_COLLISION,
                            });
                        }
                        DirectorySecurity? security = null;
                        if (request.HasSecurityDescriptor)
                        {
                            security = new DirectorySecurity();
                            security.SetSecurityDescriptorBinaryForm(request.SecurityDescriptor.ToByteArray());
                        }
                        var directoryInfo = Directory.CreateDirectory(fileName);
                        if (security != null)
                        {
                            directoryInfo.SetAccessControl(security);
                        }
                        fileDesc = new WindowsFileDesc(directoryInfo);
                        fileDesc.SetFileAttributes(request.FileAttributes);
                    }

                    FspFileInfo fileInfo;
                    var result = fileDesc.GetFileInfo(out fileInfo);
                    if (result == FileSystemBase.STATUS_SUCCESS)
                    {
                        _openHandles[handle] = fileDesc;
                        return Task.FromResult(new CreateResponse
                        {
                            Success = true,
                            Handle = handle,
                            FileInfo = ConvertFileInfo(fileInfo),
                            HasNormalizedName = false,
                            NormalizedName = string.Empty,
                            Result = result,
                        });
                    }
                    else
                    {
                        return Task.FromResult(new CreateResponse
                        {
                            Success = false,
                            Result = result,
                        });
                    }
                }
                catch
                {
                    if (null != fileDesc && null != fileDesc.Stream)
                        fileDesc.Stream.Dispose();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _openHandles.Remove(handle, out _);
                return Task.FromResult(new CreateResponse
                {
                    Success = false,
                    Result = GetResultForException(ex),
                });
            }
        }

        public override Task<OpenResponse> Open(
            OpenRequest request,
            ServerCallContext context)
        {
            var handle = Interlocked.Increment(ref _nextHandle);
            try
            {
                WindowsFileDesc? fileDesc = null;
                try
                {
                    if (request.FileName == "\\")
                    {
                        fileDesc = new WindowsFileDesc(DriveInfo.GetDrives());
                    }
                    else if (request.FileName.Length == 2)
                    {
                        fileDesc = new WindowsFileDesc(new DirectoryInfo($"{request.FileName[1]}:\\"));
                    }
                    else
                    {
                        var fileName = WindowsRfsUtil.RealPath(request.FileName);
                        if (!Directory.Exists(fileName))
                        {
                            fileDesc = new WindowsFileDesc(
                                new FileStream(
                                    fileName,
                                    FileMode.Open,
                                    ConvertAccess((FileSystemRights)request.GrantedAccess),
                                    FileShare.Read | FileShare.Write | FileShare.Delete,
                                    4096,
                                    0));
                        }
                        else
                        {
                            fileDesc = new WindowsFileDesc(
                                new DirectoryInfo(fileName));
                        }
                    }

                    FspFileInfo fileInfo;
                    var result = fileDesc.GetFileInfo(out fileInfo);
                    if (result == FileSystemBase.STATUS_SUCCESS)
                    {
                        _openHandles[handle] = fileDesc;
                        return Task.FromResult(new OpenResponse
                        {
                            Success = true,
                            Handle = handle,
                            FileInfo = ConvertFileInfo(fileInfo),
                            HasNormalizedName = false,
                            NormalizedName = string.Empty,
                            Result = result,
                        });
                    }
                    else
                    {
                        return Task.FromResult(new OpenResponse
                        {
                            Success = false,
                            Result = result,
                        });
                    }
                }
                catch
                {
                    if (null != fileDesc && null != fileDesc.Stream)
                        fileDesc.Stream.Dispose();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _openHandles.Remove(handle, out _);
                return Task.FromResult(new OpenResponse
                {
                    Success = false,
                    Result = GetResultForException(ex),
                });
            }
        }

        public override Task<OverwriteResponse> Overwrite(
            OverwriteRequest request,
            ServerCallContext context)
        {
            try
            {
                WindowsFileDesc fileDesc = _openHandles[request.Handle];
                if (request.ReplaceFileAttributes)
                {
                    fileDesc.SetFileAttributes(request.FileAttributes |
                        (UInt32)System.IO.FileAttributes.Archive);
                }
                else if (0 != request.FileAttributes)
                {
                    fileDesc.SetFileAttributes(fileDesc.GetFileAttributes() | request.FileAttributes |
                        (UInt32)System.IO.FileAttributes.Archive);
                }
                fileDesc.Stream!.SetLength(0);

                FspFileInfo fileInfo;
                var result = fileDesc.GetFileInfo(out fileInfo);

                if (result == FileSystemBase.STATUS_SUCCESS)
                {
                    return Task.FromResult(new OverwriteResponse
                    {
                        Success = true,
                        FileInfo = ConvertFileInfo(fileInfo),
                        Result = result,
                    });
                }
                else
                {
                    return Task.FromResult(new OverwriteResponse
                    {
                        Success = false,
                        Result = result,
                    });
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(new OverwriteResponse
                {
                    Success = false,
                    Result = GetResultForException(ex),
                });
            }
        }

        public override Task<CleanupResponse> Cleanup(
            CleanupRequest request,
            ServerCallContext context)
        {
            try
            {
                WindowsFileDesc fileDesc = _openHandles[request.Handle];
                if (0 != (request.Flags & FileSystemBase.CleanupDelete))
                {
                    fileDesc.SetDisposition(true);
                    if (null != fileDesc.Stream)
                    {
                        fileDesc.Stream.Dispose();
                    }
                }
                return Task.FromResult(new CleanupResponse());
            }
            catch (Exception)
            {
                return Task.FromResult(new CleanupResponse());
            }
        }

        public override Task<CloseResponse> Close(CloseRequest request, ServerCallContext context)
        {
            try
            {
                WindowsFileDesc fileDesc = _openHandles[request.Handle];
                if (null != fileDesc.Stream)
                {
                    fileDesc.Stream.Dispose();
                }
                _openHandles.Remove(request.Handle, out _);
                return Task.FromResult(new CloseResponse());
            }
            catch (Exception)
            {
                return Task.FromResult(new CloseResponse());
            }
        }

        public override Task<ReadResponse> Read(ReadRequest request, ServerCallContext context)
        {
            try
            {
                WindowsFileDesc fileDesc = _openHandles[request.Handle];
                if (request.Offset >= (UInt64)fileDesc.Stream!.Length)
                {
                    return Task.FromResult(new ReadResponse
                    {
                        Success = false,
                        Result = FileSystemBase.STATUS_END_OF_FILE,
                    });
                }
                Byte[] Bytes = new byte[request.Length];
                fileDesc.Stream.Seek((Int64)request.Offset, SeekOrigin.Begin);
                var PBytesTransferred = (UInt32)fileDesc.Stream.Read(Bytes, 0, Bytes.Length);
                return Task.FromResult(new ReadResponse
                {
                    Success = true,
                    Buffer = ByteString.CopyFrom(Bytes, 0, (int)PBytesTransferred),
                    BytesTransferred = PBytesTransferred,
                    Result = FileSystemBase.STATUS_SUCCESS,
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ReadResponse
                {
                    Success = false,
                    Result = GetResultForException(ex),
                });
            }
        }

        public override Task<WriteResponse> Write(WriteRequest request, ServerCallContext context)
        {
            try
            {
                WindowsFileDesc FileDesc = _openHandles[request.Handle];
                var length = request.Length;
                if (request.ConstrainedIo)
                {
                    if (request.Offset >= (UInt64)FileDesc.Stream!.Length)
                    {
                        return Task.FromResult(new WriteResponse
                        {
                            Success = true,
                            BytesTransferred = 0,
                            Result = FileSystemBase.STATUS_SUCCESS,
                        });
                    }
                    if (request.Offset + request.Length > (UInt64)FileDesc.Stream.Length)
                    {
                        length = (UInt32)((UInt64)FileDesc.Stream.Length - request.Offset);
                    }
                }
                Byte[] Bytes = new byte[length];
                request.Buffer.Memory.CopyTo(new Memory<byte>(Bytes, 0, Bytes.Length));
                if (!request.WriteToEndOfFile)
                {
                    FileDesc.Stream!.Seek((Int64)request.Offset, SeekOrigin.Begin);
                }
                FileDesc.Stream!.Write(Bytes, 0, Bytes.Length);
                var bytesTransferred = (UInt32)Bytes.Length;
                var result = FileDesc.GetFileInfo(out var fileInfo);
                if (result == FileSystemBase.STATUS_SUCCESS)
                {
                    return Task.FromResult(new WriteResponse
                    {
                        Success = true,
                        FileInfo = ConvertFileInfo(fileInfo),
                        Result = result,
                    });
                }
                else
                {
                    return Task.FromResult(new WriteResponse
                    {
                        Success = false,
                        Result = result,
                    });
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(new WriteResponse
                {
                    Success = false,
                    Result = GetResultForException(ex),
                });
            }
        }

        public override Task<FlushResponse> Flush(FlushRequest request, ServerCallContext context)
        {
            try
            {
                WindowsFileDesc fileDesc = _openHandles[request.Handle];
                fileDesc.Stream!.Flush(true);
                var result = fileDesc.GetFileInfo(out var fileInfo);
                if (result == FileSystemBase.STATUS_SUCCESS)
                {
                    return Task.FromResult(new FlushResponse
                    {
                        Success = true,
                        FileInfo = ConvertFileInfo(fileInfo),
                        Result = result,
                    });
                }
                else
                {
                    return Task.FromResult(new FlushResponse
                    {
                        Success = false,
                        Result = result,
                    });
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(new FlushResponse
                {
                    Success = false,
                    Result = GetResultForException(ex),
                });
            }
        }

        public override Task<GetFileInfoResponse> GetFileInfo(GetFileInfoRequest request, ServerCallContext context)
        {
            try
            {
                if (request.TypeCase == GetFileInfoRequest.TypeOneofCase.Handle)
                {
                    WindowsFileDesc fileDesc = _openHandles[request.Handle];
                    var result = fileDesc.GetFileInfo(out var fileInfo);
                    if (result == FileSystemBase.STATUS_SUCCESS)
                    {
                        return Task.FromResult(new GetFileInfoResponse
                        {
                            Success = true,
                            FileInfo = ConvertFileInfo(fileInfo),
                            Result = result,
                        });
                    }
                    else
                    {
                        return Task.FromResult(new GetFileInfoResponse
                        {
                            Success = false,
                            Result = result,
                        });
                    }
                }
                else
                {
                    var realName = WindowsRfsUtil.RealPath(request.FileName);
                    FspFileInfo fileInfo;
                    if (Directory.Exists(realName))
                    {
                        WindowsFileDesc.GetFileInfoFromFileSystemInfo(new DirectoryInfo(realName), out fileInfo);
                        return Task.FromResult(new GetFileInfoResponse
                        {
                            Success = true,
                            FileInfo = ConvertFileInfo(fileInfo),
                            Result = FileSystemBase.STATUS_SUCCESS,
                        });
                    }
                    else if (File.Exists(realName))
                    {
                        WindowsFileDesc.GetFileInfoFromFileSystemInfo(new System.IO.FileInfo(realName), out fileInfo);
                        return Task.FromResult(new GetFileInfoResponse
                        {
                            Success = true,
                            FileInfo = ConvertFileInfo(fileInfo),
                            Result = FileSystemBase.STATUS_SUCCESS,
                        });
                    }
                    else
                    {
                        return Task.FromResult(new GetFileInfoResponse
                        {
                            Success = false,
                            Result = FileSystemBase.STATUS_NOT_FOUND,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(new GetFileInfoResponse
                {
                    Success = false,
                    Result = GetResultForException(ex),
                });
            }
        }

        public override Task<SetBasicInfoResponse> SetBasicInfo(SetBasicInfoRequest request, ServerCallContext context)
        {
            try
            {
                WindowsFileDesc fileDesc = _openHandles[request.Handle];
                fileDesc.SetBasicInfo(
                    request.FileAttributes,
                    request.CreationTime,
                    request.LastAccessTime,
                    request.LastWriteTime);
                var result = fileDesc.GetFileInfo(out var fileInfo);
                if (result == FileSystemBase.STATUS_SUCCESS)
                {
                    return Task.FromResult(new SetBasicInfoResponse
                    {
                        Success = true,
                        FileInfo = ConvertFileInfo(fileInfo),
                        Result = result,
                    });
                }
                else
                {
                    return Task.FromResult(new SetBasicInfoResponse
                    {
                        Success = false,
                        Result = result,
                    });
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(new SetBasicInfoResponse
                {
                    Success = false,
                    Result = GetResultForException(ex),
                });
            }
        }

        public override Task<SetFileSizeResponse> SetFileSize(SetFileSizeRequest request, ServerCallContext context)
        {
            try
            {
                WindowsFileDesc fileDesc = _openHandles[request.Handle];
                if (!request.SetAllocationSize || (UInt64)fileDesc.Stream!.Length > request.NewSize)
                {
                    fileDesc.Stream!.SetLength((Int64)request.NewSize);
                }
                var result = fileDesc.GetFileInfo(out var fileInfo);
                if (result == FileSystemBase.STATUS_SUCCESS)
                {
                    return Task.FromResult(new SetFileSizeResponse
                    {
                        Success = true,
                        FileInfo = ConvertFileInfo(fileInfo),
                        Result = result,
                    });
                }
                else
                {
                    return Task.FromResult(new SetFileSizeResponse
                    {
                        Success = false,
                        Result = result,
                    });
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(new SetFileSizeResponse
                {
                    Success = false,
                    Result = GetResultForException(ex),
                });
            }
        }

        public override Task<CanDeleteResponse> CanDelete(CanDeleteRequest request, ServerCallContext context)
        {
            try
            {
                WindowsFileDesc fileDesc = _openHandles[request.Handle];
                fileDesc.SetDisposition(false);
                return Task.FromResult(new CanDeleteResponse
                {
                    Result = FileSystemBase.STATUS_SUCCESS,
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new CanDeleteResponse
                {
                    Result = GetResultForException(ex),
                });
            }
        }

        public override Task<RenameResponse> Rename(RenameRequest request, ServerCallContext context)
        {
            try
            {
                var fileName = WindowsRfsUtil.RealPath(request.FileName);
                var newFileName = WindowsRfsUtil.RealPath(request.NewFileName);
                WindowsFileDesc.Rename(fileName, newFileName, request.ReplaceIfExists);
                return Task.FromResult(new RenameResponse
                {
                    Result = FileSystemBase.STATUS_SUCCESS,
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new RenameResponse
                {
                    Result = GetResultForException(ex),
                });
            }
        }

        public override Task<GetSecurityResponse> GetSecurity(GetSecurityRequest request, ServerCallContext context)
        {
            try
            {
                WindowsFileDesc fileDesc = _openHandles[request.Handle];
                return Task.FromResult(new GetSecurityResponse
                {
                    Success = true,
                    SecurityDescriptor = ByteString.CopyFrom(fileDesc.GetSecurityDescriptor()),
                    Result = FileSystemBase.STATUS_SUCCESS,
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new GetSecurityResponse
                {
                    Success = false,
                    Result = GetResultForException(ex),
                });
            }
        }

        public override Task<SetSecurityResponse> SetSecurity(SetSecurityRequest request, ServerCallContext context)
        {
            try
            {
                WindowsFileDesc fileDesc = _openHandles[request.Handle];
                fileDesc.SetSecurityDescriptor(
                    (AccessControlSections)request.Sections,
                    request.SecurityDescriptor.ToByteArray());
                return Task.FromResult(new SetSecurityResponse
                {
                    Result = FileSystemBase.STATUS_SUCCESS,
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new SetSecurityResponse
                {
                    Result = GetResultForException(ex),
                });
            }
        }

#if ASYNC
        public async override Task ReadDirectory(
            ReadDirectoryRequest request,
            IServerStreamWriter<ReadDirectoryResponse> responseStream,
            ServerCallContext context)
        {
#else
        public override Task<ReadDirectoryResponse> ReadDirectory(ReadDirectoryRequest request, ServerCallContext context)
        {
            var result = new ReadDirectoryResponse();
#endif
            try
            {
                WindowsFileDesc fileDesc = _openHandles[request.Handle];
                bool hasStarted = false;
                int index = 0;
                while (true)
                {
                    if (null == fileDesc.FileSystemInfos)
                    {
                        string pattern;
                        if (request.HasPattern)
                            pattern = request.Pattern.Replace('<', '*').Replace('>', '?').Replace('"', '.');
                        else
                            pattern = "*";
                        if (fileDesc.Drives == null)
                        {
                            var @enum = fileDesc.DirInfo!.EnumerateFileSystemInfos(pattern);
                            SortedList list = new SortedList();
                            if (null != fileDesc.DirInfo && null != fileDesc.DirInfo.Parent)
                            {
                                list.Add(".", fileDesc.DirInfo);
                                list.Add("..", fileDesc.DirInfo.Parent);
                            }
                            foreach (FileSystemInfo Info in @enum)
                            {
                                list.Add(Info.Name, Info);
                            }
                            fileDesc.FileSystemInfos = new DictionaryEntry[list.Count];
                            list.CopyTo(fileDesc.FileSystemInfos, 0);
                        }
                        else
                        {
                            SortedList list = new SortedList();
                            foreach (var drive in fileDesc.Drives)
                            {
                                list.Add(drive.Name[0].ToString(), drive);
                            }
                            fileDesc.FileSystemInfos = new DictionaryEntry[list.Count];
                            list.CopyTo(fileDesc.FileSystemInfos, 0);
                        }
                    }
                    var fsInfos = fileDesc.FileSystemInfos;
                    if (request.AdditionalEntries.Count > 0)
                    {
                        // If we have dynamic additions, we can't use the cached filesystem infos.
                        var list = new SortedList();
                        foreach (var fsInfo in fsInfos)
                        {
                            list.Add(fsInfo.Key, fsInfo.Value);
                        }
                        foreach (var entry in request.AdditionalEntries)
                        {
                            if (!list.ContainsKey(entry.Name) || !entry.IsDirectory)
                            {
                                list[entry.Name] = entry;
                            }
                        }
                        fsInfos = new DictionaryEntry[list.Count];
                        list.CopyTo(fsInfos, 0);
                    }
                    if (!hasStarted)
                    {
                        index = 0;
                        hasStarted = true;
                        if (request.HasMarker)
                        {
                            index = Array.BinarySearch(
                                fsInfos,
                                new DictionaryEntry(request.Marker, null),
                                _directoryEntryComparer);
                            if (0 <= index)
                                index++;
                            else
                                index = ~index;
                        }
                    }
                    if (fsInfos.Length > index)
                    {
                        var fileName = (String)fsInfos[index].Key;
                        FspFileInfo fileInfo;
                        if (fsInfos[index].Value is FileSystemInfo fsi)
                        {
                            WindowsFileDesc.GetFileInfoFromFileSystemInfo(
                                fsi,
                                out fileInfo);
                        }
                        else if (fsInfos[index].Value is ReadDirectoryVirtualEntry v)
                        {
                            if (v.IsDirectory)
                            {
                                fileInfo = WindowsRfsVirtual.GetVirtualDirectoryOnHost(v.CreationTime, v.ChangeTime, v.LastAccessTime, v.LastWriteTime);
                            }
                            else
                            {
                                fileInfo = WindowsRfsVirtual.GetVirtualJunctionOnHost(v.CreationTime, v.ChangeTime, v.LastAccessTime, v.LastWriteTime);
                            }
                        }
                        else
                        {
                            fileInfo = WindowsRfsVirtual.GetVirtualDirectoryOnHost(
                                (ulong)_rootCreationTime.ToFileTime(),
                                (ulong)_rootCreationTime.ToFileTime(),
                                (ulong)_rootCreationTime.ToFileTime(),
                                (ulong)_rootCreationTime.ToFileTime());
                        }
#if ASYNC
                        await responseStream.WriteAsync(new ReadDirectoryResponse
                        {
                            Entry = new ReadDirectoryEntryResponse
                            {
                                FileName = fileName,
                                FileInfo = ConvertFileInfo(fileInfo),
                            }
                        });
#else
                        result.Entries.Add(new ReadDirectoryEntryResponse
                        {
                            FileName = fileName,
                            FileInfo = ConvertFileInfo(fileInfo),
                        });
#endif
                        index = index + 1;
                    }
                    else
                    {
#if ASYNC
                        await responseStream.WriteAsync(new ReadDirectoryResponse
                        {
                            Result = FileSystemBase.STATUS_SUCCESS,
                        });
                        break;
#else
                        result.Result = FileSystemBase.STATUS_SUCCESS;
                        return Task.FromResult(result);
#endif
                    }
                }
            }
            catch (Exception ex)
            {
#if ASYNC
                await responseStream.WriteAsync(new ReadDirectoryResponse
                {
                    Result = GetResultForException(ex),
                });
#else
                result.Result = GetResultForException(ex);
                return Task.FromResult(result);
#endif
            }
        }
    }
}
