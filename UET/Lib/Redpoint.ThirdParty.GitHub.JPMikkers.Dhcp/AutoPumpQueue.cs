using System.Collections.Concurrent;
using System.Threading;

namespace GitHub.JPMikkers.Dhcp;

public class AutoPumpQueue<T>
{
    public delegate void DataDelegate(AutoPumpQueue<T> sender, T data);
    private readonly object _dispatchSync = new();
    private readonly ConcurrentQueue<T> _queue;
    private readonly DataDelegate _dataDelegate;

    /// <summary>
    /// Constructor
    /// </summary>
    public AutoPumpQueue(DataDelegate dataDelegate)
    {
        _queue = new ConcurrentQueue<T>();
        _dataDelegate = dataDelegate;
    }

    private void WaitCallback(object? state)
    {
        lock(_dispatchSync)    // ensures individual invokes are serialized
        {
            while(_queue.TryDequeue(out var data))
            {
                try
                {
                    _dataDelegate(this, data);
                }
                catch
                {
                }
            }
        }
    }

    public void Enqueue(T data)
    {
        bool queueWasEmpty = _queue.IsEmpty;
        _queue.Enqueue(data);

        if(queueWasEmpty)
        {
            ThreadPool.QueueUserWorkItem(WaitCallback);
        }
    }
}
