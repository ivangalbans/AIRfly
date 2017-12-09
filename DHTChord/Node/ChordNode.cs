using System.Runtime.Serialization;
using DHTChord.Server;

namespace DHTChord.Node
{
    [DataContract]
    public class ChordNode
    {
        [DataMember]
        public string Host { get; set; }

        [DataMember]
        public int Port { get; set; }


        [DataMember]
        public ulong Id
        {
            get => ChordServer.GetHash(Host.ToUpper() + Port);
            set { }
        }

        public ChordNode(string host, int port)
        {
            Host = host;
            Port = port;

        }
    
        public override string ToString()
        {
            return $"Host {Host}:{Port}";
        }

        public override bool Equals(object obj)
        {
            if (obj is ChordNode tmp)
                return Id == tmp.Id;
            return false;
        }
    }
}
