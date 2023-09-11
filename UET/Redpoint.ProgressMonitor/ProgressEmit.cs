namespace Redpoint.ProgressMonitor
{
    /// <summary>
    /// Called by monitors when they want to emit progress information.
    /// </summary>
    /// <param name="message">The message to emit.</param>
    /// <param name="emitNumber">This number increases by 1 each time this delegate is called, which allows you to only emit every X messages in the delegate.</param>
    public delegate void ProgressEmit(string message, long emitNumber);
}
