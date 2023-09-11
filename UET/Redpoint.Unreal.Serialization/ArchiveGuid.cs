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

        public static (int a, int b, int c, int d) IntsFromGuid(Guid value)
        {
            int a = 0, b = 0, c = 0, d = 0;
            var guidBytes = value.ToByteArray();
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

        public static async ValueTask Serialize(this Archive ar, Store<Guid> value)
        {
            if (ar == null) throw new ArgumentNullException(nameof(ar));
            if (value == null) throw new ArgumentNullException(nameof(value));

            if (ar.IsLoading)
            {
                Store<int> a = new Store<int>(0),
                    b = new Store<int>(0),
                    c = new Store<int>(0),
                    d = new Store<int>(0);
                await ar.Serialize(a).ConfigureAwait(false);
                await ar.Serialize(b).ConfigureAwait(false);
                await ar.Serialize(c).ConfigureAwait(false);
                await ar.Serialize(d).ConfigureAwait(false);
                value.V = GuidFromInts(a.V, b.V, c.V, d.V);
            }
            else
            {
                var (a, b, c, d) = IntsFromGuid(value.V);
                await ar.Serialize(new Store<int>(a)).ConfigureAwait(false);
                await ar.Serialize(new Store<int>(b)).ConfigureAwait(false);
                await ar.Serialize(new Store<int>(c)).ConfigureAwait(false);
                await ar.Serialize(new Store<int>(d)).ConfigureAwait(false);
            }
        }
    }
}
