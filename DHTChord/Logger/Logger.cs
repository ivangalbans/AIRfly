using System;
using DHTChord.Server;

namespace DHTChord.Logger
{
    public static class Logger
    {
        public static void Log(string message, string details)
        {
            Console.WriteLine($"{DateTime.Now} {ChordServer.LocalNode} {message} {details}" );
        }
    }
}
