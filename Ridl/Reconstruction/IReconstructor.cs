using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridl.Reconstruction
{
    internal interface IReconstructor
    {
        void FilterSub(Span<byte> scanline);
        void FilterUp(Span<byte> scanline, ReadOnlySpan<byte> prevScan);
        void FilterAvgScan0(Span<byte> scanline);
        void FilterAvg(Span<byte> scanline, ReadOnlySpan<byte> prevScan);
        void FilterPaethScan0(Span<byte> scanline);
        void FilterPaeth(Span<byte> scanline, ReadOnlySpan<byte> prevScan);
    }
}
