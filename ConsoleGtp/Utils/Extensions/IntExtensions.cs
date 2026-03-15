using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleGtp.Utils.Extensions
{
    public static class IntExtensions
    {
        public static bool[] ToBitArray(this int value)
        {
            BitArray ba = new BitArray(new int[] { value });
            bool[] bits = new bool[ba.Count];
            ba.CopyTo(bits, 0);
            return bits;
        }

        public static bool GetBit(this int value, int bitPosition)
        {
            return (value & (1 << bitPosition)) != 0;
        }

        public static int SetBit(this int value, int bitPosition, bool bitValue)
        {
            if (bitValue)
                return value | (1 << bitPosition);
            else
                return value & ~(1 << bitPosition);
        }
    }
}
