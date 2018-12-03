using Gelf4Net.Util;
using log4net.Core;
using System.Diagnostics;

namespace Gelf4Net.Appender
{
    public class AsyncGelfHttpAppender : GelfHttpAppender
    {
        private readonly BufferedLogSender _sender;
        public int Threads { get; set; }
        public int BufferSize { get; set; }

        public AsyncGelfHttpAppender()
        {
            var options = new BufferedSenderOptions
            {
                BufferSize = BufferSize,
                NumTasks = Threads,
            };
            _sender = new BufferedLogSender(options, SendMessageAsync);
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
                _sender.QueueSend(RenderLoggingEvent(loggingEvent));
            }
        }

        protected override void OnClose()
        {
            Debug.WriteLine("Closing Async Appender");
            _sender.Stop();
            base.OnClose();
        }
    }
}
