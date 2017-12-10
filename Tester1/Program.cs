using System;
using DHTChord.InitServices;
using DHTChord.Node;
using DHTChord.Server;
using System.IO;
using static System.IO.Path;

namespace Tester1
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0) args = new string[] { "Client", "Send", "D:\\toSend/", "localhost", "5050" };
            if(args[0] == "Server")
            {
                if(args.Length == 3)//seed Node
                {
                    int port = int.Parse(args[1]);
                    string Path = args[2];

                    StartServer.Start(port,Path);
                }
                else//join to existing ring
                {
                    int port1 = int.Parse(args[1]);
                    string Host = args[2];
                    int portHost = int.Parse(args[3]);
                    string Path = args[4];

                    StartServer.Start(port1, Path, new ChordNode(Host, portHost));
                }
            }
            if(args[0] == "Client")
            {
                if(args[1] == "Send")
                {
                    string Path = args[2];
                    string Host = args[3];
                    int portHost = int.Parse(args[4]);

                    
                    var directorys = Directory.EnumerateFiles(Path);
                    ChordServer.LocalNode = new ChordNode(Host, portHost);
                    foreach (var file in directorys)
                    {                        
                        ChordServer.AddFile(GetFileName(file), Path, ChordServer.LocalNode);
                    }
                }
            }

            //if(args.Length == 0) args = new string[1]{"3030"};
            //if (args.Length == 2)
            //{
            //    Console.WriteLine("Start auto");
            //    var node = ChordServer.Instance(ChordServer.FindServiceAddress()).LocalNode;
            //    StartServer.Start(int.Parse(args[0]), node);
            //}
            //else if (args.Length == 1)
            //    StartServer.Start(int.Parse(args[0]));
            //else
            //{
            //    StartServer.Start(int.Parse(args[0]), new ChordNode(args[1],int.Parse(args[2])));
            //}
        }
    }
}
