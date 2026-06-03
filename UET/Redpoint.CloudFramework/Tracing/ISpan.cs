namespace Redpoint.CloudFramework.Tracing
{
    using System;

    public interface ISpan : IDisposable
    {
        string DisplayName { get; set; }

        void SetTag(string key, string value);
    }
}
