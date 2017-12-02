using System;
using System.ComponentModel;
using System.Threading;

using DHTChord.Node;
using DHTChord.Server;
using static DHTChord.MathOperation.ChordMath;
using static DHTChord.Logger.Logger;
using DHTChord.FTable;

namespace DHTChord.NodeInstance
{
    public class ChordNodeInstance : MarshalByRefObject
    {
        private ChordNode SeedNode = null;
        public ChordNode Successor { get; set; }
        public ChordNode Predecessor { get; set; }
        public FingerTable FingerTable { get; set; }

        private ChordNode FindClosestPrecedingFinger(ulong id)
        {
            for (int i = FingerTable.Length - 1; i >= 0; --i)
            {
                // if the finger is more closely between the local node and id and that finger corresponds to a valid node, return the finger
                if (this.FingerTable.Successors[i] != null && this.FingerTable.Successors[i] != ChordServer.LocalNode)
                {
                    if (FingerInRange(FingerTable.Successors[i].ID, ChordServer.LocalNode.ID, id))
                    {
                        ChordNodeInstance nodeInstance = FingerTable.Successors[i].GetState();
                        if (nodeInstance.IsStateValid())
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

        /// <summary>
        /// Find the node whose ID has the greater value that is less or equal than id
        /// </summary>
        /// <param name="id">ID to search the corresponding node</param>
        /// <returns>A ChordNode that is the responsable of the id</returns>
        private ChordNode FindPredecessor(ulong id)
        {
            var currentNode = ChordServer.LocalNode;
            var currentNodeInstance = currentNode.GetState();
            while (!IsIdInRange(id, currentNode.ID, currentNodeInstance.Successor.ID))
            {
                currentNode = currentNodeInstance.FindClosestPrecedingFinger(id);
                currentNodeInstance = currentNode.GetState();
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
                throw e;
            }
            return false;
        }

        public bool Join(ChordNode seed)
        {
            SeedNode = seed;
            FingerTable = new FingerTable(ChordServer.LocalNode);
            Successor = ChordServer.LocalNode;

            //TODO: Cache

            if (seed != null)
            {
                Log("Navigation", $"Joining ring @ {seed.Host}:{seed.Port}");
                ChordNodeInstance nodeInstance = seed.GetState();
                if (nodeInstance.IsStateValid())
                {
                    try
                    {
                        Successor = nodeInstance.FindSuccessor(ChordServer.LocalNode.ID);
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
                Log("Navigation", $"Sarting ring @ {seed.Host}:{seed.Port}");
            }

            StartMaintenance();

            return true;

        }

        private BackgroundWorker m_StabilizeSuccessors = new BackgroundWorker();
        private BackgroundWorker m_StabilizePredecessors = new BackgroundWorker();
        private BackgroundWorker m_UpdateFingerTable = new BackgroundWorker();
        private BackgroundWorker m_Rejoin = new BackgroundWorker();

        private void StartMaintenance()
        {
            m_StabilizeSuccessors.DoWork += new DoWorkEventHandler(StabilizeSuccessors);
            m_StabilizeSuccessors.WorkerSupportsCancellation = true;
            m_StabilizeSuccessors.RunWorkerAsync();

            m_StabilizePredecessors.DoWork += new DoWorkEventHandler(StabilizePredecessors);
            m_StabilizePredecessors.WorkerSupportsCancellation = true;
            m_StabilizePredecessors.RunWorkerAsync();

            m_UpdateFingerTable.DoWork += new DoWorkEventHandler(UpdateFingerTable);
            m_UpdateFingerTable.WorkerSupportsCancellation = true;
            m_UpdateFingerTable.RunWorkerAsync();

            m_Rejoin.DoWork += new DoWorkEventHandler(ReJoin);
            m_Rejoin.WorkerSupportsCancellation = true;
            m_Rejoin.RunWorkerAsync();
        }

        private void StopMaintenance()
        {
            m_StabilizeSuccessors.CancelAsync();
            m_StabilizePredecessors.CancelAsync();
            m_UpdateFingerTable.CancelAsync();
            m_Rejoin.CancelAsync();
        }

        private void StabilizePredecessors(object sender, DoWorkEventArgs ea)
        {
            BackgroundWorker me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                if (Predecessor != null)
                {
                    try
                    {
                        ChordNodeInstance nodeInstance = Predecessor.GetState();
                        if (!nodeInstance.IsStateValid())
                        {
                            Predecessor = null;
                        }
                    }
                    catch (Exception e)
                    {
                        Log("StabilizePredecessors", $"StabilizePredecessors error: {e.Message}");
                        Predecessor = null;
                        throw e;
                    }

                }

                Thread.Sleep(5000);
            }
        }

        private void StabilizeSuccessors(object sender, DoWorkEventArgs ea)//mierda
        {
            BackgroundWorker me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                try
                {

                    ChordNode succPredNode = Successor.GetPredecessor();
                    if (succPredNode != null)
                    {
                        if (IsIdInRange(succPredNode.ID, ChordServer.LocalNode.ID, Successor.ID))
                        {
                            Successor = succPredNode;
                        }
                        Successor.CallNotify(ChordServer.LocalNode);
                        //GetSuccessorCache(this.Successor);
                    }
                    else
                    {
                        //bool successorCacheHelped = false;
                        //foreach (ChordNode entry in this.m_SuccessorCache)
                        //{
                        //    ChordInstance instance = ChordServer.GetInstance(entry);
                        //    if (ChordServer.IsInstanceValid(instance))
                        //    {
                        //        this.Successor = entry;
                        //        ChordServer.CallNotify(this.Successor, ChordServer.LocalNode);
                        //        GetSuccessorCache(this.Successor);
                        //        successorCacheHelped = true;
                        //        break;
                        //    }
                        //}

                        //if (!successorCacheHelped)
                        //{
                        //    //ChordServer.Log(LogLevel.Error, "StabilizeSuccessors", "Ring consistency error, Re-Joining Chord ring.");
                        //    Join(SeedNode, ChordServer.LocalNode.Host, ChordServer.LocalNode.Port);
                        //    return;
                        //}
                    }
                }
                catch (Exception e)
                {
                    //ChordServer.Log(LogLevel.Error, "Maintenance", "Error occured during StabilizeSuccessors ({0})", e.Message);
                }

                // TODO: this could be tweaked and/or made configurable elsewhere or passed in as arguments
                Thread.Sleep(5000);
            }
        }

        public void Notify(ChordNode callingNode)
        {
            if (Predecessor == null || IsIdInRange(callingNode.ID, Predecessor.ID, ChordServer.LocalNode.ID))
            {
                this.Predecessor = callingNode;
                return;
            }
        }

        static int CurrentTableInput = 0;
        private void UpdateFingerTable(object sender, DoWorkEventArgs ea)
        {
            
            BackgroundWorker me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                try
                {
                    try
                    {
                        this.FingerTable.Successors[CurrentTableInput] = FindSuccessor(FingerTable.StartValues[CurrentTableInput]);
                    }
                    catch (Exception e)
                    {
                        Log("Navigation", $"Unable to update Successor for start value {FingerTable.StartValues[CurrentTableInput]} ({e.Message}).");
                    }

                    CurrentTableInput = (CurrentTableInput + 1) % FingerTable.Length;
                }
                catch (Exception e)
                {
                    Log("Maintenance", $"Error occured during UpdateFingerTable ({e.Message})");
                }

                // TODO: make this configurable via config file or passed in as an argument
                Thread.Sleep(1000);
            }
        }

        private bool m_HasReJoinRun = false;
        private void ReJoin(object sender, DoWorkEventArgs ea)
        {
            BackgroundWorker me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                try
                {
                    if (this.m_HasReJoinRun)
                    {
                        // first find the successor for the seed node
                        if (SeedNode != null)
                        {
                            ChordNode seedSuccessor = FindSuccessor(SeedNode.ID);

                            // if the successor is not equal to the seed node, something is fishy
                            if (seedSuccessor.ID != SeedNode.ID)
                            {
                                // if the seed node is still active, re-join the ring to the seed node
                                ChordNodeInstance nodeInstance = SeedNode.GetState();
                                if (nodeInstance.IsStateValid())
                                {
                                    Log( "ReJoin", $"Unable to contact initial seed node {SeedNode}.  Re-Joining...");
                                    Join(SeedNode);
                                }

                                // otherwise, in the future, there will be a cache of seed nodes to check/join from...
                                // as it may be the case that the seed node simply has disconnected from the network.
                            }
                        }
                    }
                    else
                    {
                       
                        this.m_HasReJoinRun = true;
                    }
                }
                catch (Exception e)
                {
                    Log( "Maintenance", $"Error occured during ReJoin ({e.Message})");
                }

                Thread.Sleep(30000);
            }
        }
    }
}
