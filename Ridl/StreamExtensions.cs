using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ridl
{
    public static class StreamExtensions
    {
        public static void ReadDiscard(this Stream source, int bytes)
        {
            if (bytes <= 0)
                return;

            Span<byte> buffer = stackalloc byte[Math.Min(bytes, 1024)];
            while (bytes > 0)
            {
                int bytesToRead = Math.Min(buffer.Length, bytes);
                source.ReadExactly(buffer[..bytesToRead]);
                bytes -= bytesToRead;
            }
        }
    }
}
