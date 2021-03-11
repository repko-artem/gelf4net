using Gelf4Net.Util.TypeConverters;
using log4net.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gelf4Net.Appender
{
    /// <summary>
    /// Gelf Udp Appender
    /// </summary>
    public class GelfUdpAppender : log4net.Appender.UdpAppender
    {
        /// <summary>
        /// The MessageId used in Chunked mode.
        /// </summary>
        private static long ChunkMessageId;

        /// <summary>
        /// Gets or sets GrayLogServerHost.
        /// </summary>
        public string RemoteHostName { get; set; }
        public int RetryAfterMilliseconds { get; set; } = 5000;
        private TimeSpan _bootTimeoutTimeSpan;
        private bool _shouldWaitBootTimeout = false;
        private bool _isWatingBootTimeout = false;

        public GelfUdpAppender()
        {
            Encoding = Encoding.UTF8;
            MaxChunkSize = 1024;
            ChunkMessageId = DateTime.Now.Ticks % (2 ^ 16);
            log4net.Util.TypeConverters.ConverterRegistry.AddConverter(typeof(IPAddress), new IPAddressConverter());
        }

        public override void ActivateOptions()
        {
            if (RemoteAddress == null)
            {
                RemoteAddress = IPAddress.Parse(GetIpAddressFromHostName().Result);
            }

            base.ActivateOptions();

            _bootTimeoutTimeSpan = TimeSpan.FromMilliseconds(RetryAfterMilliseconds);
        }

        protected override void InitializeClientConnection()
        {
            base.InitializeClientConnection();
        }

        public int MaxChunkSize { get; set; }

        protected override void Append(LoggingEvent loggingEvent)
        {
            try
            {
                byte[] bytes = this.RenderLoggingEvent(loggingEvent).GzipMessage(this.Encoding);
                SendMessage(bytes);
            }
            catch (Exception ex)
            {
                this.ErrorHandler.Error("Unable to send logging event to remote host " + this.RemoteAddress + " on port " + this.RemotePort + ".", ex, ErrorCode.WriteFailure);
            }
        }

        protected void SendMessage(byte[][] loggingEvents)
        {
            foreach (var loggingEvent in loggingEvents)
            {
                SendMessage(loggingEvent);
            }
        }

        protected async Task<bool> SendMessageAsync(string logMessage)
        {
            return await SendMessage(logMessage.GzipMessage(Encoding)).ConfigureAwait(false);
        }

        protected Task<bool> SendMessage(byte[] payload)
        {
            return Task.Run(async () =>
            {
                try
                {
                    if (MaxChunkSize < payload.Length)
                    {
                        var chunkCount = payload.Length / MaxChunkSize;
                        if (payload.Length % MaxChunkSize != 0)
                            chunkCount++;

                        var messageId = GenerateMessageId();
                        var state = new UdpState() { SendClient = Client, Bytes = payload, ChunkCount = chunkCount, MessageId = messageId, SendIndex = 0 };
                        var messageChunkFull = GetMessageChunkFull(state.Bytes, state.MessageId, state.SendIndex, state.ChunkCount);

                        while (state.SendIndex < state.ChunkCount)
                        {
                            messageChunkFull = GetMessageChunkFull(state.Bytes, state.MessageId, state.SendIndex, state.ChunkCount);
                            await Client.SendAsync(messageChunkFull, messageChunkFull.Length, RemoteEndPoint).ConfigureAwait(false);
                            state.SendIndex++;
                        }
                    }
                    else
                    {
                        var state = new UdpState() { SendClient = Client, Bytes = payload, ChunkCount = 0, MessageId = null, SendIndex = 0 };
                        await Client.SendAsync(payload, payload.Length, RemoteEndPoint);
                    }
                    _shouldWaitBootTimeout = false;
                    return true;
                }
                catch (Exception ex)
                {
                    _shouldWaitBootTimeout = true;
                    this.ErrorHandler.Error("Unable to send logging event to remote host " + this.RemoteAddress + " on port " + this.RemotePort + ".", ex, ErrorCode.WriteFailure);
                    return false;
                }
            });
        }

        private byte[] GetMessageChunkFull(byte[] bytes, byte[] messageId, int i, int chunkCount)
        {
            var messageChunkPrefix = CreateChunkedMessagePart(messageId, i, chunkCount);
            var skip = i * MaxChunkSize;
            var messageChunkSuffix = bytes.Skip(skip).Take(MaxChunkSize).ToArray<byte>();

            var messageChunkFull = new byte[messageChunkPrefix.Length + messageChunkSuffix.Length];
            messageChunkPrefix.CopyTo(messageChunkFull, 0);
            messageChunkSuffix.CopyTo(messageChunkFull, messageChunkPrefix.Length);

            return messageChunkFull;
        }

        private class UdpState
        {
            public UdpClient SendClient { set; get; }
            public int ChunkCount { set; get; }
            public byte[] MessageId { set; get; }
            public int SendIndex { set; get; }
            public byte[] Bytes { set; get; }
        }

        private async Task<string> GetIpAddressFromHostName()
        {
            IPAddress[] addresslist = await Dns.GetHostAddressesAsync(RemoteHostName).ConfigureAwait(false);
            return addresslist[0].ToString();
        }

        public static byte[] GenerateMessageId()
        {
            var currentId = Interlocked.Increment(ref ChunkMessageId);
            return BitConverter.GetBytes(currentId);
        }

        public static byte[] CreateChunkedMessagePart(byte[] messageId, int index, int chunkCount)
        {
            var result = new List<byte>();
            var gelfHeader = new byte[2] { Convert.ToByte(30), Convert.ToByte(15) };
            result.AddRange(gelfHeader);
            result.AddRange(messageId);
            result.Add(Convert.ToByte(index));
            result.Add(Convert.ToByte(chunkCount));

            return result.ToArray<byte>();
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
