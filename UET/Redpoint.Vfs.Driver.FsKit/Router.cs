#pragma warning disable APL0002
#pragma warning disable CA2213

namespace Redpoint.Vfs.Driver.FsKit
{
    using FSKit;
    using Microsoft.Extensions.Logging;

    internal class Router
    {
        private readonly ILogger _logger;

        public Router(ILogger logger)
        {
            _logger = logger;
        }

        internal static T DefaultParam<T>()
        {
            if (typeof(T) == typeof(NSError))
            {
                return (T)(object)FSKitFunctions.GetErrorForPosixError((int)PosixError.EIO)!;
            }
            else
            {
                return default!;
            }
        }

        internal void Call<TReply, T1>(Action<T1> reply, Func<T1> impl)
        {
            try
            {
                var result = impl();
                reply(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                reply(DefaultParam<T1>());
            }
        }

        internal void Call<T1, T2>(Action<T1, T2> reply, Func<(T1, T2)> impl)
        {
            try
            {
                var result = impl();
                reply(result.Item1, result.Item2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                reply(DefaultParam<T1>(), DefaultParam<T2>());
            }
        }

        internal void Call<T1, T2, T3>(Action<T1, T2, T3> reply, Func<(T1, T2, T3)> impl)
        {
            try
            {
                var result = impl();
                reply(result.Item1, result.Item2, result.Item3);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                reply(DefaultParam<T1>(), DefaultParam<T2>(), DefaultParam<T3>());
            }
        }
    }
}
