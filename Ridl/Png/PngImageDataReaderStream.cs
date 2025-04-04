﻿using System.Buffers.Binary;
using System.IO.Hashing;

namespace Ridl.Png
{
    internal class PngImageDataReaderStream : Stream
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
        private readonly bool _checkCrc;
        private readonly Crc32 _crc = new(); // crc for the current chunk. accumulated value is checked when the end of the chunk is reached

        /// <summary>
        /// 
        /// </summary>
        /// <param name="baseStream">The position of this stream should be at the start of the first IDAT chunk.</param>
        public PngImageDataReaderStream(Stream baseStream, bool checkCrc)
        {
            if (!baseStream.CanRead)
                throw new Exception($"{nameof(baseStream)} is not readable.");

            _baseStream = baseStream;
            _checkCrc = checkCrc;

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

            // crc includes chunk type but not length
            if (_checkCrc)
                _crc.Append(buffer);

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
                    uint chunkCrc = ReadChunkCrc(); // Advance base stream position to the start of the next chunk
                    if (_checkCrc && _crc.GetCurrentHashAsUInt32() != chunkCrc)
                        throw new Exception("CRC does not match.");
                    _crc.Reset();

                    ReadNextChunkHeader();
                    continue;
                }

                int bytesToRead = Math.Min(availableChunkDataBytes, count - readBytes);
                Span<byte> bufferSlice = buffer.AsSpan(offset + readBytes, bytesToRead);
                _baseStream.ReadExactly(bufferSlice);

                if (_checkCrc)
                    _crc.Append(bufferSlice);

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
