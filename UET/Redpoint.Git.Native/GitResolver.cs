namespace Redpoint.Git.Native
{
    using LibGit2Sharp;

    public static class GitResolver
    {
        public static string? ResolveToCommitHash(Repository repository, string @ref)
        {
            try
            {
                repository.RevParse(@ref, out Reference r, out GitObject o);
                if (r == null)
                {
                    if (o != null)
                    {
                        var directCommit = repository.Lookup<Commit>(o.Sha);
                        if (directCommit != null)
                        {
                            return o.Sha;
                        }
                        return null;
                    }

                    return null;
                }
                var dirRef = r.ResolveToDirectReference();
                if (dirRef == null)
                {
                    return null;
                }
                var target = dirRef.Target;
                if (target == null)
                {
                    return null;
                }
                string commitHash;
                if (target is TagAnnotation ta)
                {
                    commitHash = ta.Target.Sha;
                }
                else
                {
                    commitHash = target.Sha;
                }
                var commit = repository.Lookup<Commit>(commitHash);
                if (commit != null)
                {
                    return commitHash;
                }
                return null;
            }
            catch (NotFoundException)
            {
                return null;
            }
        }
    }
}
