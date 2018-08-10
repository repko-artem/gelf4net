using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gelf4Net.Util
{
    public class BufferedLogSender
    {
        private readonly BlockingCollection<string> _pendingTasks;
        private readonly CancellationTokenSource _cts;
        private readonly Func<string, Task> _sendFunc;
        private readonly Task _sender;

        public BufferedLogSender(Func<string, Task> sendFunc)
        {
            _pendingTasks = new BlockingCollection<string>(100);
            _cts = new CancellationTokenSource();
            _sendFunc = sendFunc;
            // FIXME: use the machine's core count?
            _sender = Task.WhenAll(Enumerable.Range(1, 4).Select(_ => Task.Run(SendMessagesAsync)));
        }

        private async Task SendMessagesAsync()
        {
            Debug.WriteLine("[Gelf4Net] Start Buffered Log Sender");
            var token = _cts.Token;
            while (!_cts.IsCancellationRequested)
            {
                var loggingEvent = _pendingTasks.Take(token);
                if (loggingEvent != null)
                {
                    await _sendFunc(loggingEvent);
                }
            }
            Debug.WriteLine("[Gelf4Net] Stop Buffered Log Sender");
        }

        public void QueueSend(string renderedLogLine)
        {
            _pendingTasks.Add(renderedLogLine);
        }

        public void Stop()
        {
            _cts.Cancel();
            Task.WaitAny(new[] { _sender }, TimeSpan.FromSeconds(10));
            Debug.WriteLine("Logging thread has stopped");
        }
    }
}
