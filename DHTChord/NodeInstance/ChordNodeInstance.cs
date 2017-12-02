using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;

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
            set => SuccessorCache[0] = value;
        }
        public ChordNode Predecessor { get; set; }
        public FingerTable FingerTable { get; set; }

        public ChordNode[] SuccessorCache { get; set; }

        public ChordNode FindClosestPrecedingFinger(ulong id)
        {
            for (int i = FingerTable.Length - 1; i >= 0; --i)
            {
                if (FingerTable.Successors[i] != null && FingerTable.Successors[i] != ChordServer.LocalNode)
                {
                    if (FingerInRange(FingerTable.Successors[i].Id, Id, id))
                    {
                        ChordNodeInstance nodeInstance = Instance(FingerTable.Successors[i]);
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
                for (int i = 1; i < SuccessorCache.Length; i++)
                {
                    SuccessorCache[i] = remoteSuccessorCache[i - 1];
                }
            }
        }

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

            for (int i = 0; i < SuccessorCache.Length; i++)
            {
                SuccessorCache[i] = ChordServer.LocalNode;
            }

            if (seed != null)
            {
                Log("Navigation", $"Joining ring @ {seed.Host}:{seed.Port}");
                ChordNodeInstance nodeInstance = Instance(seed);
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

        public void StabilizePredecessors(object sender, DoWorkEventArgs ea)
        {
            BackgroundWorker me = (BackgroundWorker)sender;

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

        public void StabilizeSuccessors(object sender, DoWorkEventArgs ea)
        {
            BackgroundWorker me = (BackgroundWorker)sender;

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
                        bool successorCacheHelped = false;
                        foreach (ChordNode entry in SuccessorCache)
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
        public void UpdateFingerTable(object sender, DoWorkEventArgs ea)
        {
            
            BackgroundWorker me = (BackgroundWorker)sender;

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
                for (int i = 0; i < SuccessorCache.Length; i++)
                {
                    SuccessorCache[i] = ChordServer.LocalNode;
                }
            }
        }
    }
}
