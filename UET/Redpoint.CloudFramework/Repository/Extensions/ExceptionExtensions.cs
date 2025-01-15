namespace Redpoint.CloudFramework.Repository
{
    using Grpc.Core;

    public static class ExceptionExtensions
    {
        public static bool IsContentionException(this RpcException ex)
        {
            ArgumentNullException.ThrowIfNull(ex);

            if (ex.Status.StatusCode == StatusCode.Aborted &&
                (ex.Status.Detail.Contains("Aborted due to cross-transaction contention.", StringComparison.Ordinal) ||
                 ex.Status.Detail.Contains("too much contention on these datastore entities", StringComparison.Ordinal)))
            {
                return true;
            }

            return false;
        }

        public static bool IsTransactionExpiryException(this RpcException ex)
        {
            ArgumentNullException.ThrowIfNull(ex);

            if (ex.Status.StatusCode == StatusCode.InvalidArgument &&
                ex.Status.Detail.Contains("transaction has expired", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }
    }
}
