using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using System;
using System.IO;

namespace LiveSplit.VAS
{
    public static class Log
    {
        private const string LogMessageTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] [VAS] {Message:lj}{NewLine}{Exception}";

        public static Logger Logger;

        public static event EventHandler<string> LogUpdated;

        private static readonly StringWriter LogHistory = new StringWriter();

        static Log()
        {
            var logFormatter = new MessageTemplateTextFormatter(LogMessageTemplate);
            var loggerConfiguration =
                new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .WriteTo.File(logFormatter, @"VASErrorLog.txt")
                    .WriteTo.Trace(logFormatter)
                    .WriteTo.TextWriter(LogHistory)
                    .WriteTo.Sink(new DebugSink(logFormatter));

            try
            {
                loggerConfiguration = loggerConfiguration.WriteTo.EventLog(logFormatter, "VideoAutoSplit", "Application", manageEventSource: true);
            }
            catch (Exception)
            {
                // Catch exception when event log could not be registered.
                // This will usually occur when not running as administrator.
            }

            Logger = loggerConfiguration.CreateLogger();
        }

        public static string ReadAll() => LogHistory.ToString();

        private class DebugSink : ILogEventSink
        {
            private readonly ITextFormatter _textFormatter;

            public DebugSink(ITextFormatter textFormatter)
            {
                _textFormatter = textFormatter ?? throw new ArgumentNullException(nameof(textFormatter));
            }

            public void Emit(LogEvent logEvent)
            {
                if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

                var logWriter = new StringWriter();
                _textFormatter.Format(logEvent, logWriter);
                var log = logWriter.ToString();

                LogUpdated?.Invoke(this, log);
            }
        }
    }
}
