using System;
using DHTChord.Node;

namespace DHTChord.FTable
{

    [Serializable]
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

            for(var i = 0; i < Length; ++i)
            {
                Successors[i] = node;
                StartValues[i] = node.Id + (1UL << i);
            }
        }
    }
}
