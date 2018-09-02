using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using DHTChord.Node;

namespace DHTChord.NodeInstance
{
    [ServiceContract]
    public interface INodeInstance
    {
        ChordNode[] SuccessorCache
        {
            [OperationContract]
            get;
            [OperationContract]
            set;
        }
        ChordNode[] SeedCache
        {
            [OperationContract]
            get;
            [OperationContract]
            set;
        }
        string Host
        {
            [OperationContract]
            get;
        }

        int Port
        {
            [OperationContract]
            get;
        }

        ulong Id
        {
            [OperationContract]
            get;
        }

        ChordNode Successor
        {
            [OperationContract]
            get;
            [OperationContract]
            set;
        }

        ChordNode Predecessor
        {
            [OperationContract]
            get;
            [OperationContract]
            set;
        }

        [OperationContract]
        void Depart();

        [OperationContract]
        bool Join(ChordNode seed);

        [OperationContract]
        ChordNode FindSuccessor(ulong id);

        [OperationContract]
        void Notify(ChordNode node);
    }
}
