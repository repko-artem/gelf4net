using log4net.Appender;
using log4net.Core;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gelf4Net.Appender
{
    public class GelfHttpAppender : AppenderSkeleton
    {
        private readonly HttpClient _httpClient;
        private TimeSpan _bootTimeoutTimeSpan;
        private bool _shouldWaitBootTimeout = false;
        private bool _isWatingBootTimeout = false;
        private Uri _baseUrl;
        public string Url { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public int RetryAfterMilliseconds { get; set; } = 5000;
        public int HttpClientTimeoutMilliseconds { get; set; } = 5000;

        public GelfHttpAppender()
        {
            _httpClient = new HttpClient();
        }

        public override void ActivateOptions()
        {
            base.ActivateOptions();

            _baseUrl = new Uri(Url);

            _httpClient.DefaultRequestHeaders.ExpectContinue = false;
            _httpClient.Timeout = TimeSpan.FromMilliseconds(HttpClientTimeoutMilliseconds);

            if (!string.IsNullOrEmpty(User) && !string.IsNullOrEmpty(Password))
            {
                var byteArray = Encoding.ASCII.GetBytes(User + ":" + Password);
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            }

            _bootTimeoutTimeSpan = TimeSpan.FromMilliseconds(RetryAfterMilliseconds);
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            var payload = RenderLoggingEvent(loggingEvent);
            var _ = SendMessageAsync(payload);
        }

        protected async Task<bool> SendMessageAsync(string payload)
        {
            try
            {
                using (var content = new ByteArrayContent(payload.GzipMessage(Encoding.UTF8)))
                {
                    content.Headers.ContentEncoding.Add("gzip");
                    await _httpClient.PostAsync(_baseUrl, content).ConfigureAwait(false);
                    _shouldWaitBootTimeout = false;
                    return true;
                }
            }
            catch (Exception e)
            {
                _shouldWaitBootTimeout = true;
                ErrorHandler.Error("Unable to send logging event to remote host " + Url, e);
                return false;
            }
        }

        protected async Task WaitToSendOnConnectionAsync()
        {
            if (_shouldWaitBootTimeout)
            {
                _isWatingBootTimeout = true;
                await Task.Delay(_bootTimeoutTimeSpan);
                _shouldWaitBootTimeout = false;
                _isWatingBootTimeout = false;
            }
        }

        protected bool IsWaiting() => _isWatingBootTimeout;
    }
}
