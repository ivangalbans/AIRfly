using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using DHTChord.FTable;
using DHTChord.Node;
using DHTChord.Server;
using static DHTChord.Logger.Logger;
using static DHTChord.MathOperation.ChordMath;

namespace DHTChord.NodeInstance
{
    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.Single)]
    public class ChordNodeInstance : IChordNodeInstance
    {
        public ChordNode LocalNode
        {
            get => ChordServer.LocalNode;
            set { }
        }

        public string Host
        {
            get => ChordServer.LocalNode.Host;
            set { }
        }

        public int Port
        {
            get => ChordServer.LocalNode.Port;
            set { }
        }

        public ulong Id
        {
            get => ChordServer.LocalNode.Id;
            set { }
        }

        public ChordNode SeedNode { get; set; }

        public List<ChordNode> SeedChache { get; set; } = new List<ChordNode>();

        public ChordNode Successor
        {
            get => SuccessorCache[0];
            set
            {
                if (value == null && SuccessorCache[0] != null)
                {
                    Log(LogLevel.Info, "Navigation", $"Setting by {LocalNode} successor to null.");
                }
                else if (value != null &&
                         (SuccessorCache[0] == null || SuccessorCache[0].Id != value.Id))
                {
                    Log(LogLevel.Info, "Navigation", $"New Successor by {LocalNode} {value}.");
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
                    Log(LogLevel.Info, "Navigation", $"Setting predecessor by {LocalNode} to null.");
                }
                else if (value != null &&
                         (_predecessorNode == null || _predecessorNode.Id != value.Id))
                {
                    Log(LogLevel.Info, "Navigation", $"New Predecessor {value} by {LocalNode}.");
                }
                _predecessorNode = value;
            }
        }

        public FingerTable FingerTable { get; set; }

        public ChordNode[] SuccessorCache { get; set; }
        public string ServerPath
        {
            get => ChordServer.LocalNode.Path;
            set => ChordServer.LocalNode.Path = value;
        }
        public string ServerCachePath { get => ServerPath + "Cache\\"; set { } }

        public ChordNode FindClosestPrecedingFinger(ulong id)
        {
            for (var i = FingerTable.Length - 1; i >= 0; --i)
            {
                if (FingerTable.Successors[i] != null && !Equals(FingerTable.Successors[i], ChordServer.LocalNode))
                {
                    if (FingerInRange(FingerTable.Successors[i].Id, Id, id))
                    {
                        var nodeInstance = ChordServer.Instance(FingerTable.Successors[i]);
                        if (IsInstanceValid(nodeInstance, "FindClosestPrecedingFinger111111111111111111111"))
                        {
                            return FingerTable.Successors[i];
                        }
                        nodeInstance.Close();
                    }
                }
            }
            foreach (var t in SuccessorCache)
            {
                if (t != null && !Equals(t, ChordServer.LocalNode))
                {
                    if (FingerInRange(t.Id, Id, id))
                    {
                        var instance = ChordServer.Instance(t);
                        if (IsInstanceValid(instance, "FindClosestPrecedingFinger2222222222222222"))
                        {
                            return t;
                        }
                        instance.Close();
                    }
                }
            }

            return ChordServer.LocalNode;
        }

        public void GetSuccessorCache(ChordNode remoteNode)
        {
            //TODO
            var remoteSuccessorCache = ChordServer.GetSuccessorCache(remoteNode);
            if (remoteSuccessorCache != null)
            {
                SuccessorCache[0] = remoteNode;
                for (var i = 1; i < SuccessorCache.Length; i++)
                {
                    SuccessorCache[i] = remoteSuccessorCache[i - 1];
                }
            }

        }



        public ChordNode FindSuccessor(ulong id)
        {
            if (IsIdInRange(id, Id, Successor.Id))
            {
                return Successor;
            }
            var predNode = FindClosestPrecedingFinger(id);
            var s = ChordServer.CallFindSuccessor(predNode, id);
            return s;
        }

        public static bool IsInstanceValid(ChordNodeInstanceClient instance, string message)
        {
            try
            {
                return instance.Port > 0 && instance.Successor != null;
            }
            catch (Exception e)
            {
                
                Log(LogLevel.Debug, "Instance", $" {message}  Incoming instance was not valid: ({e.Message}).");
                return false;
            }
        }

        public bool Join(ChordNode seed)
        {
            SeedNode = seed;
            FingerTable = new FingerTable(ChordServer.LocalNode);
            SuccessorCache = new ChordNode[8];

            for (var i = 0; i < SuccessorCache.Length; i++)
            {
                SuccessorCache[i] = ChordServer.LocalNode;
            }

            if (seed != null)
            {
                Log(LogLevel.Info, "Navigation", $"Joining ring @ {seed.Host}:{seed.Port}");
                var nodeInstance = ChordServer.Instance(seed);

                if (IsInstanceValid(nodeInstance, "JOIN"))
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
                nodeInstance.Close();
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
        private readonly BackgroundWorker _updateSeedCache = new BackgroundWorker();

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

            _updateSeedCache.DoWork += UpdateSeedCache;
            _updateSeedCache.WorkerSupportsCancellation = true;
            _updateSeedCache.RunWorkerAsync();
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


        private bool _hasReJoin;

        public IEnumerable<ulong> GetKeys()
        {
            List<ulong> copy = new List<ulong>(_db.Keys);
            return copy;
        }

        public bool EraseKey(ulong key)
        {
            return _db.Remove(key);
        }

        public bool EraseFile(ulong key)
        {
            var fileName = GetFromDb(key);
            if (File.Exists(ServerPath + fileName))
            {
                _db.Remove(key);
                File.Delete(ServerPath + fileName);
                return true;
            }
            return false;
        }

        public bool ContainKey(ulong key)
        {
            return _db.ContainsKey(key);
        }

        private void StabilizeDataBase(object sender, DoWorkEventArgs ea)
        {
            BackgroundWorker me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                ChordNodeInstanceClient preInstance = null;
                try
                {
                    preInstance = ChordServer.Instance(Predecessor);
                    if (IsInstanceValid(preInstance, "StabilizeDataBase"))
                    {

                        var prePrePredecessor = preInstance.Predecessor;

                        if (!Successor.Equals(Predecessor))
                        {
                            foreach (var key in GetKeys())
                            {
                                if (!IsIdInRange(key, prePrePredecessor.Id, Predecessor.Id) &&
                                    !IsIdInRange(key, Predecessor.Id, Id))
                                {
                                    if (EraseFile(key))
                                        Log(LogLevel.Info, "EraseFile",
                                            $"Erase File {GetFromDb(key)} successful from {LocalNode}");
                                    else
                                        Log(LogLevel.Error, "EraseFile",
                                            $"Erase key {GetFromDb(key)} unsuccessful from {LocalNode}");
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log(LogLevel.Error, "Maintenance", $"Error occured during StabilizaDataBase ({e.Message})");
                }
                finally
                {
                    if (preInstance != null && preInstance.State != CommunicationState.Closed)
                        preInstance.Close();
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
                    if (_hasReJoin)
                    {
                        foreach (var nodeCache in SeedChache)
                        {
                            var responsableNodeCache = FindContainerKey(nodeCache.Id);
                            if (!ChordServer.SameRing(nodeCache, responsableNodeCache) && Join(nodeCache))
                                break;
                        }
                    }
                    else
                    {
                        _hasReJoin = true;
                    }
                }
                catch (Exception e)
                {
                    Log(LogLevel.Error, "Maintenance", $"Error occured during ReJoin ({e.Message})");
                }
                Thread.Sleep(5000);
            }
        }

        private void StabilizePredecessors(object sender, DoWorkEventArgs ea)
        {
            var me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                if (Predecessor != null)
                {
                    ChordNodeInstanceClient nodeInstance = null;
                    try
                    {
                        nodeInstance = ChordServer.Instance(Predecessor);
                        if (!IsInstanceValid(nodeInstance, "StabilizePredecessors"))
                        {
                            Predecessor = null;
                        }
                        nodeInstance.Close();
                    }
                    catch (Exception e)
                    {
                        Log(LogLevel.Error, "StabilizePredecessors", $"StabilizePredecessors error: {e.Message}");
                        Predecessor = null;
                    }
                    finally
                    {
                        if (nodeInstance != null && nodeInstance.State != CommunicationState.Closed)
                            nodeInstance.Close();
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
                    //if (SeedNode != null)
                    //{
                    //    Console.WriteLine("***************************************");
                    //    var node = LocalNode;
                    //    var nodee = FindContainerKey(node.Id);

                    //    Console.WriteLine(node);
                    //    Console.WriteLine(nodee);

                    //    Console.WriteLine("---------------------");
                    //    Console.WriteLine(SeedNode);
                    //    Console.WriteLine(FindContainerKey(SeedNode.Id));

                    //    Console.WriteLine("***************************************");
                    //}

                    var succPredNode = ChordServer.GetPredecessor(Successor);
                    if (succPredNode != null)
                    {
                        if (IsIdInRange(succPredNode.Id, Id, Successor.Id))
                        {
                            Successor = succPredNode;
                        }
                        ChordServer.CallNotify(Successor, ChordServer.LocalNode);

                        GetSuccessorCache(Successor);
                    }
                    else
                    {
                        var successorCacheHelped = false;
                        foreach (var entry in SuccessorCache)
                        {
                            var instance = ChordServer.Instance(entry);

                            if (IsInstanceValid(instance, "StabilizeSuccessors"))
                            {
                                Successor = entry;
                                ChordServer.CallNotify(Successor, ChordServer.LocalNode);

                                GetSuccessorCache(Successor);

                                successorCacheHelped = true;
                                instance.Close();
                                break;
                            }
                            if (instance != null &&instance.State != CommunicationState.Closed)
                            {
                                instance.Close();
                            }
                        }

                        if (!successorCacheHelped)
                        {
                            Log(LogLevel.Error, "StabilizeSuccessors",
                                "Ring consistency error, Re-Joining Chord ring.");

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

        private void UpdateSeedCache(object sender, DoWorkEventArgs ea)
        {
            var me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                try
                {
                    SeedChache = ChordServer.FindServiceAddress();
                }
                catch (Exception e)
                {
                    Log(LogLevel.Error, "UpdateSeedCache", $"Update Seed Cache error: {e.Message}");
                }
            }
            Thread.Sleep(5000);
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
            //TODO
            var me = (BackgroundWorker)sender;

            while (!me.CancellationPending)
            {
                try
                {
                    try
                    {
                        FingerTable.Successors[_currentTableInput] =
                            FindSuccessor(FingerTable.StartValues[_currentTableInput]);
                    }
                    catch (Exception e)
                    {
                        Log(LogLevel.Error, "Navigation",
                            $"Unable to update Successor for start value {FingerTable.StartValues[_currentTableInput]} ({e.Message}).");
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
            ChordNodeInstanceClient instance = null;
            try
            {
                instance = ChordServer.Instance(Successor);
                instance.Predecessor = Predecessor;

                instance = ChordServer.Instance(Predecessor);
                instance.Successor = Successor;
            }
            catch (Exception e)
            {
                Log(LogLevel.Error, "Navigation", $"Error on Depart ({e.Message}).");
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
                if (instance != null && instance.State != CommunicationState.Closed)
                    instance.Close();
            }
        }

        //public string path = "C:\\AIRfly\\";
        //private static string replication = path + "replication\\";






        private readonly SortedList<ulong, string> _db = new SortedList<ulong, string>();


        public void ViewDataBase()
        {
            Console.WriteLine("Data Base details");
            foreach (var item in _db)
            {
                Console.WriteLine($"{item.Key} {item.Value}");
            }
            Console.WriteLine("*************************************");
            Console.WriteLine();
        }

        public void AddDb(ulong key, string value)
        {
            _db.Add(key, value);
            Console.WriteLine(_db.Count);
        }

        public void AddCache(string value)
        {
            //TODO: Borrar a los n
            _cache.Enqueue(value);
        }

        /// <summary>
        /// Retrieve the string value for a given ulong
        /// key.
        /// </summary>
        /// <param name="key">The key whose value should be returned.</param>
        /// <param name="nodeOut"></param>
        /// <returns>The string value for the given key, or an empty string if not found.</returns>
        public string GetValue(ulong key, out ChordNode nodeOut)
        {
            var tmp = FindContainerKey(key);
            nodeOut = LocalNode;
            var instance = ChordServer.Instance(tmp);
            var str = instance.GetFromDb(key);
            instance.Close();
            return str;
        }

        public string GetFromDb(ulong key)
        {
            return _db.ContainsKey(key) ? _db[key] : string.Empty;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key">The key to find</param>
        /// <returns>The node instance responsable of the key</returns>
        public ChordNode FindContainerKey(ulong key)
        {
            ChordNode owningNode = ChordServer.CallFindSuccessor(key);
            //return owningNode;
            if (owningNode.Equals(ChordServer.LocalNode))
                return owningNode;
            return ChordServer.CallFindContainerKey(owningNode, key);
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
                            ChordServer.CallReplicationFile(Successor, GetFromDb(key));

                        }
                    }

                    var sucInstance = ChordServer.Instance(Successor);
                    if(IsInstanceValid(sucInstance, "ReplicationFile"))
                    {
                        foreach (var key in sucInstance.GetKeys())
                        {
                            if (IsIdInRange(key, Predecessor.Id, Id))
                            {
                                sucInstance.SendFile(sucInstance.GetFromDb(key), LocalNode, null);
                            }
                        }
                        sucInstance.Close();
                    }                    
                }
                catch (Exception e)
                {
                    Log(LogLevel.Error, "Maintenance", $"Error occured during ReplicateStorage ({e.Message})");
                }

                Thread.Sleep(3000);
            }
        }

        public void AddNewFile(FileUploadMessage request)
        {
            ulong key = ChordServer.GetHash(request.Metadata.RemoteFileName);
            UploadFile(request);
            AddDb(key, request.Metadata.RemoteFileName);
        }

        public void SendFile(string remoteFileName, ChordNode remoteNode, string remotePath)
        {


            if (remotePath is null)
                remotePath = ServerPath;

            var instance = ChordServer.Instance(remoteNode);

            var key = ChordServer.GetHash(remoteFileName);
            if (instance.ContainKey(key))
                return;
            Stream fileStream = new FileStream(remotePath + remoteFileName, FileMode.Open, FileAccess.Read);

            var request = new FileUploadMessage();

            var fileMetadata = new FileMetaData(remoteFileName);
            request.Metadata = fileMetadata;
            request.FileByteStream = fileStream;
            Log(LogLevel.Info, "Sending File", $"Sending File {remotePath} ...");
            instance.AddNewFile(request);
            Log(LogLevel.Info, "Finish Send", $"{remotePath} Send Succesfully");
        }

        public void UploadFile(FileUploadMessage request)
        {
            string serverFileName;

            if (request.Cache)
                serverFileName = ServerCachePath + request.Metadata.RemoteFileName;
            else
                serverFileName = ServerPath + request.Metadata.RemoteFileName;

            FileStream outfile = null;
            try
            {
                outfile = new FileStream(serverFileName, FileMode.Create);


                const int bufferSize = 65536; // 64K

                byte[] buffer = new byte[bufferSize];
                int bytesRead = request.FileByteStream.Read(buffer, 0, bufferSize);

                while (bytesRead > 0)
                {
                    outfile.Write(buffer, 0, bytesRead);
                    bytesRead = request.FileByteStream.Read(buffer, 0, bufferSize);
                }

                Log(LogLevel.Info, "Data Recive", $"{Host} {Port} Recive Succefully {request.Metadata.RemoteFileName}");

            }
            catch (IOException e)
            {
                Log(LogLevel.Error, "Recive Data", $"Error while Recive {serverFileName}:   {e}");
            }
            finally
            {
                outfile?.Close();
            }
        }

        public Stream GetStream(string file, bool chache = false)
        {
            Stream fileStream;
            if (chache)            
                fileStream = new FileStream(ServerCachePath + file, FileMode.Open, FileAccess.Read);                           
            else
                fileStream = new FileStream(ServerPath + file, FileMode.Open, FileAccess.Read);

            return fileStream;            
        }
        

        private readonly Queue<string> _cache = new Queue<string>();


        public void AddCacheFile(FileUploadMessage request)
        {
            request.Cache = true;
            AddCache(request.Metadata.RemoteFileName);
            UploadFile(request);
        }
      
        public bool ConteinInCache(string value)
        {
            return _cache.Contains(value);
        }
    }



    public class ChordNodeInstanceClient : ClientBase<IChordNodeInstance>, IChordNodeInstance
    {
        public ChordNodeInstanceClient()
        {
        }

        public ChordNodeInstanceClient(string endpointConfigurationName) :
            base(endpointConfigurationName)
        {
        }

        public ChordNodeInstanceClient(string endpointConfigurationName, string remoteAddress) :
            base(endpointConfigurationName, remoteAddress)
        {
        }

        public ChordNodeInstanceClient(string endpointConfigurationName,
            EndpointAddress remoteAddress) :
            base(endpointConfigurationName, remoteAddress)
        {
        }

        public ChordNodeInstanceClient(Binding binding,
            EndpointAddress remoteAddress) :
            base(binding, remoteAddress)
        {
        }


        public ChordNode LocalNode
        {
            get => Channel.LocalNode;
            set => Channel.LocalNode = value;
        }

        public string Host
        {
            get => Channel.Host;
            set => Channel.Host = value;
        }

        public int Port
        {
            get => Channel.Port;
            set => Channel.Port = value;
        }

        public ulong Id
        {
            get => Channel.Id;
            set => Channel.Id = value;
        }

        public ChordNode Successor
        {
            get => Channel.Successor;
            set => Channel.Successor = value;
        }

        public ChordNode Predecessor
        {
            get => Channel.Predecessor;
            set => Channel.Predecessor = value;
        }

        public ChordNode[] SuccessorCache
        {
            get => Channel.SuccessorCache;
            set => Channel.SuccessorCache = value;
        }
        public string ServerPath
        {
            get => Channel.ServerPath;
            set => Channel.ServerPath = value;
        }

        public string ServerCachePath
        {
            get => Channel.ServerCachePath;
            set => Channel.ServerCachePath = value;
        }

        public ChordNode FindSuccessor(ulong id) => Channel.FindSuccessor(id);

        public bool Join(ChordNode seed) => Channel.Join(seed);

        public IEnumerable<ulong> GetKeys() => Channel.GetKeys();

        public bool EraseKey(ulong key) => Channel.EraseKey(key);

        public bool ContainKey(ulong key) => Channel.ContainKey(key);

        public void Notify(ChordNode callingNode) => Channel.Notify(callingNode);

        public void ViewDataBase() => Channel.ViewDataBase();


        public void AddDb(ulong key, string value) => Channel.AddDb(key, value);

        public string GetValue(ulong key, out ChordNode nodeOut) => Channel.GetValue(key, out nodeOut);

        public string GetFromDb(ulong key) => Channel.GetFromDb(key);

        public ChordNode FindContainerKey(ulong key) => Channel.FindContainerKey(key);

        public void Depart() => Channel.Depart();



        public void UploadFile(FileUploadMessage request)
        {
            Channel.UploadFile(request);
        }

        public void AddNewFile(FileUploadMessage request)
        {
            Channel.AddNewFile(request);
        }

        public void SendFile(string remoteFileName, ChordNode remoteNode, string path)
        {
            Channel.SendFile(remoteFileName, remoteNode, path);
        }

        public bool EraseFile(ulong key)
        {
            return Channel.EraseFile(key);
        }

        public Stream GetStream(string file, bool cache)
        {
            return Channel.GetStream(file, cache);
        }

        public void AddCacheFile(FileUploadMessage request)
        {
            Channel.AddCacheFile(request);
        }

        public void AddCache(string value)
        {
            Channel.AddCache(value);
        }

        public bool ConteinInCache(string value)
        {
            return Channel.ConteinInCache(value);
        }
    }



    [MessageContract]
    public class FileUploadMessage
    {
        [MessageHeader(MustUnderstand = true)]
        public FileMetaData Metadata;
        [MessageBodyMember(Order = 1)]
        public Stream FileByteStream;
        [DataMember(Name = "Cache", Order = 2, IsRequired = false)]
        public bool Cache;
    }



    [DataContract]
    public class FileMetaData
    {
        public FileMetaData(
            string remoteFileName)
        {

            RemoteFileName = remoteFileName;
        }

        [DataMember(Name = "remoteFilename", Order = 2, IsRequired = false)]
        public string RemoteFileName;
    }



}