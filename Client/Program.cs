using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DHTChord.NodeInstance;
using System.IO;
using System.Net;
using DHTChord.Node;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0) args = new string[] { "Send", "localhost", "7777" };
            if(args[0] == "Send")
            {
                string host = Dns.GetHostEntry(args[1]).HostName;
                int port = int.Parse(args[2]);
                var node = new ChordNode(host, port);
                //TODO: node = discovery
            
                string[] input = Console.ReadLine().Split(' ');
                if(input[0] == "1")//all directory
                {
                    string path = input[1];
                    var files = Directory.EnumerateFiles(path);

                    foreach (var f in files)
                    {
                        string fileName = Path.GetFileName(f);

                        ClientSide.Send(fileName, f, node);
                    }
                }
                if(input[0] == "2")//a single file
                {
                    string path = input[1];
                    string fileName = Path.GetFileName(path);

                    ClientSide.Send(fileName, path, node);
                }
            }
            if(args[0] == "Find")
            {
                //string host = Dns.GetHostEntry(args[1]).HostName;
                //int port = int.Parse(args[2]);
                //var node = new ChordNode(host, port);
                ////TODO: node = discovery

                //string fileName = args[3];

                //if(ClientSide.Find(fileName, node))
                //{
                //    Console.WriteLine($"Find Succefully {fileName}");
                //}
                //else
                //{
                //    Console.WriteLine($"{fileName} not found");
                //}
            }
        }
    }
}
