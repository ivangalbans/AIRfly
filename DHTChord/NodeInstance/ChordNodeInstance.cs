﻿using System;
using System.ComponentModel;
using System.Linq;
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
        public ChordNode SeedNode { get; set; }

        public ChordNode Successor
        {
            get => SuccessorCache[0];
            set => SuccessorCache[0] = value;
        }
        public ChordNode Predecessor { get; set; }
        public FingerTable FingerTable { get; set; }

        public ChordNode[] SuccessorCache { get; set; }
        public ChordNode[] SeedCache { get; set; }

        public ChordNode FindClosestPrecedingFinger(ulong id)
        {
            for (int i = FingerTable.Length - 1; i >= 0; --i)
            {
                if (FingerTable.Successors[i] != null && FingerTable.Successors[i] != ChordServer.LocalNode)
                {
                    if (FingerInRange(FingerTable.Successors[i].Id, ChordServer.LocalNode.Id, id))
                    {
                        ChordNodeInstance nodeInstance = FingerTable.Successors[i].GetNodeInstance();
                        if (nodeInstance.IsStateValid())
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
                    if (FingerInRange(t.Id,ChordServer.LocalNode.Id, id))
                    {
                        if (t.GetNodeInstance().IsStateValid())
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
            var remoteSuccessorCache = remoteNode.GetSuccessorCache();
            if (remoteSuccessorCache != null)
            {
                SuccessorCache[0] = remoteNode;
                for (int i = 1; i < SuccessorCache.Length; i++)
                {
                    SuccessorCache[i] = remoteSuccessorCache[i - 1];
                }
            }
        }

        public static Random _random = new Random(Environment.TickCount);
        public void GetSeedCache()
        {
            for (int i = 1; i < SuccessorCache.Length; i++)
            {
                SeedCache[i] = SuccessorCache[i - 1].GetNodeInstance().SeedNode;
            }
            for (int i = SuccessorCache.Length; i < SeedCache.Length; i++)
            {
                SeedCache[i] = FindSuccessor(ChordServer.GetHash(
                    _random.Next().ToString() + _random.Next()));
            }
        }


        public ChordNode FindPredecessor(ulong id)
        {
            var currentNode = ChordServer.LocalNode;
            var currentNodeInstance = currentNode.GetNodeInstance();
            while (!IsIdInRange(id, currentNode.Id, currentNodeInstance.Successor.Id))
            {
                currentNode = currentNodeInstance.FindClosestPrecedingFinger(id);
                currentNodeInstance = currentNode.GetNodeInstance();
            }
            return currentNode;
        }

        public ChordNode FindSuccessor(ulong id)
        {
            return FindPredecessor(id).GetSuccessor();
        }

        public bool IsStateValid()
        {
            try
            {
                if (ChordServer.LocalNode.Port > 0 && Successor != null)
                    return true;
                else
                    return false;
            }
            catch (Exception e)
            {
                Log("Incoming instance was not valid", e.Message);
                return false;

            }
        }

        public bool Join(ChordNode seed)
        {
            SeedNode = seed;
            FingerTable = new FingerTable(ChordServer.LocalNode);
            
            SuccessorCache = new ChordNode[8];
            SeedCache = new ChordNode[16];

            for (int i = 0; i < SuccessorCache.Length; i++)
            {
                SuccessorCache[i] = ChordServer.LocalNode;
            }
            for (int i = 0; i < SeedCache.Length; i++)
            {
                SeedCache[i] = seed;
            }

            if (seed != null)
            {
                Log("Navigation", $"Joining ring @ {seed.Host}:{seed.Port}");
                ChordNodeInstance nodeInstance = seed.GetNodeInstance();
                if (nodeInstance.IsStateValid())
                {
                    try
                    {
                        Successor = nodeInstance.FindSuccessor(ChordServer.LocalNode.Id);
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

        public readonly BackgroundWorker _mStabilizeSuccessors = new BackgroundWorker();
        public readonly BackgroundWorker _mStabilizePredecessors = new BackgroundWorker();
        public readonly BackgroundWorker _mUpdateFingerTable = new BackgroundWorker();
        public readonly BackgroundWorker _mRejoin = new BackgroundWorker();

        public void StartMaintenance()
        {
            _mStabilizeSuccessors.DoWork += StabilizeSuccessors;
            _mStabilizeSuccessors.WorkerSupportsCancellation = true;
            _mStabilizeSuccessors.RunWorkerAsync();

            _mStabilizePredecessors.DoWork += StabilizePredecessors;
            _mStabilizePredecessors.WorkerSupportsCancellation = true;
            _mStabilizePredecessors.RunWorkerAsync();

            _mUpdateFingerTable.DoWork += UpdateFingerTable;
            _mUpdateFingerTable.WorkerSupportsCancellation = true;
            _mUpdateFingerTable.RunWorkerAsync();

            _mRejoin.DoWork += ReJoin;
            _mRejoin.WorkerSupportsCancellation = true;
            _mRejoin.RunWorkerAsync();
        }

        public void StopMaintenance()
        {
            _mStabilizeSuccessors.CancelAsync();
            _mStabilizePredecessors.CancelAsync();
            _mUpdateFingerTable.CancelAsync();
            _mRejoin.CancelAsync();
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
                        ChordNodeInstance nodeInstance = Predecessor.GetNodeInstance();
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

        public void StabilizeSuccessors(object sender, DoWorkEventArgs ea)
        {
            BackgroundWorker me = (BackgroundWorker)sender;

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
                        GetSuccessorCache(Successor);
                        GetSeedCache();
                    }
                    else
                    {

                        bool successorCacheHelped = false;
                        foreach (ChordNode entry in SuccessorCache)
                        {
                            var instance = entry.GetNodeInstance();
                            if (instance.IsStateValid())
                            {
                                Successor = entry;
                                Successor.CallNotify(ChordServer.LocalNode);
                                GetSuccessorCache(Successor);
                                GetSeedCache();
                                successorCacheHelped = true;
                                break;
                            }
                        }

                        if (!successorCacheHelped)
                        {
                            if (SeedCache.Any(Join))
                            {
                                return;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
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
                return;
            }
        }

        static int _currentTableInput = 0;
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

                // TODO: make this configurable via config file or passed in as an argument
                Thread.Sleep(1000);
            }
        }

        public bool _mHasReJoinRun = false;
        public void ReJoin(object sender, DoWorkEventArgs ea)
        {
            BackgroundWorker me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                try
                {
                    if (_mHasReJoinRun)
                    {
                        // first find the successor for the seed node
                        if (SeedNode != null)
                        {
                            ChordNode seedSuccessor = FindSuccessor(SeedNode.Id);

                            // if the successor is not equal to the seed node, something is fishy
                            if (seedSuccessor.Id != SeedNode.Id)
                            {
                                // if the seed node is still active, re-join the ring to the seed node
                                ChordNodeInstance nodeInstance = SeedNode.GetNodeInstance();
                                if (nodeInstance.IsStateValid())
                                {
                                    Log( "ReJoin", $"Unable to contact initial seed node {SeedNode}.  Re-Joining...");
                                    SeedCache.Any(Join);
                                }

                                // otherwise, in the future, there will be a cache of seed nodes to check/join from...
                                // as it may be the case that the seed node simply has disconnected from the network.
                            }
                        }
                    }
                    else
                    {
                        _mHasReJoinRun = true;
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
