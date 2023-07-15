namespace Redpoint.Git.Managed.Packfile
{
    using System.Buffers.Binary;
    using System.IO.MemoryMappedFiles;

    /// <summary>
    /// Represents a Git packfile index.
    /// </summary>
    public class PackfileIndex : IDisposable
    {
        private static readonly byte[] _header = new byte[] { 0xFF, 0x74, 0x4F, 0x63, 0x00, 0x00, 0x00, 0x02 };
        private readonly MemoryMappedFile _file;
        private readonly MemoryMappedViewAccessor _viewAccessor;
        private readonly uint _objectCount;
        private bool _disposed = false;

        /// <summary>
        /// Construct a new Git packfile index by memory mapping the file at the specified path.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the specified file is not a packfile index.</exception>
        public PackfileIndex(string path)
        {
            _file = MemoryMappedFile.CreateFromFile
                (path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            _viewAccessor = _file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            var fileHeaderBytes = new byte[8];
            if (_viewAccessor.ReadArray(0, fileHeaderBytes, 0, fileHeaderBytes.Length) != fileHeaderBytes.Length)
            {
                throw new ArgumentException("The requested file is not long enough to contain a packfile index header.", nameof(path));
            }
            if (!fileHeaderBytes.SequenceEqual(_header))
            {
                throw new ArgumentException("The requested file did not contain the expected packfile index header.", nameof(path));
            }

            _objectCount = LowLevelGetCumulativeNumberOfObjectsByEndOfSectionForMostSignificantByte((byte)0xFFu);
        }

        /// <summary>
        /// The number of objects in the packfile index.
        /// </summary>
        public uint ObjectCount => _objectCount;

        /// <summary>
        /// Uses the fanout table and a binary search to locate the offset in this
        /// packfile index where information about the object specified by the SHA-1
        /// hash is stored. You can then use <see cref="GetPackfileOffsetForObjectIndex(uint)"/>
        /// to get the object's offset in the actual packfile.
        /// </summary>
        /// <param name="sha">The object hash to search for.</param>
        /// <param name="objectIndex">The index of the object in this packfile index, if this returns true.</param>
        /// <returns>True if the object exists in the packfile, false otherwise.</returns>
        public bool GetObjectIndexForObjectSha(UInt160 sha, out uint objectIndex)
        {
            // Use the fanout table to figure out where the section of objects starts and ends.
            var objectsPreceding = LowLevelGetNumberOfObjectsPrecedingSectionForMostSignificantByte(sha.MostSignificantByte);
            var cumulativeObjects = LowLevelGetCumulativeNumberOfObjectsByEndOfSectionForMostSignificantByte(sha.MostSignificantByte);
            var objectsWithinSection = cumulativeObjects - objectsPreceding;

            // If there are no objects that start with the same byte as the provided SHA-1 hash,
            // then the target object obviously does not exist.
            if (objectsWithinSection == 0)
            {
                objectIndex = 0;
                return false;
            }

            // If there is only one object in this section, then it must exactly match or the
            // then the target object does not exist.
            if (objectsWithinSection == 1)
            {
                var candidateHash = GetShaForObjectIndex(objectsPreceding);
                if (sha == candidateHash)
                {
                    objectIndex = objectsPreceding;
                    return true;
                }
                else
                {
                    objectIndex = 0;
                    return false;
                }
            }

            // Set up our initial search bounds. We will always have at least two objects at this
            // point, with the lower pointing to the first one and the upper point to the last one.
            //
            // 'lower', 'upper' and 'midpoint' refer to the index of the object metadata within the
            // packfile index, and not byte offsets or hashes.
            //
            // Note that both 'lower' and 'upper' must be inclusive; either of them must have a real
            // possibility of being the target hash we're looking for. Or to put it another way,
            // lower must always refer to a hash that is less than or equal to the target hash, and
            // upper must always refer to a hash that is greater than or equal to the target hash.
            var lower = objectsPreceding;
            var upper = objectsPreceding + objectsWithinSection - 1;

            // It is possible on the first iteration that the 'lower' refers to a hash that is
            // above our target hash, because the target hash we're looking for would sit before
            // the start of the section that the fanout table refers to. We can't have the 'lower'
            // sitting above our target hash for the binary search, so handle this case now.
            if (sha < GetShaForObjectIndex(lower))
            {
                objectIndex = 0;
                return false;
            }

            // It is possible on the first iteration that the 'upper' refers to a hash that is
            // below our target hash, because the target hash we're looking for would sit after
            // the end of the section that the fanout table refers to. We can't have the 'upper'
            // sitting below our target hash for the binary search, so handle this case now.
            if (sha > GetShaForObjectIndex(upper))
            {
                objectIndex = 0;
                return false;
            }

            // Do the binary search.
            byte depthWithinHash = 1;
            do
            {
                var targetHashByteValue = sha[depthWithinHash];

                // @note: midpoint must never be equal to lower, because that would not
                // give us any space to search.
                var midpoint = (upper == lower + 1) ? upper : (lower + ((upper - lower) / 2));
                var midpointHash = GetShaForObjectIndex(midpoint);
                var midpointHashByteValue = midpointHash[depthWithinHash];
                if (midpointHashByteValue > targetHashByteValue)
                {
                    // If this was the last byte to compare, then the target hash does not exist in the
                    // packfile.
                    if (depthWithinHash == 19)
                    {
                        objectIndex = 0;
                        return false;
                    }

                    // If the midpoint already is the upper, then we have no space left to search and
                    // we did not find the target hash.
                    //
                    // target   == 44 55 66 77 77 77
                    //
                    // index ---------------VV
                    // lower    -> 44 55 66 00 00 00
                    // midpoint -> 44 55 66 88 88 88
                    // upper    -> 44 55 66 88 88 88
                    //
                    if (midpoint == upper)
                    {
                        objectIndex = 0;
                        return false;
                    }

                    // The midpoint logically comes after our target hash, so we know that the target hash
                    // is now within 'lower -> midpoint'.
                    upper = midpoint;
                }
                else if (midpointHashByteValue < targetHashByteValue)
                {
                    // If this was the last byte to compare, then the target hash does not exist in the
                    // packfile.
                    if (depthWithinHash == 19)
                    {
                        objectIndex = 0;
                        return false;
                    }

                    // It is not possible to run into the scenario shown below, because we would never
                    // move the midpoint down on the previous iteration. Instead, we would have moved
                    // the lower up to the midpoint, which would then have made the upper the new
                    // midpoint on this iteration (and thus we would have entered a different branch
                    // because the midpoint is higher or equal, not lower).
                    //
                    // target   == 44 55 66 77 77 77
                    //
                    // [previous iteration]
                    // index ---------------VV
                    // lower    -> 44 55 66 00 00 00
                    // midpoint -> 44 55 66 66 66 66
                    // upper    -> 44 55 66 88 88 88
                    //
                    // [this iteration] (not possible)
                    // index ---------------VV
                    // lower    -> 44 55 66 00 00 00
                    // midpoint -> 44 55 66 66 66 66
                    // upper    -> 44 55 66 66 66 66
                    //
                    // It is not possible to run into the scenario shown below, where we increased
                    // our depth upon equality in the previous iteration, because that branch would not
                    // have moved the midpoint down as the target SHA-1 is less than the midpoint on
                    // a full comparison. Instead, we would have moved the lower up to the midpoint.
                    //
                    // target   == 44 55 66 77 77 77
                    //
                    // [previous iteration]
                    // index ------------VV
                    // lower    -> 44 55 66 00 00 00
                    // midpoint -> 44 55 66 66 66 66
                    // upper    -> 44 55 66 88 88 88
                    //
                    // [this iteration] (not possible)
                    // index ---------------VV
                    // lower    -> 44 55 66 00 00 00
                    // midpoint -> 44 55 66 66 66 66
                    // upper    -> 44 55 66 66 66 66
                    //
                    // It is not possible to run into the scenario shown below, because we handle cases
                    // where the initial upper would be below our target hash prior to starting the
                    // binary search.
                    //
                    // target   == 44 55 66 77 77 77
                    //
                    // [this iteration] (not possible)
                    // index ---------VV
                    // lower    -> 44 00 00 00 00 00
                    // midpoint -> 44 44 44 44 44 44
                    // upper    -> 44 44 44 44 44 44
                    //
                    // Finally, the midpoint can never be chosen as the lower at the start of the
                    // iteration due to the way we set up midpoint.
                    //
                    // Therefore, if midpoint is ever equal to lower, it is a bug somewhere in this code.
                    if (midpoint == lower)
                    {
                        throw new InvalidOperationException("midpoint == lower in binary search, which should not be possible.");
                    }

                    // The midpoint logically comes before our target hash, so we know that the target
                    // hash is now within 'midpoint -> upper'.
                    lower = midpoint;
                }
                else // midpointHashByteValue == targetHashByteValue
                {
                    // @note: If we're comparing the last index, then this is an exact match anyway.
                    // Otherwise, fully compare the hash to see if it's what we're looking for.
                    if (depthWithinHash == 19 || sha == midpointHash)
                    {
                        // The midpoint *is* exactly the object we're looking for.
                        objectIndex = midpoint;
                        return true;
                    }

                    // The midpoint could be either above or below the target hash. For example,
                    // we could have the following scenario with hashes looking at index 4.
                    //
                    // target   == 44 55 66 77 77 77
                    //
                    // index ---------------VV
                    // lower    -> 44 55 66 00 00 00
                    //             ..
                    // midpoint -> 44 55 66 77 88 88
                    //             ..
                    // upper    -> 44 55 66 99 99 99
                    //
                    // From a comparison of the current byte index alone, we can't tell whether
                    // the midpoint sits above or below the target hash. We need to know this 
                    // because it tells us whether or not the midpoint will become the lower
                    // bound (the target is after it) or the upper bound (the target is before it).
                    //
                    // We could iteratively scan down bytes until we find a difference, or we
                    // can let the UInt160 just figure it out for us now (Also we know they
                    // can't exactly match here because we just checked for that).
                    if (sha < midpointHash)
                    {
                        // If the midpoint currently is the upper, then we were only considering
                        // two possible hashes: the upper, which we now know is not the target hash
                        // because we just checked it via the midpoint, or the lower, which we 
                        // are yet to compare. If the lower isn't the target hash, then the
                        // search space has been exhausted.
                        //
                        // target   == 44 55 66 77 77 77
                        //
                        // index ---------------VV
                        // lower    -> 44 55 66 00 00 00
                        // midpoint -> 44 55 66 77 88 88
                        // upper    -> 44 55 66 77 88 88
                        //
                        if (sha != GetShaForObjectIndex(lower))
                        {
                            objectIndex = 0;
                            return false;
                        }

                        // The midpoint is above the target, let the midpoint become the new upper.
                        upper = midpoint;
                    }
                    else
                    {
                        // The lower can never equal the current midpoint. Even in the scenario
                        // below where there are no hashes between the lower, midpoint and upper, we
                        // will still make progress because the lower will be set to the midpoint,
                        // and then the next iteration the midpoint will become the upper.
                        //
                        // target   == 44 55 66 77 77 77
                        //
                        // index ---------------VV
                        // lower    -> 44 55 66 00 00 00
                        // midpoint -> 44 55 66 77 66 66
                        // upper    -> 44 55 66 77 88 88
                        //

                        // The midpoint is below the target, let the midpoint become the new lower.
                        lower = midpoint;
                    }

                    // On the next iteration, check the next byte. We don't need to worry about
                    // doing a redundant 'sha == midpointHash' check above, because the midpoint
                    // must always have moved as part of the previous if/else check.
                    depthWithinHash++;
                }
            } while (lower != upper);

            throw new InvalidOperationException("Did not expect binary search to terminate without determining a result.");
        }

        /// <summary>
        /// Gets the number of objects preceding the start of the section whose objects'
        /// SHA-1 hash starts with the specified byte.
        /// </summary>
        /// <param name="byte">The most significant SHA-1 byte.</param>
        /// <returns>The number of objects preceding the start of the section.</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public uint LowLevelGetNumberOfObjectsPrecedingSectionForMostSignificantByte(byte @byte)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PackfileIndex));
            }

            uint numberOfObjectsPreceding;
            if (@byte == 0)
            {
                // Objects that start with 00 are the first in the packfile.
                numberOfObjectsPreceding = 0;
            }
            else
            {
                // The fanout index indicates the "end of" the section of objects
                // that start with a given byte. This is why 255 (ff) is the total
                // number of objects, because it's the end of the 255 (ff) section.
                //
                // Therefore if we want to get the start of the 110 (6e) section,
                // we actually have to look at index 109 for the end of the 
                // 109 (6d) section.
                numberOfObjectsPreceding = LowLevelGetCumulativeNumberOfObjectsByEndOfSectionForMostSignificantByte((byte)(@byte - 1));
            }

            return numberOfObjectsPreceding;
        }

        /// <summary>
        /// Gets the cumulative number of objects by the end of the section whose objects'
        /// SHA-1 hash starts with the specified byte.
        /// </summary>
        /// <param name="byte">The most significant SHA-1 byte.</param>
        /// <returns>The cumulative number of objects by the end of the section.</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public uint LowLevelGetCumulativeNumberOfObjectsByEndOfSectionForMostSignificantByte(byte @byte)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PackfileIndex));
            }

            var fanoutValueOffset = _header.Length + @byte * sizeof(int);
            var cumulativeNumberOfObjects = ConvertFromNetworkByteOrder(_viewAccessor.ReadUInt32(fanoutValueOffset));
            return cumulativeNumberOfObjects;
        }

        /// <summary>
        /// Returns the SHA-1 hash at the specified object index in the packfile index.
        /// </summary>
        /// <param name="index">The object index, which must be less than <see cref="ObjectCount"/>.</param>
        /// <returns>The SHA-1 hash.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="index"/> is greater than or equal to <see cref="ObjectCount"/>.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the instance has already been disposed.</exception>
        public UInt160 GetShaForObjectIndex(uint index)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PackfileIndex));
            }

            if (index >= _objectCount)
            {
                throw new ArgumentException("Index must be less than the object count.", nameof(index));
            }

            var offset = _header.Length + 256 * sizeof(int) + index * 20;
            _viewAccessor.Read<UInt160>(offset, out var entry);
            return entry;
        }

        /// <summary>
        /// Returns the CRC checksum at the specified object index in the packfile index.
        /// </summary>
        /// <param name="index">The object index, which must be less than <see cref="ObjectCount"/>.</param>
        /// <returns>The CRC checksum.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="index"/> is greater than or equal to <see cref="ObjectCount"/>.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the instance has already been disposed.</exception>
        public uint GetCrcForObjectIndex(uint index)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PackfileIndex));
            }

            if (index >= _objectCount)
            {
                throw new ArgumentException("Index must be less than the object count.", nameof(index));
            }

            var offset = _header.Length + 256 * sizeof(int) + _objectCount * 20 + index * sizeof(uint);
            return _viewAccessor.ReadUInt32(offset);
        }

        /// <summary>
        /// Returns the offset at the specified object index in the packfile index.
        /// </summary>
        /// <param name="index">The object index, which must be less than <see cref="ObjectCount"/>.</param>
        /// <returns>The offset in the main packfile of this object.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="index"/> is greater than or equal to <see cref="ObjectCount"/>.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the instance has already been disposed.</exception>
        public ulong GetPackfileOffsetForObjectIndex(uint index)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PackfileIndex));
            }

            if (index >= _objectCount)
            {
                throw new ArgumentException("Index must be less than the object count.", nameof(index));
            }

            var offset = _header.Length + 256 * sizeof(int) + _objectCount * 20 + _objectCount * sizeof(uint) + index * sizeof(uint);
            var offsetInPackfile = ConvertFromNetworkByteOrder(_viewAccessor.ReadUInt32(offset));

            if ((offsetInPackfile & 1u << 31) != 0)
            {
                // This refers to an index in the 64-byte offset table.
                var offsetInLargeIndex = offsetInPackfile & ~(1u << 31);
                var largeIndexOffsetInIndex = _header.Length + 256 * sizeof(int) + _objectCount * 20 + _objectCount * sizeof(uint) + _objectCount * sizeof(uint) + offsetInLargeIndex * sizeof(ulong);
                var v = _viewAccessor.ReadUInt64(largeIndexOffsetInIndex);
                return ConvertFromNetworkByteOrder(v);
            }
            else
            {
                return offsetInPackfile;
            }
        }

        private uint ConvertFromNetworkByteOrder(uint v)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BinaryPrimitives.ReverseEndianness(v);
            }
            return v;
        }

        private ulong ConvertFromNetworkByteOrder(ulong v)
        {
            if (BitConverter.IsLittleEndian)
            {
                return BinaryPrimitives.ReverseEndianness(v);
            }
            return v;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PackfileIndex));
            }

            _disposed = true;
            _viewAccessor.Dispose();
            _file.Dispose();
        }
    }
}