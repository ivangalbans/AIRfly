using System;

using DHTChord.Node;
using DHTChord.Server;
using static DHTChord.MathOperation.ChordMath;

namespace DHTChord.State
{
    public class ChordState : MarshalByRefObject
    {
        private ChordNode SeedNode = null;
        public ChordNode Successor { get; set; }
        public ChordNode Predecessor { get; set; }
        public FingerTable FingerTable { get; set; }


        private ChordNode FindClosestPrecedingFinger(ulong id)
        {
            for (int i = FingerTable.Length - 1; i >= 0; i--)
            {
                // if the finger is more closely between the local node and id and that finger corresponds to a valid node, return the finger
                if (this.FingerTable.Successors[i] != null && this.FingerTable.Successors[i] != ChordServer.LocalNode)
                {
                    if (FingerInRange(FingerTable.Successors[i].ID, ChordServer.LocalNode.ID, id))
                    {
                        ChordState instance = FingerTable.Successors[i].GetState();
                        if (instance.IsStateValid())
                        {
                            return FingerTable.Successors[i];
                        }
                    }
                }
            }


            /*
             * TODO: CACHE
             * */


            return ChordServer.LocalNode;
        }

        private ChordNode FindPredecessor(ulong id)
        {
            var currentNode = ChordServer.LocalNode;
            var currentState = currentNode.GetState();
            while (!IsIDInRange(id, currentNode.ID, currentState.Successor.ID))
            {
                currentNode = currentState.FindClosestPrecedingFinger(id);
                currentState = currentNode.GetState();
            }
            return currentNode;
        }

        private ChordNode FindSuccessor(ulong id)
        {
            return FindPredecessor(id).GetState().Successor;
        }

        private bool IsStateValid()
        {
            try
            {
                if (ChordServer.LocalNode.Port > 0 && Successor != null)
                    return true;
            }
            catch (Exception e)
            {
                throw e;
            }
            return false;
        }
    }
}
