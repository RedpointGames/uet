namespace Redpoint.Uba
{
    using Redpoint.ProcessExecution;

    /// <summary>
    /// Extended options when running processes through UBA.
    /// </summary>
    public class UbaProcessSpecification : ProcessSpecification
    {
        /// <summary>
        /// If true, <see cref="IUbaServer"/> instances won't run this process locally until 
        /// they've been in the queue for at least 30 seconds without being picked up by a 
        /// remote agent.
        /// </summary>
        public bool PreferRemote { get; set; }

        /// <summary>
        /// If true, this command can be run remotely. Defaults to true if not set.
        /// </summary>
        public bool AllowRemote { get; set; } = true;
    }
}
