using BenchmarkDotNet.Running;

namespace Ridl.Benchmark
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
                throw new ArgumentException("No argument found");

            string file = args[0];

            if (!File.Exists(file))
                throw new FileNotFoundException(null, file);

            BenchmarkRunner.Run<PngDecodeBenchmark>(null, [ $"--envVars", $"pngFile:{file}" ]);
        }
    }
}
