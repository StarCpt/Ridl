using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpPng.Filtering
{
    public static class FilteringHelpers
    {
        public static byte PaethPredictor(byte a, byte b, byte c)
        {
            //int p = a + b - c;
            //int pa = FastAbs(p - a);
            //int pb = FastAbs(p - b);
            //int pc = FastAbs(p - c);

            int pa = b - c;
            int pb = a - c;
            int pc = pa + pb;
            pa = FastAbs(pa);
            pb = FastAbs(pb);
            pc = FastAbs(pc);

            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        private static int FastAbs(int val)
        {
            return val + (val >> 31) ^ val >> 31;
        }
    }
}
