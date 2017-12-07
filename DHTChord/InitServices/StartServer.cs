using System;

using DHTChord.FTable;
using DHTChord.Server;
using DHTChord.Node;
using DHTChord.NodeInstance;

using static DHTChord.Logger.Logger;

namespace DHTChord.InitServices
{
    public static class StartServer
    {
        public static void Start(int port, ChordNode seed = null)
        {
            ChordServer.LocalNode = new ChordNode(System.Net.Dns.GetHostName(), port);

            if (ChordServer.RegisterService(port))
            {

                var instance = ChordNode.Instance(ChordServer.LocalNode);
                instance.Join(seed);

                while (true)
                {
                    switch (Char.ToUpperInvariant(Console.ReadKey(true).KeyChar))
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
                                    ChordServer.CallAddValue(ChordServer.LocalNode, $"Hello Abel {i}");
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

                        default:
                        {
                            Console.WriteLine("Get Server [I]nfo, E[x]tended Info, [Q]uit, or Get Help[?]");
                            break;
                        }
                    }
                }
            }
        }

        static void PrintNodeInfo(ChordNodeInstance instance, bool extended)
        {
            var successor = instance.Successor;
            var predecessor = instance.Predecessor;
            var fingerTable = instance.FingerTable;
            var successorCache = instance.SuccessorCache;
            var port = instance.Port;
            var host = instance.Host;
            var seed = instance.SeedNode;


            Console.WriteLine($"\nNODE INFORMATION: HOST: {host}   PORT {port}");
            Console.WriteLine($"Predecessor: {predecessor?.ToString() ?? "NULL"}");
            Console.WriteLine($"LocalNode: {ChordServer.LocalNode?.ToString() ?? "NULL"}");
            Console.WriteLine($"Successor: {successor?.ToString() ?? "NULL"}");
            Console.WriteLine($"Seed: {seed?.ToString() ?? "NULL"}");
            Console.WriteLine($"\nSUCCESSOR CACHE:");

            for (var i = 0; i < successorCache.Length; i++)
                Console.WriteLine($"{i}: {successorCache[i]?.ToString()??"NULL"} ");

            if (extended)
            {
                Console.WriteLine($"\nFINGERTABLE:");

                for (var i = 0; i < fingerTable.Length; i++)
                    Console.WriteLine($"{i}: {fingerTable.Successors[i]?.ToString() ?? "NULL"} ");
            }
        }
    }
}
