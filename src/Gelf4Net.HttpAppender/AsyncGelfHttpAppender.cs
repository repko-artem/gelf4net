using log4net.Core;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gelf4Net.Appender
{
    public class AsyncGelfHttpAppender : GelfHttpAppender
    {
        private readonly BlockingCollection<string> _pendingTasks;
        private readonly CancellationTokenSource _cts;
        private readonly Task _sender;

        public AsyncGelfHttpAppender()
        {
            _pendingTasks = new BlockingCollection<string>(100);
            _cts = new CancellationTokenSource();
            // FIXME: use the machine's core count?
            _sender = Task.WhenAll(Enumerable.Range(1, 4).Select(_ => Task.Run(SendMessagesAsync)));
        }

        protected override void Append(LoggingEvent[] loggingEvents)
        {
            foreach (var loggingEvent in loggingEvents)
            {
                Append(loggingEvent);
            }
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (FilterEvent(loggingEvent))
            {
                _pendingTasks.Add(RenderLoggingEvent(loggingEvent));
            }
        }

        private async Task SendMessagesAsync()
        {
            var token = _cts.Token;
            while (!_cts.IsCancellationRequested)
            {
                var loggingEvent = _pendingTasks.Take(token);
                if (loggingEvent != null)
                {
                    await base.SendMessageAsync(loggingEvent);
                }
            }
        }

        protected override void OnClose()
        {
            Debug.WriteLine("Closing Async Appender");
            _cts.Cancel();
            Task.WaitAny(new[] { _sender }, TimeSpan.FromSeconds(10));
            Debug.WriteLine("Logging thread has stopped");
            base.OnClose();
        }
    }
}
