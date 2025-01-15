namespace Redpoint.CloudFramework.Configuration
{
    using System;
    using System.Collections.Generic;

    internal class EmptyAutoRefreshingSecret : IAutoRefreshingSecret
    {
        public EmptyAutoRefreshingSecret()
        {
            Data = new Dictionary<string, string?>();
        }

        public IDictionary<string, string?> Data { get; private init; }

        public Action? OnRefreshed { get; set; }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
