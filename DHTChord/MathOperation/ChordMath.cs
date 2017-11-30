using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DHTChord.MathOperation
{
    public static class ChordMath
    {
        public static bool IsIDInRange(ulong id, ulong start, ulong end)
        {
            if(start >= end)
            {
                if (id > start || id <= end) return true;
            }
            else
            {
                if (id > start && id <= end) return true;
            }
            return false;
        }

        public static bool FingerInRange(ulong id, ulong start, ulong end)
        {
            if (start == end) return true;
            if(start > end)
            {
                if (id > start || id < end) return true;
            }
            else
            {
                if (start < id && id < end) return true;
            }
            return false;
        }
    }
}
