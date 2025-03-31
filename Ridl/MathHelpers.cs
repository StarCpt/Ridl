using System;
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

        /// <summary>
        /// Convert DPM (Dots Per Meter) to DPI (Dots Per Inch) with optional rounding.
        /// </summary>
        /// <returns>DPI</returns>
        public static double DpmToDpi(double dpm, int digitsToRoundTo = 99)
        {
            return double.Round(dpm / 39.3700787402, digitsToRoundTo);
        }

        /// <summary>
        /// Convert DPI (Dots Per Inch) to DPM (Dots Per Meter).
        /// </summary>
        /// <returns>DPI</returns>
        public static int DpiToDpm(double dpi)
        {
            return (int)double.Round(dpi * 39.3700787402);
        }
    }
}
