namespace Redpoint.CloudFramework.Tracing
{
    using System;

    public interface ISpan : IDisposable
    {
        string DisplayName { get; set; }

        void SetTag(string key, string value);
    }

    public interface ICacheSpan : ISpan
    {
        string Key { get; set; }
    }

    public interface ICachePutSpan : ICacheSpan
    {
        bool Write { get; set; }
    }

    public interface ICacheGetSpan : ICacheSpan
    {
        bool Hit { get; set; }
    }
}
