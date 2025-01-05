namespace Redpoint.CloudFramework.Configuration
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the current value of a Google Cloud Secret Manager secret. 
    /// The <see cref="Data"/> property is automatically updated and the 
    /// <see cref="OnRefreshed"/> callback fired whenever the secret versions 
    /// are modified.
    /// </summary>
    public interface IAutoRefreshingSecret : IAsyncDisposable
    {
        /// <summary>
        /// The value of the secret JSON keyed for the configuration system. That
        /// is, a JSON value of {"Test":{"A":"World"}} in the secret would be
        /// represented with the key "Test:A" equalling "World" in this dictionary.
        /// </summary>
        IDictionary<string, string?> Data { get; }

        /// <summary>
        /// An optional callback that is fired whenever the <see cref="Data"/>
        /// property is updated with new data.
        /// </summary>
        Action? OnRefreshed { get; set; }
    }
}
