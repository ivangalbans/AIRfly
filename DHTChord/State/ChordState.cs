using System;

using DHTChord.Node;
using DHTChord.Server;
using static DHTChord.MathOperation.ChordMath;
using System.ComponentModel;
using System.Threading;
using DHTChord.MathOperation;

namespace DHTChord.State
{
    public class ChordState : MarshalByRefObject
    {
        private ChordNode SeedNode = null;
        public ChordNode Successor { get; set; }
        public ChordNode Predecessor { get; set; }
        public FingerTable FingerTable { get; set; }


        private ChordNode FindClosestPrecedingFinger(ulong id)
        {
            for (int i = FingerTable.Length - 1; i >= 0; i--)
            {
                // if the finger is more closely between the local node and id and that finger corresponds to a valid node, return the finger
                if (this.FingerTable.Successors[i] != null && this.FingerTable.Successors[i] != ChordServer.LocalNode)
                {
                    if (FingerInRange(FingerTable.Successors[i].ID, ChordServer.LocalNode.ID, id))
                    {
                        ChordState instance = FingerTable.Successors[i].GetState();
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
            var currentNode = ChordServer.LocalNode;
            var currentState = currentNode.GetState();
            while (!IsIDInRange(id, currentNode.ID, currentState.Successor.ID))
            {
                currentNode = currentState.FindClosestPrecedingFinger(id);
                currentState = currentNode.GetState();
            }
            return currentNode;
        }

        private ChordNode FindSuccessor(ulong id)
        {
            return FindPredecessor(id).GetState().Successor;
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
                throw e;
            }
            return false;
        }

        public bool Join(ChordNode seed, string host, int port)
        {
            ChordServer.LocalNode = new ChordNode(host, port);
            SeedNode = seed;

            FingerTable = new FingerTable(ChordServer.LocalNode);

            Successor = ChordServer.LocalNode;
            //TODO: Cache

            if(seed != null)
            {
                ChordState state = seed.GetState();
                if(state.IsStateValid())
                {
                    try
                    {
                        Successor = state.FindSuccessor(ChordServer.LocalNode.ID);
                    }
                    catch (Exception e)
                    {

                        throw e;
                    }
                }
                else
                {
                    Console.WriteLine($"New Ring{host} {port}");
                }
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
                        ChordState state = Predecessor.GetState();
                        if (!state.IsStateValid())
                        {
                            Predecessor = null;
                        }
                    }
                    catch (Exception e)
                    {
                        //TODO: Log
                        Predecessor = null;
                        throw e;
                    }

                }

                // TODO: make this configurable either via config file or passed in via arguments.
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

                    ChordNode succPredNode = Successor.GetState().Predecessor;//ULTRA KILL
                    if (succPredNode != null)
                    {
                        if (IsIDInRange(succPredNode.ID, ChordServer.LocalNode.ID, Successor.ID))
                        {
                            Successor = succPredNode;
                        }

                        Successor.GetState().Notify(ChordServer.LocalNode);
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

        private void Notify(ChordNode callingNode)
        {
            if (Predecessor == null)
                return;

            if(IsIDInRange(callingNode.ID, Predecessor.ID, ChordServer.LocalNode.ID))
            {
                Predecessor = callingNode;
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
                        //ChordServer.Log(LogLevel.Error, "Navigation", "Unable to update Successor for start value {0} ({1}).", this.FingerTable.StartValues[this.m_NextFingerToUpdate], e.Message);
                    }

                    CurrentTableInput = (CurrentTableInput + 1) % FingerTable.Length;
                }
                catch (Exception e)
                {
                    // (overly safe here)
                    //ChordServer.Log(LogLevel.Error, "Maintenance", "Error occured during UpdateFingerTable ({0})", e.Message);
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
                                ChordState state = SeedNode.GetState();
                                if (state.IsStateValid())
                                {
                                    //ChordServer.Log(LogLevel.Error, "ReJoin", "Unable to contact initial seed node {0}.  Re-Joining...", this.m_SeedNode);
                                    Join(SeedNode, ChordServer.LocalNode.Host, ChordServer.LocalNode.Port);
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
                    //ChordServer.Log(LogLevel.Error, "Maintenance", "Error occured during ReJoin ({0})", e.Message);
                }

                Thread.Sleep(30000);
            }
        }
    }
}
