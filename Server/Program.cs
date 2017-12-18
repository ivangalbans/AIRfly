using DHTChord.InitServices;
using DHTChord.Server;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0) args = new string[] { "3030", "D:\\A" };
            if (args.Length == 3)
            {
                System.Console.WriteLine("Start auto");
                var tmp = ChordServer.FindServiceAddress();
                var node = ChordServer.Instance(tmp[0]).LocalNode;
                StartServer.Start(int.Parse(args[0]), args[1], node);
            }
            else if (args.Length == 2)
            {
                StartServer.Start(int.Parse(args[0]), args[1]);
            }
        }
    }
}
