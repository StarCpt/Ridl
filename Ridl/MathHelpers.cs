﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridl
{
    public static class MathHelpers
    {
        // https://stackoverflow.com/a/17974
        // Note: There is an overflow bug (read the comments)
        public static int DivRoundUp(int dividend, int divisor)
        {
            return (dividend + divisor - 1) / divisor;
        }

        public static int Align(int val, int alignment)
        {
            return (val + (alignment - 1)) & ~(alignment - 1);
        }
    }
}
