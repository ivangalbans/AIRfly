using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Core.DHT;
using DHTChord.State;
using DHTChord.Server;

namespace DHTChord.Node
{
    public class ChordNode : IDHTNode
    {
        public string Host { get; set; }
        public int Port { get ; set; }
        public ulong ID { get => ChordServer.GetHash(Host.ToUpper() + Port.ToString());}
        public ChordNode(string host, int port)
        {
            Host = host;
            Port = port;
            
        }
        public ChordState GetState()
        {
            if(this == null)
            {
                throw new Exception("Invalid Node");
            }
            try
            {
                return (ChordState)Activator.GetObject(typeof(ChordState), $"tcp://{Host} : {Port}/chord");
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }
}
