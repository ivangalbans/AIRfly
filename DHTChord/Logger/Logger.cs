using System;
using DHTChord.Server;

namespace DHTChord.Logger
{
    public static class Logger
    {
        /// <summary>
        /// The logging level to use for a given message / log.
        /// </summary>
        public enum LogLevel
        {
            Error,
            Info,
            Warn,
            Debug
        }

        /// <summary>
        /// Log a message to the Chord logging facility.
        /// </summary>
        /// <param name="logLevel">The log level.</param>
        /// <param name="logArea">The functional source area of the log message.</param>
        /// <param name="message">The message to log.</param>
        static object obj;
        public static void Log(LogLevel logLevel, string logArea, string message)
        {
            
            ConsoleColor originColor = Console.ForegroundColor;
            ConsoleColor color = ConsoleColor.Black;
            if(logLevel != LogLevel.Debug)
            {
                if(logLevel == LogLevel.Error)
                {
                    color = ConsoleColor.Red;
                }
                else if(logLevel == LogLevel.Info)
                {
                    color = ConsoleColor.Green;
                }
                else if(logLevel == LogLevel.Warn)
                {
                    color = ConsoleColor.Yellow;
                }

                lock(obj)
                {
                    Console.Write($"{DateTime.Now} {ChordServer.LocalNode}");
                    Console.ForegroundColor = color;
                    Console.Write($" {logArea}: ");
                    Console.ForegroundColor = originColor;
                    Console.WriteLine($"{message}");
                }
            }
        }
    }
}
