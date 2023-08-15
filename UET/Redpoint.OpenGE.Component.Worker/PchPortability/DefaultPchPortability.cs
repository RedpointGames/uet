namespace Redpoint.OpenGE.Component.Worker.PchPortability
{
    using Google.Protobuf;
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Diagnostics;
    using System.IO.MemoryMappedFiles;
    using System.Text;
    using System.Threading.Tasks;
    using System.Numerics;

    /// <summary>
    /// The MSVC compiler generates PCH files with absolute paths in them. When we 
    /// bring the PCH to another computer or even across different tasks, the build 
    /// layout directory will change, which results in '#pragma once' not working
    /// properly because include paths appear to be different when processed.
    /// 
    /// We rely on the drive letter and reservation folder name being exactly the
    /// same length on all machines to:
    /// 
    /// - Search for all non-portable paths in a PCH when they're created 
    ///   and emitted from a worker.
    /// - Generate a list of replacement locations and send this down to the
    ///   dispatcher as metadata for the PCH blob.
    /// - Send the list of replacement locations to a worker when the PCH blob
    ///   is being used in a remote compilation.
    /// - Replace old paths with the current build layout directory on the
    ///   worker when the PCH is being used as an input, using the replacements
    ///   list to do it quickly and in parallel.
    /// 
    /// Long term this should go away when we have full Detours-based remoting
    /// (i.e. API call virtualisation), but for now we need PCHs to work across
    /// machines because they're essential for distributed Unreal Engine builds.
    /// </summary>
    internal class DefaultPchPortability : IPchPortability
    {
        private static readonly string _portablePchHeader = "OPENGE-PORTABLE-PCH-V1";
        private static readonly byte[] _portablePchHeaderBytes = Encoding.ASCII.GetBytes("OPENGE-PORTABLE-PCH-V1");
        private static readonly byte[] _vcPchHeaderBytes = Encoding.ASCII.GetBytes("VCPCH0");

        public async Task<PchFileReplacementLocations> ScanPchForReplacementLocationsAsync(
            string pchPath, 
            string buildLayoutPath,
            CancellationToken cancellationToken)
        {
            var bytesSearchLower = Encoding.ASCII.GetBytes(buildLayoutPath.ToLowerInvariant());
            var bytesSearchUpper = Encoding.ASCII.GetBytes(buildLayoutPath.ToUpperInvariant());

            var locations = new PchFileReplacementLocations
            {
                PortablePathPrefixLength = bytesSearchLower.Length,
            };

            using (var stream = new FileStream(
                pchPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                // Sanity check: Make sure this isn't already a portable PCH.
                if (stream.Length > _portablePchHeaderBytes.Length)
                {
                    stream.Seek(-_portablePchHeaderBytes.Length, SeekOrigin.End);
                    var portableHeaderBuffer = new byte[_portablePchHeaderBytes.Length];
                    await stream.ReadExactlyAsync(portableHeaderBuffer);
                    stream.Seek(0, SeekOrigin.Begin);
                    if (Encoding.ASCII.GetString(portableHeaderBuffer) == _portablePchHeader)
                    {
                        throw new ArgumentException("PCH is already a portable PCH.", nameof(pchPath));
                    }
                }

                var bufferA = new byte[128 * 1024];
                var bufferB = new byte[128 * 1024];
                long fileOffset = 0;
                int lastByteReadCount = 0;
                do
                {
                    int bytesReadA = await stream.ReadAsync(bufferA, cancellationToken);
                    int bytesReadB = await stream.ReadAsync(bufferB, cancellationToken);
                    lastByteReadCount = bytesReadA + bytesReadB;
                    var bytesMatching = 0;
                    var offsetOfStartOfMatch = 0;
                    int startingScan = 0;
                    scanFromPartialMatch:
                    for (int i = startingScan; i < bytesReadA + bytesReadB; i++)
                    {
                        var targetBuffer = i < bytesReadA ? bufferA : bufferB;
                        var offsetInBuffer = i < bytesReadA ? i : (i - bytesReadA);
                        if (targetBuffer[offsetInBuffer] == bytesSearchLower[bytesMatching] ||
                            targetBuffer[offsetInBuffer] == bytesSearchUpper[bytesMatching])
                        {
                            if (bytesMatching == 0)
                            {
                                offsetOfStartOfMatch = i;
                            }
                            bytesMatching++;
                            if (bytesMatching == bytesSearchLower.Length)
                            {
                                locations.ReplacementOffsets.Add(fileOffset + offsetOfStartOfMatch);
                                bytesMatching = 0;
                            }
                        }
                        else
                        {
                            bytesMatching = 0;
                            offsetOfStartOfMatch = 0;
                        }
                    }
                    if (bytesMatching >= 1 && 
                        lastByteReadCount == bufferA.Length + bufferB.Length)
                    {
                        // We've got a match that is over a read boundary. Flip the
                        // buffers, read one buffer's worth into the new buffer B
                        // and then continue the partial match scan from the offset
                        // in the new buffer A.
                        var tmp = bufferA;
                        bufferB = bufferA;
                        bufferA = tmp;
                        startingScan = offsetOfStartOfMatch - bytesReadA;
                        fileOffset += bytesReadA;
                        bytesReadA = bytesReadB;
                        bytesReadB = await stream.ReadAsync(bufferB, cancellationToken);
                        lastByteReadCount = bytesReadA + bytesReadB;
                        goto scanFromPartialMatch;
                    }
                    fileOffset += lastByteReadCount;
                } while (lastByteReadCount == bufferA.Length + bufferB.Length);
            }

            return locations;
        }

        public async Task ConvertPchToPortablePch(
            string pchPath,
            string buildLayoutPath,
            CancellationToken cancellationToken)
        {
            using (var stream = new FileStream(
                pchPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                var vcPchHeaderBytes = new byte[_vcPchHeaderBytes.Length];
                stream.ReadExactly(vcPchHeaderBytes);
                if (Encoding.ASCII.GetString(vcPchHeaderBytes) != Encoding.ASCII.GetString(_vcPchHeaderBytes))
                {
                    // This isn't a MSVC PCH file.
                    return;
                }
            }

            var replacements = await ScanPchForReplacementLocationsAsync(
                pchPath,
                buildLayoutPath,
                cancellationToken);

            if (replacements.ReplacementOffsets.Count > 0)
            {
                using (var stream = new FileStream(
                    pchPath,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.None))
                {
                    var replacementsBytes = replacements.ToByteArray();
                    stream.Seek(0, SeekOrigin.End);
                    await stream.WriteAsync(replacementsBytes, cancellationToken);
                    var replacementsBytesLengthBytes = ToBigEndian(replacementsBytes.Length);
                    await stream.WriteAsync(replacementsBytesLengthBytes, cancellationToken);
                    await stream.WriteAsync(_portablePchHeaderBytes, cancellationToken);
                }
            }
        }

        private static byte[] ToBigEndian<T>(T value) where T : IBinaryInteger<T>
        {
            byte[] bytes = new byte[sizeof(int)];
            value.WriteBigEndian(bytes);
            return bytes;
        }

        private static T FromBigEndian<T>(byte[] bytes) where T : IBinaryInteger<T>
        {
            return T.ReadBigEndian(bytes, false);
        }

        public async Task ConvertPotentialPortablePchToPch(
            string pchPath,
            string buildLayoutPath,
            CancellationToken cancellationToken)
        {
            using (var stream = new FileStream(
                pchPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None))
            {
                var vcPchHeaderBytes = new byte[_vcPchHeaderBytes.Length];
                await stream.ReadExactlyAsync(vcPchHeaderBytes, cancellationToken);
                if (Encoding.ASCII.GetString(vcPchHeaderBytes) != Encoding.ASCII.GetString(_vcPchHeaderBytes))
                {
                    // This isn't a MSVC PCH file.
                    return;
                }
                stream.Seek(-_portablePchHeaderBytes.Length, SeekOrigin.End);
                var portableHeaderBuffer = new byte[_portablePchHeaderBytes.Length];
                await stream.ReadExactlyAsync(portableHeaderBuffer, cancellationToken);
                if (Encoding.ASCII.GetString(portableHeaderBuffer) != _portablePchHeader)
                {
                    // This isn't a portable PCH file.
                    return;
                }

                stream.Seek((-_portablePchHeaderBytes.Length) - sizeof(Int32), SeekOrigin.End);
                var replacementsBytesLengthBytes = new byte[sizeof(Int32)];
                await stream.ReadExactlyAsync(replacementsBytesLengthBytes, cancellationToken);
                var replacementsBytesLength = FromBigEndian<Int32>(replacementsBytesLengthBytes);
                stream.Seek((-_portablePchHeaderBytes.Length) - sizeof(Int32) - replacementsBytesLength, SeekOrigin.End);
                var replacementsBytes = new byte[replacementsBytesLength];
                await stream.ReadExactlyAsync(replacementsBytes, cancellationToken);

                var replacementLocations = PchFileReplacementLocations.Parser.ParseFrom(replacementsBytes);

                var buildLayoutPathBytes = Encoding.ASCII.GetBytes(buildLayoutPath);

                if (buildLayoutPathBytes.Length != replacementLocations.PortablePathPrefixLength)
                {
                    throw new ArgumentException($"The build layout path '{buildLayoutPath}' must be {replacementLocations.PortablePathPrefixLength} bytes long, as this is the prefix length detected when scanning the PCH, but it is {buildLayoutPathBytes.Length} instead.");
                }

                foreach (var replacementLocation in replacementLocations.ReplacementOffsets)
                {
                    stream.Seek(replacementLocation, SeekOrigin.Begin);
                    await stream.WriteAsync(buildLayoutPathBytes, cancellationToken);
                }

                stream.SetLength(stream.Length - _portablePchHeaderBytes.Length - sizeof(Int32) - replacementsBytesLength);
            }
        }
    }
}
