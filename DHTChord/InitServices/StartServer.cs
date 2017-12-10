using System;
using System.IO;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Discovery;
using DHTChord.Node;
using DHTChord.NodeInstance;
using DHTChord.Server;
using static DHTChord.Logger.Logger;
using static DHTChord.InitServices.StartClient;

namespace DHTChord.InitServices
{
    public static class StartServer
    {
        public static void Start(int port, ChordNode seed = null)
        {
            ChordServer.LocalNode = new ChordNode(Dns.GetHostName(), port);
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
                            PrintNodeInfo(instance, false);
                            break;
                        }
                        case 'X':
                        {
                            PrintNodeInfo(instance, true);
                            break;
                        }

                        case 'Q':
                        {
                            instance.Depart();
                            return;
                        }

                        case 'A':
                        {
                                for(int i = 0; i < 10; ++i)
                                {
                                    string a = "";
                                    for (int j = 0; j < 100000; j++)
                                    {
                                        a += i;
                                    }
                                    ChordServer.CallAddValue(ChordServer.LocalNode, $"Hello Abel {a}");
                                    Log(LogLevel.Info, "Add New Value", "Adding the value");
                                }
                                break;
                        }

                        case 'V':
                        {
                            for (int i = 0; i < 10; ++i)
                            {
                                ChordServer.CallAddValue(ChordServer.LocalNode, $"Hello Ivan {i}");
                                Log(LogLevel.Info, "Add New Value", "Adding the value");
                            }
                            break;
                        }

                        case 'R':
                        {
                            for (int i = 0; i < 10; ++i)
                            {
                                ChordServer.CallAddValue(ChordServer.LocalNode, $"Hello Raydel {i}");
                                Log(LogLevel.Info, "Add New Value", "Adding the value");
                            }
                            break;
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
                            string path =
                                "G:\\!!from adriano\\music from\\Imagine Dragons\\Discos\\[2013] Night Visions/";
                            var directorys = Directory.EnumerateFiles(path);
                            foreach (var file in directorys)
                            {
                                ChordServer.AddFile(Path.GetFileName(file), path, ChordServer.LocalNode);
                            }
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

        static void PrintNodeInfo(ChordNodeInstanceClient instance, bool extended)
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
            Console.WriteLine($"\nSUCCESSOR CACHE:");

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
