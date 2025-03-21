using System.Buffers.Binary;

namespace SharpPng
{
    internal class ImageDataReaderStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _baseStream.Length;
        public override long Position
        {
            get => _baseStream.Position;
            set => throw new NotSupportedException();
        }

        public bool EndOfImageData => _endOfImageData;

        private int _currentChunkDataLength;
        private int _currentChunkDataPosition;
        private bool _endOfImageData = false;

        private readonly Stream _baseStream;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="baseStream">The position of this stream should be at the start of the first IDAT chunk.</param>
        public ImageDataReaderStream(Stream baseStream, bool checkCrc)
        {
            if (!baseStream.CanRead)
                throw new Exception($"{nameof(baseStream)} is not readable.");

            if (checkCrc)
                throw new NotImplementedException();

            _baseStream = baseStream;

            ReadNextChunkHeader();
        }

        private void ReadNextChunkHeader()
        {
            // position should be at the start of the next chunk
            Span<byte> buffer = stackalloc byte[4];

            // read chunk length
            _baseStream.ReadExactly(buffer);
            _currentChunkDataLength = (int)BinaryPrimitives.ReadUInt32BigEndian(buffer);
            _currentChunkDataPosition = 0;

            // read chunk type
            _baseStream.ReadExactly(buffer);
            var chunkType = new ChunkType(buffer);

            _endOfImageData = chunkType != ChunkType.ImageData;
        }

        private uint ReadChunkCrc()
        {
            // baseStream position should be at the start of the crc
            Span<byte> buffer = stackalloc byte[4];
            _baseStream.ReadExactly(buffer);
            return BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count <= 0 || _endOfImageData)
                return 0;

            // Assume buffer.Length >= count - offset
            // If not, the array will throw an exception, no need to check bounds here

            int readBytes = 0;
            while (!_endOfImageData && readBytes < count)
            {
                int availableChunkDataBytes = _currentChunkDataLength - _currentChunkDataPosition;
                if (availableChunkDataBytes <= 0)
                {
                    ReadChunkCrc(); // Advance base stream position to the start of the next chunk
                    ReadNextChunkHeader();
                    continue;
                }

                int bytesToRead = Math.Min(availableChunkDataBytes, count - readBytes);
                Span<byte> bufferSlice = buffer.AsSpan(offset + readBytes, bytesToRead);
                _baseStream.ReadExactly(bufferSlice);

                _currentChunkDataPosition += bytesToRead;
                readBytes += bytesToRead;
            }

            return readBytes;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
