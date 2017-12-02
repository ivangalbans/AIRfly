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
            Length = 64;
            Successors = new ChordNode[Length];
            StartValues = new ulong[Length];

            for(int i = 0; i < Length; ++i)
            {
                Successors[i] = node;
                StartValues[i] = node.Id + (1UL << i);
            }
        }
    }
}
