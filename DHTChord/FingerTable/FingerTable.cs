using DHTChord.Node;

namespace DHTChord.FingerTable
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
                StartValues[i] = node.Id + (1UL << i);
            }
        }
    }
}
