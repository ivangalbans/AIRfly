using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Core.DHT;

namespace DHTChord.Node
{
    class ChordNode : IDHTNode
    {
        public string Host { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int Port { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ulong ID { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }
}
