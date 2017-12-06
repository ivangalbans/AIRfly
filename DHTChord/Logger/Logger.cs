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
        public static void Log(LogLevel logLevel, string logArea, string message)
        {
            if(logLevel != LogLevel.Debug)
                Console.WriteLine($"{DateTime.Now} {ChordServer.LocalNode} {logArea} {message}" );
        }
    }
}
