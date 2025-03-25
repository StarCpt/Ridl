using BenchmarkDotNet.Attributes;
using Ridl.Png;

namespace Ridl.Benchmark
{
    [MemoryDiagnoser(false)]
    public class PngDecodeBenchmark
    {
        private readonly byte[] _pngBytes;

        public PngDecodeBenchmark()
        {
            string file = Environment.GetEnvironmentVariable("pngFile") ?? throw new Exception("pngFile envVar not found. This exception shouldn't be thrown!");
            using var fs = File.OpenRead(file);
            _pngBytes = new byte[fs.Length];
            fs.ReadExactly(_pngBytes);
        }

        private MemoryStream GetPngStream()
        {
            MemoryStream pngStream = new(_pngBytes);
            return pngStream;
        }

        [Benchmark(Baseline = true)]
        public void RidlDecode()
        {
            using var pngStream = GetPngStream();
            var image = PngDecoder.Default.Decode(pngStream);
        }

        [Benchmark]
        public void ImageSharpDecode()
        {
            using var pngStream = GetPngStream();
            var bitmap = SixLabors.ImageSharp.Formats.Png.PngDecoder.Instance.Decode(new SixLabors.ImageSharp.Formats.Png.PngDecoderOptions(), pngStream);
        }

        [Benchmark]
        public void SkiaSharpDecode()
        {
            using var pngStream = GetPngStream();
            var bitmap = SkiaSharp.SKBitmap.Decode(pngStream);
        }
    }
}
