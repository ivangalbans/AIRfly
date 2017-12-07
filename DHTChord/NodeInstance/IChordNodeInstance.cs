using System.Collections.Generic;
using System.ServiceModel;
using DHTChord.Node;

namespace DHTChord.NodeInstance
{
    [ServiceContract]
    public interface IChordNodeInstance
    {
        string Host { [OperationContract] get; }

        int Port { [OperationContract] get; }

        ulong Id { [OperationContract] get; }

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
        void AddValue(string value);

        [OperationContract]
        void AddDb(ulong key, string value);

        [OperationContract]
        string GetValue(ulong key, out ChordNode nodeOut);

        [OperationContract]
        string GetFromDb(ulong key);

        [OperationContract]
        IChordNodeInstance FindContainerKey(ulong key);

        [OperationContract]
        void ReplicateKey(ulong key, string value);

        [OperationContract]
        void Depart();
    }
}
