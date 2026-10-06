using System.Diagnostics;
using System.Globalization;
using System.Threading.Channels;

namespace FormatParse.Example;

internal static class CsvExample
{
    private const int BatchSize = 512;
    private const string Header = "id,first_name,last_name,email,age,city,salary,joined";

    public static async Task RunAsync(string path, int workerCount)
    {
        if (workerCount is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(workerCount), "Use between 1 and 64 workers.");

        var compilation = Stopwatch.StartNew();
        var parser = Parser.For<Employee>("{},{},{},{},{},{},{},{}")
            .Bind(x => x.Id)
            .Bind(x => x.FirstName)
            .Bind(x => x.LastName)
            .Bind(x => x.Email)
            .Bind(x => x.Age)
            .Bind(x => x.City)
            .Bind(x => x.Salary)
            .Bind(x => x.Joined, "yyyy-MM-dd")
            .Compile();
        compilation.Stop();

        // One reader owns the file. Bounded batches limit memory and amortize queue overhead.
        using var cancellation = new CancellationTokenSource();
        var channel = Channel.CreateBounded<string[]>(new BoundedChannelOptions(workerCount * 2)
        {
            SingleWriter = true,
            SingleReader = workerCount == 1,
            FullMode = BoundedChannelFullMode.Wait
        });

        var processing = Stopwatch.StartNew();
        Task<Totals>[] workers = Enumerable.Range(0, workerCount).Select(_ => Task.Run(async () =>
        {
            try
            {
                return await ParseBatchesAsync(channel.Reader, parser, cancellation.Token);
            }
            catch
            {
                cancellation.Cancel();
                throw;
            }
        })).ToArray();

        Task producer = Task.Run(async () =>
        {
            try
            {
                await ReadBatchesAsync(path, channel.Writer, cancellation.Token);
                channel.Writer.TryComplete();
            }
            catch (Exception exception)
            {
                channel.Writer.TryComplete(exception);
                cancellation.Cancel();
                throw;
            }
        });

        // Observe every task, including failures, before disposing shared resources.
        await Task.WhenAll(new[] { producer }.Concat(workers));
        Totals[] results = await Task.WhenAll(workers);
        long parsed = results.Sum(x => x.Parsed);
        long rejected = results.Sum(x => x.Rejected);
        long idSum = results.Sum(x => x.IdSum);
        decimal salary = results.Sum(x => x.Salary);
        processing.Stop();

        Console.WriteLine($"File: {Path.GetFullPath(path)}");
        Console.WriteLine($"Workers: {workerCount}; batch size: {BatchSize}");
        Console.WriteLine(FormattableString.Invariant($"Compile: {compilation.Elapsed.TotalMilliseconds:F2} ms"));
        Console.WriteLine(FormattableString.Invariant($"Read + queue + parse + aggregate: {processing.Elapsed.TotalMilliseconds:F2} ms"));
        Console.WriteLine($"Rows: {parsed + rejected}; parsed: {parsed}; rejected: {rejected}; ID sum: {idSum}");
        Console.WriteLine(FormattableString.Invariant($"Salary total: {salary:F2}"));
        Console.WriteLine($"Rows per worker: {string.Join(", ", results.Select(x => x.Parsed + x.Rejected))}");
    }

    private static async Task ReadBatchesAsync(string path, ChannelWriter<string[]> writer, CancellationToken token)
    {
        using StreamReader reader = File.OpenText(path);
        if (await reader.ReadLineAsync(token) != Header)
            throw new InvalidDataException($"Expected header: {Header}");

        var batch = new List<string>(BatchSize);
        while (await reader.ReadLineAsync(token) is { } line)
        {
            batch.Add(line);
            if (batch.Count == BatchSize)
            {
                await writer.WriteAsync(batch.ToArray(), token);
                batch.Clear();
            }
        }

        if (batch.Count != 0)
            await writer.WriteAsync(batch.ToArray(), token);
    }

    private static async Task<Totals> ParseBatchesAsync(
        ChannelReader<string[]> reader, FormatParser<Employee> parser, CancellationToken token)
    {
        long parsed = 0;
        long rejected = 0;
        long idSum = 0;
        decimal salary = 0;
        await foreach (string[] batch in reader.ReadAllAsync(token))
        {
            foreach (string line in batch)
            {
                // This sample has no quoted fields. It is not a general CSV parser.
                if (!line.Contains('"') && parser.TryParse(line.AsSpan(), CultureInfo.InvariantCulture, out Employee row))
                {
                    parsed++;
                    idSum += row.Id;
                    salary += row.Salary;
                }
                else
                {
                    rejected++;
                }
            }
        }

        // No shared counters or per-row locks: aggregate worker-local results after completion.
        return new Totals(parsed, rejected, idSum, salary);
    }

    private readonly record struct Totals(long Parsed, long Rejected, long IdSum, decimal Salary);
    private readonly record struct Employee(
        int Id, string FirstName, string LastName, string Email, int Age, string City, decimal Salary, DateOnly Joined);
}