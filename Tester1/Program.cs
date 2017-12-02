using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DHTChord.InitServices;

namespace Tester1
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length == 1)
            {
                StartServer.Start(int.Parse(args[0]));
            }
            else
            {
                StartServer.Start(int.Parse(args[0]), new DHTChord.Node.ChordNode(args[1],int.Parse(args[2])));
            }
        }
    }
}
