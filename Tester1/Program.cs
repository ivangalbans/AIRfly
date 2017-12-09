using System;
using DHTChord.InitServices;
using DHTChord.Node;
using DHTChord.Server;

namespace Tester1
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length == 0) args = new string[1]{"3030"};
            if (args.Length == 2)
            {
                Console.WriteLine("Start auto");
                var node = ChordServer.Instance(ChordServer.FindServiceAddress()).LocalNode;
                StartServer.Start(int.Parse(args[0]), node);
            }
            else if (args.Length == 1)
                StartServer.Start(int.Parse(args[0]));
            else
            {
                StartServer.Start(int.Parse(args[0]), new ChordNode(args[1],int.Parse(args[2])));
            }
        }
    }
}
