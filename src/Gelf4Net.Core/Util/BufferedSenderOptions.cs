namespace Gelf4Net.Util
{
    internal class BufferedSenderOptions
    {
        /// <summary>
        /// Number of tasks to use for the async appender. 0 or fewer indicates one task per processor.
        /// </summary>
        public int? NumTasks { get; set; }

        /// <summary>
        /// Number of log lines to buffer for async send. Defaults to <see cref="DefaultBufferSize"/> if unset.
        /// </summary>
        public int? BufferSize { get; internal set; }

        /// <summary>
        /// Default Number of Items to Buffer for Async Appenders
        /// </summary>
        internal static int DefaultBufferSize = 10;
    }
}
