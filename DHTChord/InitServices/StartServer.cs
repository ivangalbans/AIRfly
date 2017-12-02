using System;
using DHTChord.FTable;
using DHTChord.Server;
using DHTChord.Node;
using DHTChord.NodeInstance;

namespace DHTChord.InitServices
{
    public static class StartServer
    {
        public static void Start(int port, ChordNode seed = null)
        {
            ChordServer.LocalNode = new ChordNode(System.Net.Dns.GetHostName(), port);

            if (ChordServer.RegisterService(port))
            {

                var instance = ChordServer.LocalNode.GetNodeInstance();
                instance.Join(seed);

                while (true)
                {
                    switch (Char.ToUpperInvariant(Console.ReadKey(true).KeyChar))
                    {
                        case 'I':
                            {
                                PrintNodeInfo(instance, true);
                                break;
                            }
                        case 'X':
                            {
                                PrintNodeInfo(instance, true);
                                break;
                            }
                        case '?':
                            {
                                Console.WriteLine("Get Server [I]nfo, E[x]tended Info, [Q]uit, or Get Help[?]");
                                break;
                            }
                        case 'Q':
                            {
                                //instance.Depart();
                                return;
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
            ChordNode successor = instance.Successor;
            ChordNode predecessor = instance.Predecessor;
            FingerTable fingerTable = instance.FingerTable;
            ChordNode[] successorCache = instance.SuccessorCache;

            string successorString, predecessorString, successorCacheString, fingerTableString;
            if (successor != null)
            {
                successorString = successor.ToString();
            }
            else
            {
                successorString = "NULL";
            }

            if (predecessor != null)
            {
                predecessorString = predecessor.ToString();
            }
            else
            {
                predecessorString = "NULL";
            }

            successorCacheString = "SUCCESSOR CACHE:";
            for (int i = 0; i < successorCache.Length; i++)
            {
                successorCacheString += string.Format("\n\r{0}: ", i);
                if (successorCache[i] != null)
                {
                    successorCacheString += successorCache[i].ToString();
                }
                else
                {
                    successorCacheString += "NULL";
                }
            }

            fingerTableString = "FINGER TABLE:";
            for (int i = 0; i < fingerTable.Length; i++)
            {
                fingerTableString += string.Format("\n\r{0:x8}: ", fingerTable.StartValues[i]);
                if (fingerTable.Successors[i] != null)
                {
                    fingerTableString += fingerTable.Successors[i].ToString();
                }
                else
                {
                    fingerTableString += "NULL";
                }
            }

            Console.WriteLine("\n\rNODE INFORMATION:\n\rSuccessor: {1}\r\nLocal Node: {0}\r\nPredecessor: {2}\r\n", ChordServer.LocalNode, successorString, predecessorString);

            if (extended)
            {
                Console.WriteLine("\n\r" + successorCacheString);

                //Console.WriteLine("\n\r" + fingerTableString);

                foreach (var item in instance.SeedCache)
                {
                    Console.WriteLine(item);
                }
            }
        }
    }
}
