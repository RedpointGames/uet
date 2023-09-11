namespace Redpoint.Vfs.Layer.Scratch
{
    using System;
    using System.Collections.Generic;
    using Tenray.ZoneTree;
    using Tenray.ZoneTree.Exceptions;
    using Tenray.ZoneTree.Options;

    internal sealed class ScratchIndex : IDisposable
    {
        private IZoneTree<string, byte> _zoneTree;

        public ScratchIndex(string path)
        {
            try
            {
                _zoneTree = new ZoneTreeFactory<string, byte>()
                    .SetDataDirectory(path)
                    .ConfigureWriteAheadLogOptions(configure =>
                    {
                        configure.WriteAheadLogMode = WriteAheadLogMode.Sync;
                    })
                    .OpenOrCreate();
            }
            catch (WriteAheadLogCorruptionException ex)
            {
                throw new ScratchIndexCorruptException("The scratch index could not be loaded because it is corrupt. Construct a new ScratchIndex, rebuild the index and then move it into place.", ex);
            }
        }

        public void Dispose()
        {
            _zoneTree.Dispose();
        }

        public (bool found, byte status) Get(string normalizedPath)
        {
            if (_zoneTree.TryGet(normalizedPath, out byte value))
            {
                return (true, value);
            }
            return (false, 0);
        }

        public void Set(string normalizedPath, byte status)
        {
            _zoneTree.Upsert(normalizedPath, status);
        }

        public void Delete(string normalizedPath)
        {
            _zoneTree.ForceDelete(normalizedPath);
        }

        public IEnumerable<(string normalizedPath, byte status)> Iterate()
        {
            using (var it = _zoneTree.CreateIterator())
            {
                while (it.Next())
                {
                    yield return (it.CurrentKey, it.CurrentValue);
                }
            }
        }

        public IEnumerable<string> IterateKeysOnly()
        {
            using (var it = _zoneTree.CreateIterator())
            {
                while (it.Next())
                {
                    yield return it.CurrentKey;
                }
            }
        }
    }
}
