namespace Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels
{
    using Redpoint.Uefs.Protocol;

    public static class PersistenceModeSerializer
    {
        public static string ToString(WriteScratchPersistence scratchPersistence, StartupBehaviour startupBehaviour)
        {
            switch (startupBehaviour)
            {
                case StartupBehaviour.None:
                    return "none";
                case StartupBehaviour.MountOnStartup:
                    switch (scratchPersistence)
                    {
                        case WriteScratchPersistence.DiscardOnUnmount:
                            return "ro";
                        case WriteScratchPersistence.Keep:
                            return "rw";
                    }
                    break;
            }
            return "none";
        }

        public static (WriteScratchPersistence scratchPersistence, StartupBehaviour startupBehaviour) ToMode(string mode, bool error = false)
        {
            if (mode == null) throw new ArgumentNullException(nameof(mode));

            switch (mode.Trim())
            {
                case "":
                case "none":
                    return (WriteScratchPersistence.DiscardOnUnmount, StartupBehaviour.None);
                case "ro":
                    return (WriteScratchPersistence.DiscardOnUnmount, StartupBehaviour.MountOnStartup);
                case "rw":
                    return (WriteScratchPersistence.Keep, StartupBehaviour.MountOnStartup);
                default:
                    if (error)
                    {
                        throw new ArgumentException("--persist should be one of 'none', 'ro' or 'rw'.", nameof(mode));
                    }
                    return (WriteScratchPersistence.DiscardOnUnmount, StartupBehaviour.None);
            }
        }
    }
}
