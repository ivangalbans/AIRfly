using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
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

        public ChordNode Successor
        {
            get => SuccessorCache[0];
            set
            {
                if (value == null && SuccessorCache[0] != null)
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
                         (_predecessorNode == null || _predecessorNode.Id != value.Id))
                {
                    Log(LogLevel.Info, "Navigation", $"New Predecessor {value}.");
                }
                _predecessorNode = value;
            }
        }

        public FingerTable FingerTable { get; set; }

        public ChordNode[] SuccessorCache { get; set; }
        public string serverPath
        {
            get => ChordServer.LocalNode.Path;
            set => ChordServer.LocalNode.Path = value;
        }

        public ChordNode FindClosestPrecedingFinger(ulong id)
        {
            for (var i = FingerTable.Length - 1; i >= 0; --i)
            {
                if (FingerTable.Successors[i] != null && !Equals(FingerTable.Successors[i], ChordServer.LocalNode))
                {
                    if (FingerInRange(FingerTable.Successors[i].Id, Id, id))
                    {
                        var nodeInstance = ChordServer.Instance(FingerTable.Successors[i]);
                        if (IsInstanceValid(nodeInstance))
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
                        if (IsInstanceValid(instance))
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
            return ChordServer.CallFindSuccessor(predNode, id);
        }

        public static bool IsInstanceValid(ChordNodeInstanceClient instance)
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
                var nodeInstance = ChordServer.Instance(seed);
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

            //_reJoin.DoWork += ReJoin;
            //_reJoin.WorkerSupportsCancellation = true;
            //_reJoin.RunWorkerAsync();

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
            if(File.Exists(serverPath + fileName))
            {
                _db.Remove(key);
                File.Delete(serverPath + fileName);
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
            BackgroundWorker me = (BackgroundWorker) sender;

            while (!me.CancellationPending)
            {
                ChordNodeInstanceClient sucInstance = null;
                ChordNodeInstanceClient preInstance = null;
                try
                {
                    sucInstance = ChordServer.Instance(Successor);
                    preInstance = ChordServer.Instance(Predecessor);

                    if (!Successor.Equals(Predecessor))
                    {
                        foreach (var key in GetKeys())
                        {
//TODO: erase my own key if the keyis not in either range
                            if (preInstance.ContainKey(key) && sucInstance.ContainKey(key))
                            {
                                if (IsIdInRange(key, preInstance.Id, Id))
                                {
                                    if (preInstance.EraseFile(key))
                                        Log(LogLevel.Info, "EraseFile",
                                            $"Erase File {GetFromDb(key)} successful from {Predecessor}");
                                    else
                                        Log(LogLevel.Error, "EraseFile",
                                            $"Erase key {GetFromDb(key)} unsuccessful from {Predecessor}");
                                }
                                else
                                {
                                    if (sucInstance.EraseFile(key))
                                        Log(LogLevel.Info, "EraseKey", $"Erase key {GetFromDb(key)} successful from {Successor}");
                                    else
                                        Log(LogLevel.Error, "EraseKey",
                                            $"Erase key {GetFromDb(key)} unsuccessful from {Successor}");
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
                    if (sucInstance != null && sucInstance.State != CommunicationState.Closed)
                        sucInstance.Close();
                    if (preInstance != null && preInstance.State != CommunicationState.Closed)
                        preInstance.Close();
                }
                Thread.Sleep(3000);
            }
        }

        private void ReJoin(object sender, DoWorkEventArgs ea)
        {
            BackgroundWorker me = (BackgroundWorker) sender;

            while (!me.CancellationPending)
            {
                ChordNodeInstanceClient instance = null;
                try
                {
                    if (_hasReJoin)
                    {
                        if (SeedNode != null)
                        {
                            if (Successor.Equals(ChordServer.LocalNode))
                            {
                                //Console.WriteLine("RRRRRRRRREEEEEEEEEE");
                                instance = ChordServer.Instance(SeedNode);
                                if (IsInstanceValid(instance))
                                {
                                    Log(LogLevel.Debug, "ReJoin",
                                        $"Unable to contact initial seed node {SeedNode}.  Re-Joining...");
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
                        _hasReJoin = true;
                    }
                }
                catch (Exception e)
                {
                    Log(LogLevel.Error, "Maintenance", $"Error occured during ReJoin ({e.Message})");
                }
                finally
                {
                    if (instance != null && instance.State != CommunicationState.Closed) instance.Close();
                }
                Thread.Sleep(5000);
            }
        }

        private void StabilizePredecessors(object sender, DoWorkEventArgs ea)
        {
            var me = (BackgroundWorker) sender;

            while (!me.CancellationPending)
            {
                if (Predecessor != null)
                {
                    ChordNodeInstanceClient nodeInstance = null;
                    try
                    {
                        nodeInstance = ChordServer.Instance(Predecessor);
                        if (!IsInstanceValid(nodeInstance))
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
            var me = (BackgroundWorker) sender;

            while (!me.CancellationPending)
            {
                try
                {
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

                            if (IsInstanceValid(instance))
                            {

                                Successor = entry;
                                ChordServer.CallNotify(Successor, ChordServer.LocalNode);

                                GetSuccessorCache(Successor);

                                successorCacheHelped = true;
                                instance.Close();
                                break;
                            }
                            else if (instance != null && instance.State != CommunicationState.Closed)
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

            var me = (BackgroundWorker) sender;

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

                Thread.Sleep(1000);
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

        /// <summary>
        /// Add a key-value pair into database
        /// </summary>
        /// <param name="value">The value to add.</param>
        public void AddValue(string value)
        {

            ulong key = ChordServer.GetHash(value);
            var instance = ChordServer.Instance(ChordServer.CallFindContainerKey(new ChordNode(Host, Port), key));
            instance.AddDb(key, value);
            instance.Close();
        }

        public void AddDb(ulong key, string value)
        {

            _db.Add(key, value);
            Console.WriteLine(_db.Count);
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

            if (owningNode.Equals(ChordServer.LocalNode))
                return owningNode;
            return ChordServer.CallFindContainerKey(owningNode, key);
        }

        /// <summary>
        /// Add the given key/value pair as replicas to the local store.
        /// </summary>
        /// <param name="key">The key to replicate.</param>
        /// <param name="value">The value to replicate.</param>
        public void ReplicateKey(ulong key, string value)
        {
            if (!_db.ContainsKey(key))
            {
                Log(LogLevel.Info, "Replication", $"Replication Key and Value {key} {value}");
                _db.Add(key, value);
            }
        }

        
        /// <summary>
        /// Replicate the local data store on a background thread.
        /// </summary>
        /// <param name="sender">The background worker thread this task is running on.</param>
        /// <param name="ea">Args (ignored).</param>
        private void ReplicateStorage(object sender, DoWorkEventArgs ea)
        {
            BackgroundWorker me = (BackgroundWorker) sender;

            while (!me.CancellationPending)
            {
                try
                {

                    foreach (var key in GetKeys())
                    {
                        if (IsIdInRange(key, Predecessor.Id, Id))
                        {
                            //Console.WriteLine($"REPLICATION      {path + GetFromDb(key)}");
                            //ChordServer.CallReplicateKey(Successor, key, GetFromDb(key));
                            ChordServer.CallReplicationFile(Successor, GetFromDb(key));

                        }
                    }

                    var sucInstance = ChordServer.Instance(Successor);
                    foreach (var key in sucInstance.GetKeys())
                    {
                        if (IsIdInRange(key, Predecessor.Id, Id))
                        {
                            //ChordServer.CallReplicateKey(ChordServer.LocalNode, key, sucInstance.GetFromDb(key));
                            sucInstance.SendFile(sucInstance.GetFromDb(key), LocalNode, null);
                        }
                    }
                    sucInstance.Close();
                }
                catch (Exception e)
                {
                    Log(LogLevel.Error, "Maintenance", $"Error occured during ReplicateStorage ({e.Message})");
                }

                // TODO: make this configurable via config file or passed in as an argument
                Thread.Sleep(3000);
            }
        }

        public void AddNewFile(FileUploadMessage request)
        {
            ulong key = ChordServer.GetHash(request.Metadata.RemoteFileName);
            //Console.WriteLine($"ADD NEW FILE {key} {request.Metadata.RemoteFileName}");
            AddDb(key, request.Metadata.RemoteFileName);
            UploadFile(request);
            //if (!_db.ContainsKey(key))
            //{
            //    var instance = ChordServer.Instance(ChordServer.CallFindContainerKey(LocalNode, key));
            //    instance.UploadFile(request);
            //    instance.AddDb(key,request.Metadata.RemoteFileName);
            //    instance.Close();
            //}
        }

        public void SendFile(string remoteFileName, ChordNode remoteNode, string remotePath)
        {
            try
            {


                if (remotePath is null)
                    remotePath = serverPath;

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
            catch (Exception e)
            {
                Console.WriteLine("\n***********************************");
                Console.WriteLine(remotePath);
                Console.WriteLine(remoteFileName);
                Console.WriteLine("***********************************\n");

                throw;
            }
        }

        public void UploadFile(FileUploadMessage request)
        {
            string serverFileName = serverPath + request.Metadata.RemoteFileName;
            
            FileStream outfile = null;
            try
            {
                //Console.WriteLine($"***************** {serverFileName}");
                outfile = new FileStream(serverFileName, FileMode.Create);//TODO

                //Console.WriteLine("========================");

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
                Log(LogLevel.Error, "Recive Data", $"Error while Recive {serverFileName}");
            }
            finally
            {
                outfile?.Close();
            }
        }

        public FileDownloadReturnMessage DownloadFile(FileDownloadMessage request)
        {
            // parameters validation omitted for clarity
            //string localFileName = request.FileMetaData.LocalFileName;

            try
            {
                string basePath = ConfigurationSettings.AppSettings["FileTransferPath"];
                string serverFileName = Path.Combine(serverPath, request.FileMetaData.RemoteFileName);

                Stream fs = new FileStream(serverFileName, FileMode.Open);

                return new FileDownloadReturnMessage(new FileMetaData(serverFileName), fs);
            }
            catch (IOException e)
            {
                throw new FaultException<IOException>(e);
            }
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
        public string serverPath
        {
            get => Channel.serverPath;
            set => Channel.serverPath = value;
        }

        public ChordNode FindSuccessor(ulong id) => Channel.FindSuccessor(id);

        public bool Join(ChordNode seed) => Channel.Join(seed);

        public IEnumerable<ulong> GetKeys() => Channel.GetKeys();

        public bool EraseKey(ulong key) => Channel.EraseKey(key);

        public bool ContainKey(ulong key) => Channel.ContainKey(key);

        public void Notify(ChordNode callingNode) => Channel.Notify(callingNode);

        public void ViewDataBase() => Channel.ViewDataBase();

        public void AddValue(string value) => Channel.AddValue(value);

        public void AddDb(ulong key, string value) => Channel.AddDb(key, value);

        public string GetValue(ulong key, out ChordNode nodeOut) => Channel.GetValue(key, out nodeOut);

        public string GetFromDb(ulong key) => Channel.GetFromDb(key);

        public ChordNode FindContainerKey(ulong key) => Channel.FindContainerKey(key);

        public void ReplicateKey(ulong key, string value) => Channel.ReplicateKey(key, value);

        public void Depart() => Channel.Depart();
       
       

        public void UploadFile(FileUploadMessage request)
        {
            Channel.UploadFile(request);
        }

        public FileDownloadReturnMessage DownloadFile(FileDownloadMessage request)
        {
            return Channel.DownloadFile(request);
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
    }

  

    [MessageContract]
    public class FileUploadMessage
    {
        [MessageHeader(MustUnderstand = true)]
        public FileMetaData Metadata;
        [MessageBodyMember(Order = 1)]
        public Stream FileByteStream;
    }

    [MessageContract]
    public class FileDownloadMessage
    {
        [MessageHeader(MustUnderstand = true)]
        public FileMetaData FileMetaData;
    }

    [MessageContract]
    public class FileDownloadReturnMessage
    {
        public FileDownloadReturnMessage(FileMetaData metaData, Stream stream)
        {
            DownloadedFileMetadata = metaData;
            FileByteStream = stream;
        }

        [MessageHeader(MustUnderstand = true)]
        public FileMetaData DownloadedFileMetadata;
        [MessageBodyMember(Order = 1)]
        public Stream FileByteStream;
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