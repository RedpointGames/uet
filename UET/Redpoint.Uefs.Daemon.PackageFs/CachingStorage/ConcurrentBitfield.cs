namespace Redpoint.Uefs.Daemon.PackageFs.CachingStorage
{
    using System.Text;

    internal class ConcurrentBitfield
    {
        private ulong[] _index;

        private ConcurrentBitfield(ulong[] index)
        {
            _index = index;
        }

        public ConcurrentBitfield(ulong bits)
        {
            _index = new ulong[bits / 64];
        }

        public static ConcurrentBitfield? LoadFromFile(string path, ulong bits)
        {
            int length;
            ulong[] index;
            using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 4096))
            {
                using (var binary = new BinaryReader(reader, Encoding.UTF8, true))
                {
                    length = binary.ReadInt32();
                    index = new ulong[length];
                    for (int i = 0; i < length; i++)
                    {
                        index[i] = binary.ReadUInt64();
                    }
                }
            }
            if (index.Length != (int)(bits / 64))
            {
                return null;
            }
            return new ConcurrentBitfield(index);
        }

        public void SaveToFile(string path)
        {
            using (var writer = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096))
            {
                using (var binary = new BinaryWriter(writer, Encoding.UTF8, true))
                {
                    Int32 length = _index.Length;
                    binary.Write(length);
                    for (int i = 0; i < length; i++)
                    {
                        binary.Write(Interlocked.Read(ref _index[i]));
                    }
                }

                writer.Flush();
            }
        }

        public bool Get(ulong bit)
        {
            return (_index[bit / 64] & (1UL << (int)(bit % 64))) != 0;
        }

        public void SetOn(ulong bit)
        {
            Interlocked.Or(ref _index[bit / 64], 1UL << (int)(bit % 64));
        }

        public void SetOff(ulong bit)
        {
            Interlocked.And(ref _index[bit / 64], ~(1UL << (int)(bit % 64)));
        }
    }
}
