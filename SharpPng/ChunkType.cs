using System.Diagnostics.CodeAnalysis;

namespace SharpPng
{
    internal readonly struct ChunkType
    {
        // Critical Types
        public static readonly ChunkType Header = new("IHDR");
        public static readonly ChunkType Trailer = new("IEND");
        public static readonly ChunkType Palette = new("PLTE");
        public static readonly ChunkType ImageData = new("IDAT");

        // Optional Types
        public static readonly ChunkType Transparency = new("tRNS");
        public static readonly ChunkType SignificantBits = new("sBIT");
        public static readonly ChunkType BackgroundColor = new("bKGD");
        public static readonly ChunkType PixelDimensions = new("pHYs");
        public static readonly ChunkType SuggestedPalette = new("sPLT");
        public static readonly ChunkType Timestamp = new("tIME");

        public readonly string Name => new([ (char)b0, (char)b1, (char)b2, (char)b3 ]);

        /// <summary>
        /// Critical chunks are necessary for successful display of the contents of the datastream, for example the image header chunk (IHDR). A decoder trying to extract the image, upon encountering an unknown chunk type in which the ancillary bit is 0, shall indicate to the user that the image contains information it cannot safely interpret.
        /// Ancillary chunks are not strictly necessary in order to meaningfully display the contents of the datastream, for example the time chunk(tIME). A decoder encountering an unknown chunk type in which the ancillary bit is 1 can safely ignore the chunk and proceed to display the image.
        /// </summary>
        public readonly bool IsAncillary => (b0 & 0x20) != 0;

        /// <summary>
        /// A public chunk is one that is defined in this International Standard or is registered in the list of PNG special-purpose public chunk types maintained by the Registration Authority (see 4.9 Extension and registration). Applications can also define private (unregistered) chunk types for their own purposes. The names of private chunks have a lowercase second letter, while public chunks will always be assigned names with uppercase second letters. Decoders do not need to test the private-chunk property bit, since it has no functional significance; it is simply an administrative convenience to ensure that public and private chunk names will not conflict. See clause 14: Editors and extensions and 12.10.2: Use of private chunks.
        /// </summary>
        public readonly bool IsPrivate => (b1 & 0x20) != 0;

        /// <summary>
        /// The significance of the case of the third letter of the chunk name is reserved for possible future extension. In this International Standard, all chunk names shall have uppercase third letters.
        /// </summary>
        /// 
        /// <remarks>
        /// If the reserved bit is 1, the datastream does not conform to this version of PNG.
        /// </remarks>
        public readonly bool IsReserved => (b2 & 0x20) != 0;

        /// <summary>
        /// This property bit is not of interest to pure decoders, but it is needed by PNG editors. This bit defines the proper handling of unrecognized chunks in a datastream that is being modified. Rules for PNG editors are discussed further in 14.2: Behaviour of PNG editors.
        /// </summary>
        public readonly bool IsSafeToCopy => (b3 & 0x20) != 0;

        public readonly byte b0, b1, b2, b3;

        public ChunkType(string name)
        {
            b0 = Convert.ToByte(name[0]);
            b1 = Convert.ToByte(name[1]);
            b2 = Convert.ToByte(name[2]);
            b3 = Convert.ToByte(name[3]);
        }

        public ChunkType(ReadOnlySpan<byte> data)
        {
            b0 = data[0];
            b1 = data[1];
            b2 = data[2];
            b3 = data[3];
        }

        public override string ToString() => Name;
        public static bool operator ==(ChunkType x, ChunkType y) => x.b0 == y.b0 && x.b1 == y.b1 && x.b2 == y.b2 && x.b3 == y.b3;
        public static bool operator !=(ChunkType x, ChunkType y) => x.b0 != y.b0 || x.b1 != y.b1 || x.b2 != y.b2 || x.b3 != y.b3;
        public override bool Equals([NotNullWhen(true)] object? obj) => obj is ChunkType chunkType && this == chunkType;
        public override int GetHashCode() => (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
    }
}
