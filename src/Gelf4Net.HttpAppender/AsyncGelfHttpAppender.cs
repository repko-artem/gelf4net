using Gelf4Net.Util;
using log4net.Core;
using System.Diagnostics;

namespace Gelf4Net.Appender
{
    public class AsyncGelfHttpAppender : GelfHttpAppender
    {
        private readonly BufferedLogSender _sender;

        public AsyncGelfHttpAppender()
        {
            _sender = new BufferedLogSender(SendMessageAsync);
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
