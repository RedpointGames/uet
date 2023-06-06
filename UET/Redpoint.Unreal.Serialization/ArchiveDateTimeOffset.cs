namespace Redpoint.Unreal.Serialization
{
    using System;

    public static class ArchiveDateTimeOffset
    {
        public static async Task Serialize(this Archive ar, Store<DateTimeOffset> timestamp)
        {
            if (ar.IsLoading)
            {
                var ticks = new Store<long>(0);
                await ar.Serialize(ticks);
                timestamp.V = new DateTimeOffset(ticks.V, TimeSpan.Zero);
            }
            else
            {
                var ticks = new Store<long>(timestamp.V.Ticks);
                await ar.Serialize(ticks);
            }
        }
    }
}
