namespace FormatParse.Example;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                UsageExamples.Run();
                Console.WriteLine("Run with --csv [path] [workers] for the timed Task.Run example.");
                return 0;
            }

            if (args[0] != "--csv" || args.Length > 3)
            {
                Console.Error.WriteLine("Usage: --csv [path] [workers]");
                return 1;
            }

            string path = args.Length >= 2 ? args[1] : Path.Combine(AppContext.BaseDirectory, "sample-5mb.csv");
            int workers = args.Length == 3 ? int.Parse(args[2]) : Math.Clamp(Environment.ProcessorCount, 2, 8);
            await CsvExample.RunAsync(path, workers);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}