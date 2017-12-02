using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Core.DHT;
using DHTChord.Node;

namespace DHTChord.FTable
{
    public class FingerTable
    {
        public ChordNode[] Successors { get; set; }
        public ulong[] StartValues { get; set; }
        public int Length { get; }

        public FingerTable(ChordNode node)
        {
            Successors = new ChordNode[64];
            StartValues = new ulong[64];
            Length = 64;

            for(int i = 0; i < Successors.Length; ++i)
            {
                Successors[i] = node;
                StartValues[i] = node.ID + (1UL << i);
            }
        }
    }
}
