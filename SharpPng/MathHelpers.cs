using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpPng
{
    public static class MathHelpers
    {
        // https://stackoverflow.com/a/17974
        // Note: There is an overflow bug (read the comments)
        public static int DivRoundUp(int dividend, int divisor)
        {
            return (dividend + divisor - 1) / divisor;
        }
    }
}
