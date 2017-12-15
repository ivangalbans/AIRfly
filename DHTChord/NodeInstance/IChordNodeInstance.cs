using System.Collections.Generic;
using System.ServiceModel;
using DHTChord.Node;
using System.IO;

namespace DHTChord.NodeInstance
{
    [ServiceContract]
    public interface IChordNodeInstance
    {
        ChordNode LocalNode { [OperationContract] get; [OperationContract] set; }

        string Host { [OperationContract] get; [OperationContract] set; }

        int Port { [OperationContract] get; [OperationContract] set; }

        ulong Id { [OperationContract] get; [OperationContract] set; }

        string ServerPath { [OperationContract] get; [OperationContract]set; }

        string ServerCachePath { [OperationContract] get; [OperationContract]set; }


        ChordNode Successor { [OperationContract] get; [OperationContract] set; }

        ChordNode Predecessor { [OperationContract] get; [OperationContract] set; }

        ChordNode[] SuccessorCache { [OperationContract] get; [OperationContract] set; }

        
        [OperationContract]
        ChordNode FindSuccessor(ulong id);


        [OperationContract]
        bool Join(ChordNode seed);

        [OperationContract]
        IEnumerable<ulong> GetKeys();

        [OperationContract]
        bool EraseKey(ulong key);

        [OperationContract]
        bool ContainKey(ulong key);

        [OperationContract]
        void Notify(ChordNode callingNode);

        [OperationContract]
        void ViewDataBase();

        [OperationContract]
        void AddDb(ulong key, string value);

        [OperationContract]
        string GetValue(ulong key, out ChordNode nodeOut);

        [OperationContract]
        string GetFromDb(ulong key);

        [OperationContract]
        ChordNode FindContainerKey(ulong key);

        [OperationContract]
        void Depart();

        [OperationContract(IsOneWay = true)]
        void UploadFile(FileUploadMessage request);

        [OperationContract]
        void AddNewFile(FileUploadMessage request);
        [OperationContract]
        void SendFile(string remoteFileName, ChordNode remoteNode, string path);
        [OperationContract]
        bool EraseFile(ulong key);
        [OperationContract]
        Stream GetStream(string file, bool cache);
        [OperationContract]
        void AddCacheFile(FileUploadMessage request);
        [OperationContract]
        void AddCache(string value);        
        [OperationContract]
        bool ContainInCache(string value);
        [OperationContract]
        IEnumerable<string> GetDb();
    }
}
