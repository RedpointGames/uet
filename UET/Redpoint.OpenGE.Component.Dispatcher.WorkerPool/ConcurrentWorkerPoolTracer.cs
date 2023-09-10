namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public class ConcurrentWorkerPoolTracer : WorkerPoolTracer
    {
        private readonly ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();

        public override void AddTracingMessage(string message, [CallerMemberName] string memberName = "")
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
