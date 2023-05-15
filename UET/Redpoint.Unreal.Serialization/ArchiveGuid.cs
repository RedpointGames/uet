namespace Redpoint.Unreal.Serialization
{
    using System;

    public static class ArchiveGuid
    {
        public static Guid GuidFromInts(int a, int b, int c, int d)
        {
            unsafe
            {
                var guidBytes = new byte[16];
                for (int i = 0; i < sizeof(int); i++)
                {
                    guidBytes[i] = ((byte*)((int*)&a))[3 - i];
                }
                for (int i = 0; i < sizeof(int); i++)
                {
                    guidBytes[i + 4] = ((byte*)((int*)&b))[3 - i];
                }
                for (int i = 0; i < sizeof(int); i++)
                {
                    guidBytes[i + 8] = ((byte*)((int*)&c))[3 - i];
                }
                for (int i = 0; i < sizeof(int); i++)
                {
                    guidBytes[i + 12] = ((byte*)((int*)&d))[3 - i];
                }
                return new Guid(guidBytes);
            }
        }

        public static (int a, int b, int c, int d) IntsFromGuid(Guid guid)
        {
            int a = 0, b = 0, c = 0, d = 0;
            var guidBytes = guid.ToByteArray();
            unsafe
            {
                for (int i = 0; i < sizeof(int); i++)
                {
                    ((byte*)((int*)&a))[3 - i] = guidBytes[i];
                }
                for (int i = 0; i < sizeof(int); i++)
                {
                    ((byte*)((int*)&b))[3 - i] = guidBytes[i + 4];
                }
                for (int i = 0; i < sizeof(int); i++)
                {
                    ((byte*)((int*)&c))[3 - i] = guidBytes[i + 8];
                }
                for (int i = 0; i < sizeof(int); i++)
                {
                    ((byte*)((int*)&d))[3 - i] = guidBytes[i + 12];
                }
            }
            return (a, b, c, d);
        }

        public static void Serialize(this Archive ar, ref Guid guid)
        {
            if (ar.IsLoading)
            {
                int a = 0, b = 0, c = 0, d = 0;
                ar.Serialize(ref a);
                ar.Serialize(ref b);
                ar.Serialize(ref c);
                ar.Serialize(ref d);
                guid = GuidFromInts(a, b, c, d);
            }
            else
            {
                var (a, b, c, d) = IntsFromGuid(guid);
                ar.Serialize(ref a);
                ar.Serialize(ref b);
                ar.Serialize(ref c);
                ar.Serialize(ref d);
            }
        }
    }
}
