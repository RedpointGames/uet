namespace Redpoint.CloudFramework.Tracing
{
    using System;

    public interface ISpan : IDisposable
    {
        void SetTag(string key, string value);
        void SetExtra(string key, object? value);
    }
}
