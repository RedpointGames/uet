#pragma warning disable APL0002
#pragma warning disable CA2213

namespace Redpoint.Vfs.Driver.FsKit
{
    using FSKit;
    using Microsoft.Extensions.Logging;
    using Redpoint.Vfs.Abstractions;

    public sealed class FsKitVolume : FSVolume, IFSVolumePathConfOperations, IFSVolumeOperations, IFSVolumeOpenCloseOperations, IFSVolumeXattrOperations, IFSVolumeReadWriteOperations
    {
        private readonly ILogger _logger;
        private readonly IVfsLayer _projectionLayer;
        private readonly FSResource _resource;
        private readonly string _filesystemType;
        private readonly Router _router;

        private readonly FsKitFileNode _root = FsKitFileNode.AsExistingDirectoryWithPath("/", new VfsEntry
        {
            Name = string.Empty,
            Attributes = FileAttributes.Directory,
            CreationTime = DateTimeOffset.UtcNow,
            LastAccessTime = DateTimeOffset.UtcNow,
            LastWriteTime = DateTimeOffset.UtcNow,
            ChangeTime = DateTimeOffset.UtcNow,
            Size = 0,
        });

        private const int _allocationUnit = 4096;

        public FsKitVolume(
            ILogger logger,
            IVfsLayer projectionLayer,
            FSResource resource,
            string filesystemType,
            string volumeName,
            string volumeGuid) : base(
            volumeId: new FSVolumeIdentifier { Uuid = Constants.VolumeIdentifier },
            volumeName: new FSFileName(volumeName))
        {
            _logger = logger;
            _projectionLayer = projectionLayer;
            _resource = resource;
            _filesystemType = filesystemType;
            _router = new Router(logger);
        }

        nint IFSVolumePathConfOperations.MaximumLinkCount => -1;

        nint IFSVolumePathConfOperations.MaximumNameLength => -1;

        bool IFSVolumePathConfOperations.RestrictsOwnershipChanges => false;

        bool IFSVolumePathConfOperations.TruncatesLongNames => false;

        nint IFSVolumePathConfOperations.MaximumXattrSize => nint.MaxValue;

        nint IFSVolumePathConfOperations.MaximumXattrSizeInBits => nint.MaxValue;

        ulong IFSVolumePathConfOperations.MaximumFileSize => ulong.MaxValue;

        nint IFSVolumePathConfOperations.MaximumFileSizeInBits => nint.MaxValue;

        bool IFSVolumeOperations.EnableOpenUnlinkEmulation { get; set; }

        FSVolumeSupportedCapabilities IFSVolumeOperations.SupportedVolumeCapabilities => new FSVolumeSupportedCapabilities
        {
            SupportsHardLinks = false,
            SupportsSymbolicLinks = false,
            SupportsPersistentObjectIds = false,
            DoesNotSupportVolumeSizes = true,
            SupportsHiddenFiles = true,
            Supports64BitObjectIds = true,
            CaseFormat = FSVolumeCaseFormat.InsensitiveCasePreserving,
        };

        FSStatFSResult IFSVolumeOperations.VolumeStatistics => new FSStatFSResult(_filesystemType)
        {
            BlockSize = _allocationUnit,
            IoSize = 1024000,
            TotalBlocks = 1024000,
            AvailableBlocks = 1024000,
            FreeBlocks = 1024000,
            TotalFiles = 1024000,
            FreeFiles = 1024000,
        };

        internal static ulong GetAllocationSize(ulong fileSize)
        {
            return (fileSize + _allocationUnit - 1) / _allocationUnit * _allocationUnit;
        }

        void IFSVolumeOperations.Activate(FSTaskOptions options, FSVolumeOperationsActivateHandler reply)
        {
            _router.Call<FSItem?, NSError?>(reply.Invoke, () =>
            {
                return (_root, null);
            });
        }

        void IFSVolumeOperations.Deactivate(FSDeactivateOptions options, FSVolumeOperationsDeactivateHandler reply)
        {
            reply(null);
        }

        void IFSVolumeOperations.Mount(FSTaskOptions options, FSVolumeOperationsMountHandler reply)
        {
            reply(null);
        }

        void IFSVolumeOperations.Unmount(Action reply)
        {
            reply();
        }

        void IFSVolumeOperations.Synchronize(FSSyncFlags flags, FSVolumeOperationsSynchronizeHandler reply)
        {
            reply(null);
        }

        void IFSVolumeOperations.GetAttributes(
            FSItemGetAttributesRequest desiredAttributes,
            FSItem item,
            FSVolumeOperationsAttributesHandler reply)
        {
            (FSItemAttributes? attributes, NSError? error) Impl()
            {
                if (item is FsKitFileNode redpointItem)
                {
                    return (redpointItem.Attributes, null);
                }
                else
                {
                    return (null, FSKitFunctions.GetErrorForPosixError((int)PosixError.EIO));
                }
            }

            _router.Call(reply.Invoke, Impl);
        }

        void IFSVolumeOperations.SetAttributes(
            FSItemSetAttributesRequest newAttributes,
            FSItem item,
            FSVolumeOperationsAttributesHandler reply)
        {
            (FSItemAttributes? attributes, NSError? error) Impl()
            {
                if (item is FsKitFileNode redpointItem)
                {
                    return (null, FSKitFunctions.GetErrorForPosixError((int)PosixError.EROFS));
                }
                else
                {
                    return (null, FSKitFunctions.GetErrorForPosixError((int)PosixError.EIO));
                }
            }

            _router.Call(reply.Invoke, Impl);
        }

        void IFSVolumeOperations.LookupItem(
            FSFileName name,
            FSItem directory,
            FSVolumeOperationsLookupItemHandler reply)
        {
            (FSItem? item, FSFileName? itemName, NSError? error) Impl()
            {
                if (directory is FsKitFileNode redpointItem)
                {
                    var childItem = _projectionLayer.GetInfo(Path.Combine(redpointItem.Path, name.String!));
                    if (childItem == null)
                    {
                        return (null, null, FSKitFunctions.GetErrorForPosixError((int)PosixError.ENOENT));
                    }
                    else
                    {
                        return (
                            childItem.IsDirectory
                                ? FsKitFileNode.AsExistingDirectoryWithPath(Path.Combine(redpointItem.Path, childItem.Name), childItem)
                                : FsKitFileNode.AsFileWithPath(Path.Combine(redpointItem.Path, childItem.Name), childItem),
                            new FSFileName(childItem.Name),
                            null);
                    }
                }
                else
                {
                    return (null, null, FSKitFunctions.GetErrorForPosixError((int)PosixError.ENOENT));
                }
            }

            _router.Call(reply.Invoke, Impl);
        }

        void IFSVolumeOperations.Reclaim(FSItem item, FSVolumeOperationsReclaimHandler reply)
        {
            reply(null);
        }

        void IFSVolumeOperations.ReadSymbolicLink(FSItem item, FSVolumeOperationsReadSymbolicLinkHandler reply)
        {
            reply(null, FSKitFunctions.GetErrorForPosixError((int)PosixError.EIO));
        }

        void IFSVolumeOperations.CreateItem(
            FSFileName name,
            FSItemType type,
            FSItem directory,
            FSItemSetAttributesRequest newAttributes,
            FSVolumeOperationsCreateItemHandler reply)
        {
            (FSItem? newItem, FSFileName? newItemName, NSError? error) Impl()
            {
                if (!(directory is FsKitFileNode redpointDirectory))
                {
                    return (null, null, FSKitFunctions.GetErrorForPosixError((int)PosixError.ENOENT));
                }

                var path = Path.Combine(redpointDirectory.Path, name.String!).TrimStart('/');

                // Is this read-only?
                if (_projectionLayer.ReadOnly)
                {
                    return (null, null, FSKitFunctions.GetErrorForPosixError((int)PosixError.EROFS));
                }

                // Does this object already exist, or does it's parent not exist?
                if (_projectionLayer.Exists(path) == VfsEntryExistence.FileExists)
                {
                    return (null, null, FSKitFunctions.GetErrorForPosixError((int)PosixError.EEXIST));
                }

                // @todo: Implement
                return (null, null, FSKitFunctions.GetErrorForPosixError((int)PosixError.EIO));
            }

            _router.Call(reply.Invoke, Impl);
        }

        void IFSVolumeOperations.CreateSymbolicLink(
            FSFileName name,
            FSItem directory,
            FSItemSetAttributesRequest newAttributes,
            FSFileName contents,
            FSVolumeOperationsCreateItemHandler reply)
        {
            reply(null, null, FSKitFunctions.GetErrorForPosixError((int)PosixError.EROFS));
        }

        void IFSVolumeOperations.CreateLink(
            FSItem item,
            FSFileName name,
            FSItem directory,
            FSVolumeOperationsCreateLinkHandler reply)
        {
            reply(null, FSKitFunctions.GetErrorForPosixError((int)PosixError.EROFS));
        }

        void IFSVolumeOperations.RemoveItem(
            FSItem item,
            FSFileName name,
            FSItem directory,
            FSVolumeOperationsRemoveItemHandler reply)
        {
            reply(null);
        }

        void IFSVolumeOperations.RenameItem(
            FSItem item,
            FSItem sourceDirectory,
            FSFileName sourceName,
            FSFileName destinationName,
            FSItem destinationDirectory,
            FSItem? overItem,
            FSVolumeOperationsRenameItemHandler reply)
        {
            reply(null, FSKitFunctions.GetErrorForPosixError((int)PosixError.EROFS));
        }

        void IFSVolumeOperations.EnumerateDirectory(
            FSItem directory,
            ulong startingAtCookie,
            ulong verifier,
            FSItemGetAttributesRequest? attributes,
            FSDirectoryEntryPacker packer,
            FSVolumeOperationsEnumerateDirectoryHandler reply)
        {
            (ulong currentVerifier, NSError? error) Impl()
            {
                if (!(directory is FsKitFileNode redpointItem))
                {
                    return (0, FSKitFunctions.GetErrorForPosixError((int)PosixError.ENOENT));
                }

                var enumerable = _projectionLayer.List(redpointItem.Path);
                if (enumerable == null)
                {
                    return (0, FSKitFunctions.GetErrorForPosixError((int)PosixError.ENOENT));
                }

                var sortedChildren = enumerable
                    .OrderBy(x => x.Name)
                    .ToList();
                for (int idx = startingAtCookie == FSConstants.FSDirectoryCookieInitial ? 0 : (int)startingAtCookie;
                    idx < sortedChildren.Count;
                    idx++)
                {
                    var child = sortedChildren[idx];
                    packer.PackEntry(
                        name: new FSFileName(child.Name),
                        itemType: child.IsDirectory ? FSItemType.Directory : FSItemType.File,
                        itemId: FsKitFileNode.GetFileId(Path.Combine(redpointItem.Path, child.Name)),
                        nextCookie: (ulong)(idx + 1),
                        attributes: FsKitFileNode.GetItemAttributes(redpointItem.Path, child));
                }

                return (0, null);
            }

            _router.Call(reply.Invoke, Impl);
        }

        void IFSVolumeOpenCloseOperations.OpenItem(
            FSItem item,
            FSVolumeOpenModes mode,
            FSVolumeOpenCloseOperationsHandler reply)
        {
            reply(null);
        }

        void IFSVolumeOpenCloseOperations.CloseItem(
            FSItem item,
            FSVolumeOpenModes mode,
            FSVolumeOpenCloseOperationsHandler reply)
        {
            reply(null);
        }

        void IFSVolumeXattrOperations.GetXattr(
            FSFileName name,
            FSItem item,
            FSVolumeXattrOperationsGetHandler reply)
        {
            reply(null, null);
        }

        void IFSVolumeXattrOperations.SetXattr(
            FSFileName name,
            NSData? value,
            FSItem item,
            FSSetXattrPolicy policy,
            FSVolumeXattrOperationsSetHandler reply)
        {
            reply(FSKitFunctions.GetErrorForPosixError((int)PosixError.EROFS));
        }

        void IFSVolumeXattrOperations.ListXattrs(
            FSItem item,
            FSVolumeXattrOperationsListHandler reply)
        {
            reply([], null);
        }

        void IFSVolumeReadWriteOperations.Read(
            FSItem item,
            long offset,
            nuint length,
            FSMutableFileDataBuffer buffer,
            FSVolumeReadWriteOperationsReadHandler reply)
        {
            reply(0, FSKitFunctions.GetErrorForPosixError((int)PosixError.EIO));
        }

        void IFSVolumeReadWriteOperations.Write(
            NSData contents,
            FSItem item,
            long offset,
            FSVolumeReadWriteOperationsWriteHandler reply)
        {
            reply(0, FSKitFunctions.GetErrorForPosixError((int)PosixError.EIO));
        }
    }
}
