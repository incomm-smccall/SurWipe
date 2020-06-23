using NLog;
using System;
using System.Runtime.CompilerServices;

namespace SurStore.Utils
{
    public static class Logging
    {
        private static readonly Logger UsageLogger = LogManager.GetLogger("usage");
        private static readonly Logger ErrorLogger = LogManager.GetLogger("errors");
        private static readonly Logger GenLogger = LogManager.GetLogger("general");

        public static void LogUsage(string parameterData = null, [CallerMemberName] string functionName = "")
        {
            string parameterStuff = string.IsNullOrWhiteSpace(parameterData) ? "" : $", {parameterData}";
            string message = $"Function={functionName}{parameterStuff}";
            UsageLogger.Info(message);
            GenLogger.Debug($"Usage | {message}");
        }

        public static void LogMessage(LoggingLevel loggerLevel, string message, [CallerMemberName] string nameSpace = "")
        {
            GenLogger.Log(GetLogLevel(loggerLevel), $"[{nameSpace}]:{DateTime.Now}:{message}");
        }

        public static void LogError(LoggingLevel loggerLevel, string message, Exception ex = null, [CallerMemberName] string nameSpace = "")
        {
            LogLevel logLevel = GetLogLevel(loggerLevel);
            if (ex == null)
                ErrorLogger.Log(logLevel, $"[{nameSpace}]:{message}");
            else
                ErrorLogger.Log(logLevel, ex, $"[{nameSpace}]:{message}");
        }

        public static void LogRunTime(long runTime, [CallerMemberName] string functionName = "")
        {
            string runTimeMessage = $"Total Runtime for {functionName}: {runTime}";
            GenLogger.Log(LogLevel.Info, runTimeMessage);
        }

        public static void LogRunTime(TimeSpan runTime, string functionName = "")
        {
            string runTimeMessage = $"Total RunTime for {functionName}: {runTime.TotalMilliseconds}";
            GenLogger.Log(LogLevel.Info, runTimeMessage);
        }

        private static LogLevel GetLogLevel(LoggingLevel loggerLevel)
        {
            switch (loggerLevel)
            {
                case LoggingLevel.Info:
                    return LogLevel.Info;
                case LoggingLevel.RunTime:
                    return LogLevel.Info;
                case LoggingLevel.Warn:
                    return LogLevel.Warn;
                case LoggingLevel.Error:
                    return LogLevel.Error;
                case LoggingLevel.Fatal:
                    return LogLevel.Fatal;
                default:
                    return LogLevel.Info;
            }
        }
    }

    public enum LoggingLevel
    {
        Info,
        RunTime,
        Warn,
        Error,
        Fatal
    }
}
