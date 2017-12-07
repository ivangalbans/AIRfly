using System;
using System.ComponentModel;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Markup;

using DHTChord.FTable;
using DHTChord.Node;
using DHTChord.Server;

using static DHTChord.Logger.Logger;
using static DHTChord.Node.ChordNode;
using static DHTChord.MathOperation.ChordMath;

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
                    Log(LogLevel.Info, "Navigation", "Setting successor to null.");
                }
                else if (value != null &&
                         (SuccessorCache[0] == null || SuccessorCache[0].Id != value.Id))
                {
                    Log(LogLevel.Info, "Navigation", $"New Successor {value}.");
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
                    Log(LogLevel.Info, "Navigation", "Setting predecessor to null.");
                }
                else if (value != null &&
                         (_predecessorNode== null || _predecessorNode.Id != value.Id))  
                {
                    Log(LogLevel.Info, "Navigation", $"New Predecessor {value}.");
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
                return ChordServer.CallFindSuccessor(predNode,id);
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
                Log(LogLevel.Debug, "Instance", $"Incoming instance was not valid: ({e.Message}).");
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
                Log(LogLevel.Info, "Navigation", $"Joining ring @ {seed.Host}:{seed.Port}");
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
                        Log(LogLevel.Error, "Navigation", $"Error setting  Successor Node {e.Message}");
                        return false;
                    }
                }
                else
                {
                    Log(LogLevel.Error, "Navigation", "Invalid node seed");
                    return false;
                }
            }
            else
            {
                Log(LogLevel.Info, "Navigation", $"Sarting ring @ {Host}:{Port}");
            }

            StartMaintenance();

            return true;
        }

        private readonly BackgroundWorker _stabilizeSuccessors = new BackgroundWorker();
        private readonly BackgroundWorker _stabilizePredecessors = new BackgroundWorker();
        private readonly BackgroundWorker _updateFingerTable = new BackgroundWorker();
        private readonly BackgroundWorker _reJoin = new BackgroundWorker();
        private readonly BackgroundWorker _replicationStorage = new BackgroundWorker();
        private readonly BackgroundWorker _stabilizeDataBase = new BackgroundWorker();

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

            _reJoin.DoWork += ReJoin;
            _reJoin.WorkerSupportsCancellation = true;
            _reJoin.RunWorkerAsync();

            _replicationStorage.DoWork += ReplicateStorage;
            _replicationStorage.WorkerSupportsCancellation = true;
            _replicationStorage.RunWorkerAsync();

            _stabilizeDataBase.DoWork += StabilizeDataBase;
            _stabilizeDataBase.WorkerSupportsCancellation = true;
            _stabilizeDataBase.RunWorkerAsync();
        }

        public void StopMaintenance()
        {
            _stabilizeSuccessors.CancelAsync();
            _stabilizePredecessors.CancelAsync();
            _updateFingerTable.CancelAsync();
            _reJoin.CancelAsync();
            _replicationStorage.CancelAsync();
            _stabilizeDataBase.CancelAsync();
        }


        private bool HasReJoin = false;

        public IEnumerable<ulong> GetKeys()
        {
            return db.Keys;            
        }

        public bool EraseKey(ulong key)
        {
            return db.Remove(key);            
        }
        public bool ContainKey(ulong key)
        {
            return db.ContainsKey(key);
        }

        private void StabilizeDataBase(object sender, DoWorkEventArgs ea)
        {
            BackgroundWorker me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                try
                {
                    var sucInstance = Instance(Successor);
                    var preInstance = Instance(Predecessor);

                    if (!Successor.Equals(Predecessor))
                    {
                        foreach (var key in GetKeys())
                        {
                            if (preInstance.ContainKey(key) && sucInstance.ContainKey(key))
                            {
                                if (IsIdInRange(key, preInstance.Id, Id))
                                {
                                    if (preInstance.EraseKey(key))
                                        Log(LogLevel.Info, "EraseKey", $"Erase key {key} successful from {Predecessor}");
                                    else
                                        Log(LogLevel.Error, "EraseKey", $"Erase key {key} unsuccessful from {Predecessor}");
                                }
                                else
                                {
                                    if (sucInstance.EraseKey(key))
                                        Log(LogLevel.Info, "EraseKey", $"Erase key {key} successful from {Successor}");
                                    else
                                        Log(LogLevel.Error, "EraseKey", $"Erase key {key} unsuccessful from {Successor}");
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log(LogLevel.Error, "Maintenance", $"Error occured during StabilizaDataBase ({e.Message})");
                }

                Thread.Sleep(3000);
            }
        }

        private void ReJoin(object sender, DoWorkEventArgs ea)
        {
            BackgroundWorker me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                try
                {
                    if (HasReJoin)
                    {
                        if (SeedNode != null)
                        {
                            ChordNode seedSuccessor = FindSuccessor(SeedNode.Id);

                            if (seedSuccessor.Id != SeedNode.Id)
                            {
                                ChordNodeInstance instance = Instance(SeedNode);
                                if (ChordNodeInstance.IsInstanceValid(instance))
                                {
                                    Log(LogLevel.Debug, "ReJoin", $"Unable to contact initial seed node {SeedNode}.  Re-Joining...");
                                    Join(SeedNode);
                                }

                                //!!!!!!!!!!!!!!!TODO!!!!!!!!!!!!!!!!!!!!!!!!!!
                                // otherwise, in the future, there will be a cache of seed nodes to check/join from...
                                // as it may be the case that the seed node simply has disconnected from the network.
                            }
                        }
                    }
                    else
                    {
                        this.HasReJoin = true;
                    }
                }
                catch (Exception e)
                {
                    Log(LogLevel.Error, "Maintenance", $"Error occured during ReJoin ({e.Message})");
                }

                Thread.Sleep(3000);
            }
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
                        Log(LogLevel.Error, "StabilizePredecessors", $"StabilizePredecessors error: {e.Message}");
                        Predecessor = null;
                    }

                }

                Thread.Sleep(100);
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
                            Log(LogLevel.Error, "StabilizeSuccessors", "Ring consistency error, Re-Joining Chord ring.");

                            if (Join(SeedNode))
                            {
                                return;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                   Log(LogLevel.Error, "Maintenance", $"Error occured during StabilizeSuccessors ({e.Message})");
                }

                Thread.Sleep(100);
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
                        Log(LogLevel.Error, "Navigation", $"Unable to update Successor for start value {FingerTable.StartValues[_currentTableInput]} ({e.Message}).");
                    }

                    _currentTableInput = (_currentTableInput + 1) % FingerTable.Length;
                }
                catch (Exception e)
                {
                    Log(LogLevel.Error, "Maintenance", $"Error occured during UpdateFingerTable ({e.Message})");
                }

                Thread.Sleep(100);
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
                Log(LogLevel.Error, "Navigation", $"Error on Depart ({e.Message})." );
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

        #region Storage
        
        private SortedList<ulong, string> db = new SortedList<ulong, string>();


        public void ViewDataBase()
        {
            Console.WriteLine($"Data Base details");
            foreach (var item in db)
            {
                Console.WriteLine($"{item.Key} {item.Value}");
            }
            Console.WriteLine("*************************************");
            Console.WriteLine();
        }

        /// <summary>
        /// Add a key-value pair into database
        /// </summary>
        /// <param name="value">The value to add.</param>
        public void AddValue(string value)
        {
           
            ulong key = ChordServer.GetHash(value);
            ChordServer.CallFindContainerKey(new ChordNode(Host, Port), key).AddDB(key, value);            
        }

        public void AddDB(ulong key, string value)
        {

            db.Add(key, value);
            Console.WriteLine(db.Count);
        }

        /// <summary>
        /// Retrieve the string value for a given ulong
        /// key.
        /// </summary>
        /// <param name="key">The key whose value should be returned.</param>
        /// <returns>The string value for the given key, or an empty string if not found.</returns>
        public string GetValue(ulong key, out ChordNode nodeOut)
        {
            var tmp = FindContainerKey(key);
            nodeOut = new ChordNode(tmp.Host, tmp.Port);
            return tmp.GetFromDB(key);
        }

        public string GetFromDB(ulong key)
        {
            return db.ContainsKey(key) ? db[key] : string.Empty;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key">The key to find</param>
        /// <returns>The node instance responsable of the key</returns>
        public ChordNodeInstance FindContainerKey(ulong key)
        {
            ChordNode owningNode = ChordServer.CallFindSuccessor(key);

            if (owningNode.Equals(ChordServer.LocalNode))
                return Instance(owningNode);
            return ChordServer.CallFindContainerKey(owningNode, key);
        }

        /// <summary>
        /// Add the given key/value pair as replicas to the local store.
        /// </summary>
        /// <param name="key">The key to replicate.</param>
        /// <param name="value">The value to replicate.</param>
        public void ReplicateKey(ulong key, string value)
        {
            if (!this.db.ContainsKey(key))
            {
                Log(LogLevel.Info, "Replication", $"Replication Key and Value {key} {value}");
                this.db.Add(key, value);
            }
        }

        /// <summary>
        /// Replicate the local data store on a background thread.
        /// </summary>
        /// <param name="sender">The background worker thread this task is running on.</param>
        /// <param name="ea">Args (ignored).</param>
        private void ReplicateStorage(object sender, DoWorkEventArgs ea)
        {
            BackgroundWorker me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                try
                {
                    foreach (var key in GetKeys())
                    {
                        if (IsIdInRange(key, Predecessor.Id, Id))
                        {                        
                            ChordServer.CallReplicateKey(this.Successor, key, GetFromDB(key));
                        }
                    }

                    var sucInstance = Instance(Successor);
                    foreach (var key in sucInstance.GetKeys())
                    {
                        if(IsIdInRange(key, Predecessor.Id, Id))
                        {
                            ChordServer.CallReplicateKey(ChordServer.LocalNode, key, sucInstance.GetFromDB(key));
                        }
                    }
                }
                catch (Exception e)
                {
                    Log(LogLevel.Error, "Maintenance", $"Error occured during ReplicateStorage ({e.Message})");
                }

                // TODO: make this configurable via config file or passed in as an argument
                Thread.Sleep(3000);
            }
        }

        #endregion

        #region Transmission



        #endregion

    }
}
