namespace Redpoint.Uet.Workspace.Descriptors
{
    public enum GitWorkspaceDescriptorBuildType
    {
        /// <summary>
        /// This is a project or plugin build.
        /// </summary>
        Generic,

        /// <summary>
        /// This is a checkout of the engine itself, instead of a project or plugin, from the Unreal Engine GitHub repository where we need to run GitDeps and potentially copy console files.
        /// </summary>
        Engine,

        /// <summary>
        /// This is a checkout of the engine itself, where all the engine files have been stored in a Git repository with LFS (and there's no external console folders or GitDeps to run). This is the case if you externally sync Perforce into a Git repository with LFS.
        /// </summary>
        EngineLfs,
    }
}
