namespace Redpoint.CloudFramework.Repository.Migration
{
    using Redpoint.CloudFramework.Models;

    /// <summary>
    /// This isn't a real model; we just use it to construct keys for locking.
    /// </summary>
    [Kind("rcf_migrationLock")]
    internal class MigrationLockModel : Model<MigrationLockModel>
    {
    }
}
