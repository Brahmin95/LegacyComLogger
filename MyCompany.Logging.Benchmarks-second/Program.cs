using BenchmarkDotNet.Running;

namespace MyCompany.Logging.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Use the BenchmarkRunner to execute our performance tests.
            var summary = BenchmarkRunner.Run<PerformanceBenchmarks>();
        }
    }
}