using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Markup;
using DHTChord.Node;
using DHTChord.Server;
using DHTChord.FTable;
using static DHTChord.Node.ChordNode;
using static DHTChord.MathOperation.ChordMath;
using static DHTChord.Logger.Logger;

namespace DHTChord.NodeInstance
{
    public class ChordNodeInstance : MarshalByRefObject
    {
        public string Host => ChordServer.LocalNode.Host;

        public int Port => ChordServer.LocalNode.Port;

        public ulong Id => ChordServer.LocalNode.Id;

        public ChordNode SeedNode { get; set; }


        public ChordNode Successor
        {
            get => SuccessorCache[0];
            set
            {
                if (value == null &&  SuccessorCache[0] != null)
                {
                    Log( "Navigation", "Setting successor to null.");
                }
                else if (value != null &&
                         (SuccessorCache[0] == null || SuccessorCache[0].Id != value.Id))
                {
                    Log("Navigation", $"New Successor {value}.");
                }
                SuccessorCache[0] = value;
            }
        }



        private ChordNode _predecessorNode;
        public ChordNode Predecessor
        {
            get => _predecessorNode;
            set
            {
                if (value == null && null != _predecessorNode)
                {
                    Log("Navigation", "Setting predecessor to null.");
                }
                else if (value != null &&
                         (_predecessorNode== null || _predecessorNode.Id != value.Id))  
                {
                    Log("Navigation", $"New Predecessor {value}.");
                }
                _predecessorNode = value;
            }
        }
        public FingerTable FingerTable { get; set; }

        public ChordNode[] SuccessorCache { get; set; }

        //public ChordNode[] SeedCache { get; set; }

        public ChordNode FindClosestPrecedingFinger(ulong id)
        {
            for (var i = FingerTable.Length - 1; i >= 0; --i)
            {
                if (FingerTable.Successors[i] != null && FingerTable.Successors[i] != ChordServer.LocalNode)
                {
                    if (FingerInRange(FingerTable.Successors[i].Id, Id, id))
                    {
                        var nodeInstance = Instance(FingerTable.Successors[i]);
                        if (IsInstanceValid(nodeInstance))
                        {
                            return FingerTable.Successors[i];
                        }
                    }
                }
            }
            foreach (var t in SuccessorCache)
            {
                if (t != null && t != ChordServer.LocalNode)
                {
                    if (FingerInRange(t.Id, Id, id))
                    {
                        var instance =Instance(t);
                        if (IsInstanceValid(instance))
                        {
                            return t;
                        }
                    }
                }
            }


            return ChordServer.LocalNode;
        }

        public void GetSuccessorCache(ChordNode remoteNode)
        {
            var remoteSuccessorCache = ChordNode.GetSuccessorCache(remoteNode);
            if (remoteSuccessorCache != null)
            {
                SuccessorCache[0] = remoteNode;
                for (var i = 1; i < SuccessorCache.Length; i++)
                {
                    SuccessorCache[i] = remoteSuccessorCache[i - 1];
                }
            }
        }
        //public void GetSeedCache()
        //{
        //    for (int i = 1; i < SeedCache.Length; i++)
        //    {
        //        SeedCache[i] = FindSuccessor(ChordServer.GetHash(Random.Next() + Random.Next().ToString()));
        //    }
        //}

        public static Random Random = new Random(Environment.TickCount);


        public ChordNode FindSuccessor(ulong id)
        {
            if (IsIdInRange(id, Id, Successor.Id))
            {
                return Successor;
            }
            else
            {
                var predNode = FindClosestPrecedingFinger(id);
                return CallFindSuccessor(predNode,id);
            }
        }
        public static bool IsInstanceValid(ChordNodeInstance instance)
        {
            try
            {
                return instance.Port > 0 && instance.Successor != null;
            }
            catch (Exception e)
            {
                Log("Instance", $"Incoming instance was not valid: ({e.Message}).");
                return false;
            }
        }

        public bool Join(ChordNode seed)
        {
            SeedNode = seed;
            FingerTable = new FingerTable(ChordServer.LocalNode);
            
            SuccessorCache = new ChordNode[8];
           // SeedCache = new ChordNode[8];

            for (var i = 0; i < SuccessorCache.Length; i++)
            {
                SuccessorCache[i] = ChordServer.LocalNode;
            }

            if (seed != null)
            {
                Log("Navigation", $"Joining ring @ {seed.Host}:{seed.Port}");
                var nodeInstance = Instance(seed);
                if (IsInstanceValid(nodeInstance))
                {
                    try
                    {
                        Successor = nodeInstance.FindSuccessor(Id);
                        GetSuccessorCache(Successor);
                    }
                    catch (Exception e)
                    {
                        Log("Navigation", $"Error setting  Successor Node {e.Message}");
                        return false;
                    }
                }
                else
                {
                    Log("Navigation", "Invalid node seed");
                    return false;
                }
            }
            else
            {
                Log("Navigation", $"Sarting ring @ {Host}:{Port}");
            }

            StartMaintenance();

            return true;

        }

        private readonly BackgroundWorker _stabilizeSuccessors = new BackgroundWorker();
        private readonly BackgroundWorker _stabilizePredecessors = new BackgroundWorker();
        private readonly BackgroundWorker _updateFingerTable = new BackgroundWorker();

        public void StartMaintenance()
        {
            _stabilizeSuccessors.DoWork += StabilizeSuccessors;
            _stabilizeSuccessors.WorkerSupportsCancellation = true;
            _stabilizeSuccessors.RunWorkerAsync();

            _stabilizePredecessors.DoWork += StabilizePredecessors;
            _stabilizePredecessors.WorkerSupportsCancellation = true;
            _stabilizePredecessors.RunWorkerAsync();

            _updateFingerTable.DoWork += UpdateFingerTable;
            _updateFingerTable.WorkerSupportsCancellation = true;
            _updateFingerTable.RunWorkerAsync();
        }

        public void StopMaintenance()
        {
            _stabilizeSuccessors.CancelAsync();
            _stabilizePredecessors.CancelAsync();
            _updateFingerTable.CancelAsync();
        }

        private void StabilizePredecessors(object sender, DoWorkEventArgs ea)
        {
            var me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                if (Predecessor != null)
                {
                    try
                    {
                        var nodeInstance = Instance(Predecessor);
                        if (!IsInstanceValid(nodeInstance))
                        {
                            Predecessor = null;
                        }
                    }
                    catch (Exception e)
                    {
                        Log("StabilizePredecessors", $"StabilizePredecessors error: {e.Message}");
                        Predecessor = null;
                    }

                }

                Thread.Sleep(1000);
            }
        }

        private void StabilizeSuccessors(object sender, DoWorkEventArgs ea)
        {
            var me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                try
                {
                    var succPredNode = GetPredecessor(Successor);
                    if (succPredNode != null)
                    {
                        if (IsIdInRange(succPredNode.Id, Id, Successor.Id))
                        {
                            Successor = succPredNode;
                        }
                        CallNotify(Successor,ChordServer.LocalNode);

                        GetSuccessorCache(Successor);
                    }
                    else
                    {
                        var successorCacheHelped = false;
                        foreach (var entry in SuccessorCache)
                        {
                            var instance = Instance(entry);

                            if (IsInstanceValid(instance))
                            {

                                Successor = entry;
                                CallNotify(Successor, ChordServer.LocalNode);

                                GetSuccessorCache(Successor);

                                successorCacheHelped = true;
                                break;
                            }
                        }

                        if (!successorCacheHelped)
                        {
                            Console.WriteLine("***********\n************\n************");
                            Log("StabilizeSuccessors", "Ring consistency error, Re-Joining Chord ring.");

                            if (Join(SeedNode))
                            {
                                return;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                   Log("Maintenance", $"Error occured during StabilizeSuccessors ({e.Message})");
                }

                Thread.Sleep(1000);
            }
        }

        public void Notify(ChordNode callingNode)
        {

            if (Predecessor == null || IsIdInRange(callingNode.Id, Predecessor.Id, Id))
            {
                Predecessor = callingNode;
            }
        }

        private static int _currentTableInput;
        private void UpdateFingerTable(object sender, DoWorkEventArgs ea)
        {
            
            var me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                try
                {
                    try
                    {
                        FingerTable.Successors[_currentTableInput] = FindSuccessor(FingerTable.StartValues[_currentTableInput]);
                    }
                    catch (Exception e)
                    {
                        Log("Navigation", $"Unable to update Successor for start value {FingerTable.StartValues[_currentTableInput]} ({e.Message}).");
                    }

                    _currentTableInput = (_currentTableInput + 1) % FingerTable.Length;
                }
                catch (Exception e)
                {
                    Log("Maintenance", $"Error occured during UpdateFingerTable ({e.Message})");
                }

                Thread.Sleep(1000);
            }
        }

        public void Depart()
        {
            StopMaintenance();

            try
            {
                var instance = Instance(Successor);
                instance.Predecessor = Predecessor;

                instance = Instance(Predecessor);
                instance.Successor = Successor;
            }
            catch (Exception e)
            {
                Log( "Navigation", $"Error on Depart ({e.Message})." );
            }
            finally
            {
                SeedNode = ChordServer.LocalNode;
                Successor = ChordServer.LocalNode;
                Predecessor = ChordServer.LocalNode;
                FingerTable = new FingerTable(ChordServer.LocalNode);
                for (var i = 0; i < SuccessorCache.Length; i++)
                {
                    SuccessorCache[i] = ChordServer.LocalNode;
                }
            }
        }
    }
}
