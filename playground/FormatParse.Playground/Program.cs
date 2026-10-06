
namespace FormatParse.Playground;

record Log(
    string Name,
    DateTime TimeStamp,
    int Score
);

class Program
{
    static void Main()
    {
        const string s = "alice,100,2026-10-5 10:36:45{";
        const string p = "{},{},{}{{";


        var parser = Parser.For<Log>(p)
            .Bind(x => x.Name)
            .Bind(x => x.Score)
            .Bind(x => x.TimeStamp)
            .Compile();


        var log = parser.Parse(s);
        Console.WriteLine(log);
    }
}
