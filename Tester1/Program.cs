using DHTChord.InitServices;
using DHTChord.Node;

namespace Tester1
{
    class Program
    {
        static void Main(string[] args)
        {
            args = new string[1] { "5050" };
            if(args.Length == 1)
                StartServer.Start(int.Parse(args[0]));
            else
            {
                StartServer.Start(int.Parse(args[0]), new ChordNode(args[1],int.Parse(args[2])));
            }
        }
    }
}
