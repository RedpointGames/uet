namespace Redpoint.Git.Abstractions
{
    /// <summary>
    /// Represents metrics of a recursive enumeration.
    /// </summary>
    public class GitTreeEnumerationMetrics
    {
        private readonly Action<long> _onObjectsMappedUpdated;
        private long _objectsMapped = 0;

        /// <summary>
        /// Constructs a new <see cref="GitTreeEnumerationMetrics"/> for tracking enumeration metrics.
        /// </summary>
        /// <param name="onObjectsMappedUpdated">The callback to fire when the number of objects mapped changes.</param>
        public GitTreeEnumerationMetrics(Action<long> onObjectsMappedUpdated)
        {
            _onObjectsMappedUpdated = onObjectsMappedUpdated;
        }

        /// <summary>
        /// The number of objects mapped in the recursive enumeration.
        /// </summary>
        public long ObjectsMapped
        {
            get => _objectsMapped;
            set
            {
                _objectsMapped = value;
                _onObjectsMappedUpdated(_objectsMapped);
            }
        }
    }
}
