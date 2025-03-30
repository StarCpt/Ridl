using System.Buffers.Binary;

namespace Ridl.Qoi
{
    public readonly struct QoiHeader
    {
        public readonly uint Width;
        public readonly uint Height;
        public readonly byte Channels;
        public readonly QoiColorSpace ColorSpace;

        public QoiHeader(Span<byte> headerBytes)
        {
            Width = BinaryPrimitives.ReadUInt32BigEndian(headerBytes);
            Height = BinaryPrimitives.ReadUInt32BigEndian(headerBytes[4..]);
            Channels = headerBytes[8];
            ColorSpace = (QoiColorSpace)headerBytes[9];
        }
    }
}
