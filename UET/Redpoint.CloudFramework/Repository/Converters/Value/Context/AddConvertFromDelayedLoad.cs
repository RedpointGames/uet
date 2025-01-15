namespace Redpoint.CloudFramework.Repository.Converters.Value.Context
{
    /// <summary>
    /// This callback is provided to conversion methods so they can register delayed loads.
    /// </summary>
    /// <param name="convertFromDelayedLoad">The delayed load to invoke after all other fields are loaded.</param>
    internal delegate void AddConvertFromDelayedLoad(ConvertFromDelayedLoad convertFromDelayedLoad);
}
