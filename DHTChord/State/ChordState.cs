using System;
using System.ComponentModel;
using System.Threading;
using DHTChord.Node;
using DHTChord.Server;
using static DHTChord.MathOperation.ChordMath;
using static DHTChord.Logger.Logger;

namespace DHTChord.State
{
    public class ChordState : MarshalByRefObject
    {
        private ChordNode _seedNode;
        public ChordNode Successor { get; set; }
        public ChordNode Predecessor { get; set; }
        public FingerTable.FingerTable FingerTable { get; set; }


        private ChordNode FindClosestPrecedingFinger(ulong id)
        {
            for (int i = FingerTable.Length - 1; i >= 0; i--)
            {
                // if the finger is more closely between the local node and id and that finger corresponds to a valid node, return the finger
                if (FingerTable.Successors[i] != null && FingerTable.Successors[i] != ChordServer.LocalNode)
                {
                    if (FingerInRange(FingerTable.Successors[i].Id, ChordServer.LocalNode.Id, id))
                    {
                        var instance = FingerTable.Successors[i].GetState();
                        if (instance.IsStateValid())
                        {
                            return FingerTable.Successors[i];
                        }
                    }
                }
            }


            /*
             * TODO: CACHE
             * */


            return ChordServer.LocalNode;
        }

        private ChordNode FindPredecessor(ulong id)
        {
            var currentNode =  ChordServer.LocalNode;
            var currentState = currentNode.GetState();
            while (!IsIdInRange(id, currentNode.Id, currentState.Successor.Id))
            {
                currentNode = currentState.FindClosestPrecedingFinger(id);
                currentState = currentNode.GetState();
            }
            return currentNode;
        }

        public ChordNode FindSuccessor(ulong id)
        {
            return FindPredecessor(id).GetSuccessor();
        }

        private bool IsStateValid()
        {
            try
            {
                if (ChordServer.LocalNode.Port > 0 && Successor != null)
                    return true;
            }
            catch (Exception e)
            {
                Log("Incoming instance was not valid", e.Message);
                throw;
            }
            return false;
        }

        public bool Join(ChordNode seed)
        {
            //ChordServer.LocalNode = new ChordNode(host, port);
            _seedNode = seed;

            FingerTable = new FingerTable.FingerTable(ChordServer.LocalNode);

            Successor = ChordServer.LocalNode;
            //TODO: Cache

            if (seed != null)
            {
                Log("Navigation", $"Joining ring @ {seed.Host}:{seed.Port}");
                var state = seed.GetState();
                if (state.IsStateValid())
                {
                    try
                    {
                        Successor = state.FindSuccessor(ChordServer.LocalNode.Id);
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
                Log("Navigation", $"Sarting ring @ {ChordServer.LocalNode.Host}:{ChordServer.LocalNode.Port}");
            }

            StartMaintenance();

            return true;

        }

        private readonly BackgroundWorker _stabilizeSuccessors = new BackgroundWorker();
        private readonly BackgroundWorker _stabilizePredecessors = new BackgroundWorker();
        private readonly BackgroundWorker _updateFingerTable = new BackgroundWorker();
        private readonly BackgroundWorker _rejoin = new BackgroundWorker();

        private void StartMaintenance()
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

            _rejoin.DoWork += ReJoin;
            _rejoin.WorkerSupportsCancellation = true;
            _rejoin.RunWorkerAsync();
        }

        private void StopMaintenance()
        {
            _stabilizeSuccessors.CancelAsync();
            _stabilizePredecessors.CancelAsync();
            _updateFingerTable.CancelAsync();
            _rejoin.CancelAsync();
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
                        var state = Predecessor.GetState();
                        if (!state.IsStateValid())
                        {
                            Predecessor = null;
                        }
                    }
                    catch (Exception e)
                    {
                        Log("StabilizePredecessors", $"StabilizePredecessors error: {e.Message}");
                        Predecessor = null;
                        throw;
                    }

                }

                Thread.Sleep(5000);
            }
        }

        private void StabilizeSuccessors(object sender, DoWorkEventArgs ea)//mierda
        {
            var me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                try
                {

                    var succPredNode = Successor.GetPredecessor();
                    if (succPredNode != null)
                    {
                        if (IsIdInRange(succPredNode.Id, ChordServer.LocalNode.Id, Successor.Id))
                        {
                            Successor = succPredNode;
                        }
                        Successor.CallNotify(ChordServer.LocalNode);
                        //GetSuccessorCache(this.Successor);
                    }
                }
                catch (Exception)
                {
                    //ChordServer.Log(LogLevel.Error, "Maintenance", "Error occured during StabilizeSuccessors ({0})", e.Message);
                }

                // TODO: this could be tweaked and/or made configurable elsewhere or passed in as arguments
                Thread.Sleep(5000);
            }
        }

        public void Notify(ChordNode callingNode)
        {
            if (Predecessor == null || IsIdInRange(callingNode.Id, Predecessor.Id, ChordServer.LocalNode.Id))
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

                // TODO: make this configurable via config file or passed in as an argument
                Thread.Sleep(1000);
            }
        }

        private bool _hasReJoinRun;
        private void ReJoin(object sender, DoWorkEventArgs ea)
        {
            var me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                try
                {
                    if (_hasReJoinRun)
                    {
                        // first find the successor for the seed node
                        if (_seedNode != null)
                        {
                            var seedSuccessor = FindSuccessor(_seedNode.Id);

                            // if the successor is not equal to the seed node, something is fishy
                            if (seedSuccessor.Id != _seedNode.Id)
                            {
                                // if the seed node is still active, re-join the ring to the seed node
                                var state = _seedNode.GetState();
                                if (state.IsStateValid())
                                {
                                    Log( "ReJoin", $"Unable to contact initial seed node {_seedNode}.  Re-Joining...");
                                    Join(_seedNode);
                                }

                                // otherwise, in the future, there will be a cache of seed nodes to check/join from...
                                // as it may be the case that the seed node simply has disconnected from the network.
                            }
                        }
                    }
                    else
                    {
                       
                        _hasReJoinRun = true;
                    }
                }
                catch (Exception e)
                {
                    Log( "Maintenance", $"Error occured during ReJoin ({e.Message})");
                }

                Thread.Sleep(30000);
            }
        }

        public void Depart()
        {
            StopMaintenance();

            try
            {
                var state = Successor.GetState();
                state.Predecessor = Predecessor;

                state = Predecessor.GetState();
                state.Successor = Successor;
            }
            catch (Exception e)
            {
                Log("Navigation", $"Error on Depart {e.Message}");

            }
            finally
            {
                Successor = ChordServer.LocalNode;
                Predecessor = ChordServer.LocalNode;
                FingerTable = new FingerTable.FingerTable(ChordServer.LocalNode);
                //TODO: Successor Cache
            }
        }
    }
}
