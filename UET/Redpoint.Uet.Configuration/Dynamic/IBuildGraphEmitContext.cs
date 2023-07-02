namespace Redpoint.Uet.Configuration.Dynamic
{
    using System.Threading.Tasks;

    public interface IBuildGraphEmitContext
    {
        /// <summary>
        /// The current service provider.
        /// </summary>
        IServiceProvider Services { get; }

        /// <summary>
        /// Runs the specified block once across all providers.
        /// </summary>
        /// <param name="name">The unique name that determines whether something has run before.</param>
        /// <returns></returns>
        Task EmitOnceAsync(string name, Func<Task> runOnce);

        /// <summary>
        /// Filters out platforms that the current executor won't be able to run things on. This
        /// basically only filters out on the local executor (where you obviously can't run tasks
        /// that don't match your own platform).
        /// </summary>
        /// <param name="platform">The platform to consider.</param>
        /// <returns>Whether the platform that can actually be used.</returns>
        bool CanHostPlatformBeUsed(BuildConfigHostPlatform platform);
    }
}
