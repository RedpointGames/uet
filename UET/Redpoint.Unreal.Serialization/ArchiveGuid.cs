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

        public static async ValueTask Serialize(this Archive ar, Store<Guid> guid)
        {
            if (ar.IsLoading)
            {
                Store<int> a = new Store<int>(0),
                    b = new Store<int>(0),
                    c = new Store<int>(0),
                    d = new Store<int>(0);
                await ar.Serialize(a);
                await ar.Serialize(b);
                await ar.Serialize(c);
                await ar.Serialize(d);
                guid.V = GuidFromInts(a.V, b.V, c.V, d.V);
            }
            else
            {
                var (a, b, c, d) = IntsFromGuid(guid.V);
                await ar.Serialize(new Store<int>(a));
                await ar.Serialize(new Store<int>(b));
                await ar.Serialize(new Store<int>(c));
                await ar.Serialize(new Store<int>(d));
            }
        }
    }
}
