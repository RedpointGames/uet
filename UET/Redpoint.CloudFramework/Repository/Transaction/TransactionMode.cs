namespace Redpoint.CloudFramework.Repository.Transaction
{
    /// <summary>
    /// The type of transaction being performed. The default is read-write, but you can
    /// specify <see cref="ReadOnly"/> for improved performance.
    /// </summary>
    public enum TransactionMode
    {
        /// <summary>
        /// This transaction modifies the repository.
        /// </summary>
        ReadWrite,

        /// <summary>
        /// The transaction only reads data from the repository, but needs a consistent view across
        /// multiple reads or queries.
        /// </summary>
        ReadOnly,
    }
}
