using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Core.DHT;

namespace DHTChord.Node
{
    class FingerTable
    {
        public ChordNode[] Successor { get; set; }
        public ulong[] StartValues { get; set; }

        public FingerTable(ChordNode node)
        {
            Successor = new ChordNode[64];
            StartValues = new ulong[64];

            for(int i = 0; i < Successor.Length; ++i)
            {
                Successor[i] = node;
                StartValues[i] = node.ID + (1UL << i);
            }
        }
    }
}
