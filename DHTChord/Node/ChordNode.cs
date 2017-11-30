using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Core.DHT;
using DHTChord.State;
namespace DHTChord.Node
{
    public class ChordNode : IDHTNode
    {
        public string Host { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int Port { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ulong ID { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

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
