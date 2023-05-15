namespace Redpoint.Unreal.Serialization
{
    using System;

    public static class ArchiveDateTimeOffset
    {
        public static void Serialize(this Archive ar, ref DateTimeOffset timestamp)
        {
            if (ar.IsLoading)
            {
                long ticks = 0;
                ar.Serialize(ref ticks);
                timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);
            }
            else
            {
                long ticks = timestamp.Ticks;
                ar.Serialize(ref ticks);
            }
        }
    }
}
