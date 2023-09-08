namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;

    public class WorkerPoolTracer
    {
        private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();

        [Conditional("DEBUG")]
        public void AddTracingMessage(string message, [CallerMemberName] string memberName = "")
        {
            _messageQueue.Enqueue($"{DateTime.UtcNow}: {memberName}: {message}");
        }

        public IReadOnlyList<string> DumpAllMessages()
        {
            var messages = new List<string>();
            while (_messageQueue.TryDequeue(out var message))
            {
                messages.Add(message);
            }
            return messages;
        }
    }
}
