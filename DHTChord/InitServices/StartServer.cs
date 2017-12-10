using System;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Discovery;
using DHTChord.Node;
using DHTChord.NodeInstance;
using DHTChord.Server;
using static DHTChord.Logger.Logger;

namespace DHTChord.InitServices
{
    public static class StartServer
    {
        public static void Start(int port, string path, ChordNode seed = null)
        {
            ChordServer.LocalNode = new ChordNode(Dns.GetHostName(), port) {Path = path};
            Uri baseAddress = new Uri($"net.tcp://{ChordServer.LocalNode.Host}:{port}/chord");
            using (ServiceHost serviceHost = new ServiceHost(typeof(ChordNodeInstance), baseAddress))
            {
                serviceHost.AddServiceEndpoint(typeof(IChordNodeInstance), ChordServer.CreategBinding(), baseAddress);
                serviceHost.Description.Behaviors.Add(new ServiceDiscoveryBehavior());
                serviceHost.AddServiceEndpoint(new UdpDiscoveryEndpoint());
                serviceHost.Open();


                var instance = ChordServer.Instance(ChordServer.LocalNode);                
                instance.Join(seed);

                while (true)
                {
                    switch (char.ToUpperInvariant(Console.ReadKey(true).KeyChar))
                    {
                        case 'I':
                        {
                            PrintNodeInfo(instance);
                            break;
                        }
                        case 'X':
                        {
                            PrintNodeInfo(instance);
                            break;
                        }

                        case 'Q':
                        {
                            instance.Depart();
                            return;
                        }

                        case 'F':
                        {
                                for (int i = 0; i < 10; ++i)
                                {
                                    var val = ChordServer.CallGetValue(ChordServer.LocalNode, ChordServer.GetHash($"Hello Wolrd {i}"), out var tmp);
                                    if(val == null)
                                    {
                                        Log(LogLevel.Error, "GetValue", $"The instance is null. Value is not found: Hello Wolrd {i}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Download from {tmp}: {val}");
                                    }
                                }
                                break;
                        }

                        case 'D':
                        {
                                instance.ViewDataBase();
                                break;
                        }
                        case 'M':
                        {
                            //string path =
                            //    "G:\\!!from adriano\\music from\\Imagine Dragons\\Discos\\[2013] Night Visions/";
                            //var directorys = Directory.EnumerateFiles(path);
                            //foreach (var file in directorys)
                            //{
                            //    ChordServer.AddFile(GetFileName(file), path, ChordServer.LocalNode);
                            //}
                            break;
                        }

                        default:
                        {
                            Console.WriteLine("Get Server [I]nfo, E[x]tended Info, [Q]uit, or Get Help[?]");
                            break;
                        }
                    }
                }
            }
        }

        static void PrintNodeInfo(ChordNodeInstanceClient instance)
        {
            var successor = instance.Successor;
            var predecessor = instance.Predecessor;
            //var fingerTable = instance.FingerTable;
            var successorCache = instance.SuccessorCache;
            var port = instance.Port;
            var host = instance.Host;
            //var seed = instance.SeedNode;


            Console.WriteLine($"\nNODE INFORMATION: HOST: {host}   PORT {port}");
            Console.WriteLine($"Predecessor: {predecessor?.ToString() ?? "NULL"}");
            Console.WriteLine($"LocalNode: {ChordServer.LocalNode?.ToString() ?? "NULL"}");
            Console.WriteLine($"Successor: {successor?.ToString() ?? "NULL"}");
            //Console.WriteLine($"Seed: {seed?.ToString() ?? "NULL"}");
            Console.WriteLine("\nSUCCESSOR CACHE:");

            for (var i = 0; i < successorCache.Length; i++)
                Console.WriteLine($"{i}: {successorCache[i]?.ToString()??"NULL"} ");

            //if (extended)
            //{
            //    Console.WriteLine($"\nFINGERTABLE:");

            //    for (var i = 0; i < fingerTable.Length; i++)
            //        Console.WriteLine($"{i}: {fingerTable.Successors[i]?.ToString() ?? "NULL"} ");
            //}
        }
    }
}
