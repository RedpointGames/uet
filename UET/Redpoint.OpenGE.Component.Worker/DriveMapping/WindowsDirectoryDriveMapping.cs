namespace Redpoint.OpenGE.Component.Worker.DriveMapping
{
    using System;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.Versioning;
    using Windows.Win32;
    using Windows.Win32.NetworkManagement.WNet;

    [SupportedOSPlatform("windows5.0")]
    internal class WindowsDirectoryDriveMapping : IDirectoryDriveMapping
    {
        private readonly ConcurrentDictionary<string, string> _driveMappings = new ConcurrentDictionary<string, string>();
        private readonly SemaphoreSlim _driveMappingSemaphore = new SemaphoreSlim(1);

        public string ShortenPath(string rootPath)
        {
            rootPath = rootPath.TrimEnd('\\');
            if (_driveMappings.TryGetValue(rootPath, out var mappedEarlyCheck))
            {
                return mappedEarlyCheck;
            }
            _driveMappingSemaphore.Wait();
            try
            {
                if (_driveMappings.TryGetValue(rootPath, out var mappedLateCheck))
                {
                    return mappedLateCheck;
                }

                unsafe
                {
                    var targetRemoteName = "\\\\"
                        + Environment.MachineName
                        + "\\"
                        + rootPath[0]
                        + "$"
                        + rootPath.Substring("C$".Length);

                    // Check if the target folder is already mapped.
                    WNetCloseEnumSafeHandle handle;
                    if (PInvoke.WNetOpenEnum(
                        NET_RESOURCE_SCOPE.RESOURCE_CONNECTED,
                        NET_RESOURCE_TYPE.RESOURCETYPE_DISK,
                        0,
                        null,
                        out handle) == 0)
                    {
                        using (handle)
                        {
                            while (true)
                            {
                                uint count = 1;
                                byte[] buffer = new byte[16 * 1024];
                                uint bufferSize = (uint)buffer.Length;
                                fixed (byte* bufferRaw = buffer)
                                {
                                    var result = PInvoke.WNetEnumResource(
                                        handle,
                                        ref count,
                                        bufferRaw,
                                        ref bufferSize);
                                    if (result == 259 /* ERROR_NO_MORE_ITEMS */ ||
                                        count == 0)
                                    {
                                        break;
                                    }
                                    else if (result != 0)
                                    {
                                        throw new Win32Exception(unchecked((int)result));
                                    }
                                    var resource = (NETRESOURCEW*)bufferRaw;

                                    if (string.Equals(
                                        resource->lpRemoteName.ToString(),
                                        targetRemoteName,
                                        StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        var shortenedPath = resource->lpLocalName.ToString().TrimEnd('\\') + '\\';
                                        _driveMappings.TryAdd(
                                            rootPath,
                                            shortenedPath);
                                        return shortenedPath;
                                    }
                                }
                            }
                        }
                    }

                    // It's not mapped, add a mapping.
                    var drives = DriveInfo.GetDrives()
                        .Select(x => x.Name.Substring(0, 1).ToUpperInvariant()[0])
                        .ToHashSet();
                    for (char l = 'G'; l < 'Z'; l++)
                    {
                        if (!drives.Contains(l))
                        {
                            // We've found an available drive letter.
                            var localName = $"{l}:";
                            fixed (char* localNamePtr = localName)
                            {
                                fixed (char* remoteNamePtr = targetRemoteName)
                                {
                                    var resource = new NETRESOURCEW
                                    {
                                        dwType = NET_RESOURCE_TYPE.RESOURCETYPE_DISK,
                                        lpLocalName = localNamePtr,
                                        lpRemoteName = remoteNamePtr,
                                    };
                                    var result = PInvoke.WNetAddConnection2W(
                                        resource,
                                        null,
                                        null,
                                        0);
                                    if (result != 0)
                                    {
                                        throw new Win32Exception(unchecked((int)result));
                                    }
                                    _driveMappings.TryAdd(
                                        rootPath,
                                        $"{localName}\\");
                                    return $"{localName}\\";
                                }
                            }
                        }
                    }

                    throw new InvalidOperationException($"No available drive letters to map path '{rootPath}'");
                }
            }
            finally
            {
                _driveMappingSemaphore.Release();
            }
        }
    }
}
