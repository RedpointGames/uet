namespace Redpoint.Windows.VolumeManagement
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using global::Windows.Win32;
    using global::Windows.Win32.Foundation;

    /// <summary>
    /// Instantiate this class to enumerate over the current system volumes.
    /// </summary>
    [SupportedOSPlatform("windows6.2")]
    public sealed class SystemVolumes : IEnumerable<SystemVolume>
    {
        private sealed class SystemVolumeEnumerator : IEnumerator<SystemVolume>
        {
            private char[] _buffer = new char[PInvoke.MAX_PATH];
            private SystemVolume? _current;
            private global::Windows.Win32.Storage.FileSystem.FindVolumeHandle? _iterationHandle;

            public SystemVolume Current
            {
                get
                {
                    if (_current == null)
                    {
                        throw new IndexOutOfRangeException();
                    }
                    else
                    {
                        return _current;
                    }
                }
            }

            object IEnumerator.Current => Current;

            private string GetStringFromBuffer()
            {
                int end;
                for (end = 0; end < _buffer.Length; end++)
                {
                    if (_buffer[end] == '\0')
                    {
                        return new string(_buffer, 0, end);
                    }
                }
                throw new InvalidOperationException("Missing null terminator in buffer!");
            }

            private SystemVolume ProcessCurrentVolume()
            {
                unsafe
                {
                    fixed (char* buffer = _buffer)
                    {
                        var volumeName = GetStringFromBuffer();

                        var volumeIdRaw = volumeName[4..].TrimEnd('\\');
                        if (PInvoke.QueryDosDevice(volumeIdRaw, buffer, (uint)_buffer.Length) == 0x0)
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }
                        var deviceName = GetStringFromBuffer();

                        var volumePathNames = new List<string>();
                        PInvoke.GetVolumePathNamesForVolumeName(volumeName, null, 0, out uint returnLength);
                        if (returnLength != 0)
                        {
                            char* pathsBuffer = (char*)Marshal.AllocHGlobal((int)returnLength * sizeof(char));
                            try
                            {
                                var pathsPtr = new PZZWSTR(pathsBuffer);
                                if (PInvoke.GetVolumePathNamesForVolumeName(volumeName, pathsPtr, returnLength, out returnLength) == 0x0)
                                {
                                    throw new Win32Exception(Marshal.GetLastWin32Error());
                                }

                                var span = pathsPtr.AsSpan();
                                var lastIndex = 0;
                                for (int i = 0; i < span.Length; i++)
                                {
                                    if (span[i] == 0)
                                    {
                                        volumePathNames.Add(new string(span[lastIndex..i]));
                                        lastIndex = i + 1;
                                    }
                                }
                                if (lastIndex != span.Length)
                                {
                                    volumePathNames.Add(new string(span[lastIndex..(int)span.Length]));
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal((nint)pathsBuffer);
                            }
                        }

                        return new SystemVolume(volumeName, deviceName, volumePathNames.ToArray());
                    }
                }
            }

            public bool MoveNext()
            {
                unsafe
                {
                    fixed (char* buffer = _buffer)
                    {
                        if (_current == null || _iterationHandle == null)
                        {
                            var iterationHandle = PInvoke.FindFirstVolume(buffer, (uint)_buffer.Length);
                            if (iterationHandle.IsNull)
                            {
                                // We don't have any elements.
                                return false;
                            }

                            _current = ProcessCurrentVolume();
                            _iterationHandle = iterationHandle;
                            return true;
                        }
                        else
                        {
                            var hasNext = PInvoke.FindNextVolume(_iterationHandle.Value, buffer, (uint)_buffer.Length);
                            if (!hasNext)
                            {
                                // We are done. Close iteration.
                                PInvoke.FindVolumeClose(_iterationHandle.Value);
                                _current = null;
                                _iterationHandle = null;
                                return false;
                            }

                            _current = ProcessCurrentVolume();
                            return true;
                        }
                    }
                }
            }

            public void Dispose()
            {
                if (_iterationHandle != null)
                {
                    PInvoke.FindVolumeClose(_iterationHandle.Value);
                    _iterationHandle = null;
                }
                if (_current != null)
                {
                    _current = null;
                }
            }

            public void Reset()
            {
                if (_iterationHandle != null)
                {
                    PInvoke.FindVolumeClose(_iterationHandle.Value);
                    _iterationHandle = null;
                }
                if (_current != null)
                {
                    _current = null;
                }
            }
        }

        /// <summary>
        /// Returns an enumerator over the system volumes.
        /// </summary>
        /// <returns>The enumerator over the system volumes.</returns>
        public IEnumerator<SystemVolume> GetEnumerator()
        {
            return new SystemVolumeEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new SystemVolumeEnumerator();
        }
    }
}