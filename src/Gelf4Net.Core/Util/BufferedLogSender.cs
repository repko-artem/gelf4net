using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net.Core;

namespace Gelf4Net.Util
{
    internal class BufferedLogSender
    {
        private readonly ConcurrentQueue<string> _pendingTasks;
        private readonly CancellationTokenSource _cts;
        private readonly Func<string, Task<bool>> _sendFunc;
        private readonly Func<Task> _funcWaitToRecover;
        private readonly Func<bool> _funcIsWaiting;
        private readonly Task _sender;
        private readonly int _bufferSize;
        static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        public BufferedLogSender(BufferedSenderOptions options, Func<string, Task<bool>> sendFunc) : this(options, sendFunc, null, null)
        {
        }

        public BufferedLogSender(BufferedSenderOptions options, Func<string, Task<bool>> sendFunc, Func<Task> funcWaitToRecover, Func<bool> funcIsWaiting)
        {
            _bufferSize = options.BufferSize ?? BufferedSenderOptions.DefaultBufferSize;
            if (_bufferSize <= 1)
            {
                _bufferSize = BufferedSenderOptions.DefaultBufferSize;
            }
            var numTasks = options.NumTasks ?? Environment.ProcessorCount;
            if (numTasks <= 1)
            {
                numTasks = Environment.ProcessorCount;
            }

            _pendingTasks = new ConcurrentQueue<string>();
            _cts = new CancellationTokenSource();
            _sendFunc = sendFunc;
            _funcWaitToRecover = funcWaitToRecover;
            _funcIsWaiting = funcIsWaiting;
            _sender = Task.WhenAll(Enumerable.Range(1, numTasks).Select(_ => Task.Run(SendMessagesAsync)));
        }

        private async Task SendMessagesAsync()
        {
            Debug.WriteLine("[Gelf4Net] Start Buffered Log Sender");
            var token = _cts.Token;
            while (!_cts.IsCancellationRequested)
            {
                if (_pendingTasks.Count > 0)
                {
                    if (_funcIsWaiting != null && _funcIsWaiting())
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                        continue;
                    }

                    if (_funcWaitToRecover != null)
                    {
                        await _funcWaitToRecover().ConfigureAwait(false);
                    }

                    await semaphoreSlim.WaitAsync();
                    try
                    {
                        if (_pendingTasks.TryPeek(out string payload))
                        {
                            Debug.WriteLine("[Gelf4Net] send" + payload);
                            var shouldDequeue = await _sendFunc(payload).ConfigureAwait(false);
                            if (shouldDequeue)
                            {
                                _pendingTasks.TryDequeue(out _);
                            }
                            else
                            {
                                if (_pendingTasks.Count > _bufferSize)
                                {
                                    _pendingTasks.TryDequeue(out _);
                                }
                            }
                        }
                    }
                    finally
                    {
                        semaphoreSlim.Release();
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                }
            }
            Debug.WriteLine("[Gelf4Net] Stop Buffered Log Sender");
        }

        public void QueueSend(string renderedLogLine)
        {
            _pendingTasks.Enqueue(renderedLogLine);
            if (_pendingTasks.Count > _bufferSize)
            {
                _pendingTasks.TryDequeue(out _);
            }
        }

        public void Stop()
        {
            _cts.Cancel();
            Task.WaitAny(new[] { _sender }, TimeSpan.FromSeconds(10));
            Debug.WriteLine("[Gelf4Net] Logging thread has stopped");
        }
    }
}
