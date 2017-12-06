using System;

using DHTChord.InitServices;
using DHTChord.Node;

namespace Tester1
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length == 1)
                StartServer.Start(int.Parse(args[0]));
            else
            {
                StartServer.Start(int.Parse(args[0]), new ChordNode(args[1],int.Parse(args[2])));
            }
        }
    }
}
