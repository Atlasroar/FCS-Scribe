using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using FcsScribe;

[DataContract]
internal sealed class PerformanceSample
{
    [DataMember] public string Operation;
    [DataMember] public double Milliseconds;
    [DataMember] public long ManagedBytes;
    [DataMember] public long PrivateBytes;
    [DataMember] public long PeakWorkingSetBytes;
}

[DataContract]
internal sealed class PerformanceReport
{
    [DataMember] public string SourceCommit;
    [DataMember] public string HostSha256;
    [DataMember] public string Runtime;
    [DataMember] public int PointerBytes;
    [DataMember] public int Records;
    [DataMember] public int Cells;
    [DataMember] public int Entries;
    [DataMember] public int Sessions;
    [DataMember] public long StorageBytes;
    [DataMember] public List<PerformanceSample> Samples = new List<PerformanceSample>();
}

internal static class PerformanceTests
{
    private static PerformanceReport Report(Snapshot state, string source)
    {
        return new PerformanceReport { SourceCommit = source,
            HostSha256 = TrackingStorage.HashFile(System.Reflection.Assembly.GetExecutingAssembly().Location),
            Runtime = Environment.Version.ToString(),
            PointerBytes = IntPtr.Size, Records = state.Records.Count,
            Cells = state.Records.Sum(x => x.Cells.Count) };
    }

    private static void Measure(PerformanceReport report, string name, Action operation)
    {
        var clock = Stopwatch.StartNew();
        operation();
        clock.Stop();
        using (var process = Process.GetCurrentProcess())
        {
            process.Refresh();
            report.Samples.Add(new PerformanceSample { Operation = name, Milliseconds = clock.Elapsed.TotalMilliseconds,
                ManagedBytes = GC.GetTotalMemory(false), PrivateBytes = process.PrivateMemorySize64,
                PeakWorkingSetBytes = process.PeakWorkingSet64 });
        }
    }

    private static void Save(PerformanceReport report, string path)
    {
        using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            Json.WriteStream(output, report);
        foreach (var group in report.Samples.GroupBy(x => x.Operation))
        {
            double[] times = group.Select(x => x.Milliseconds).OrderBy(x => x).ToArray();
            Console.WriteLine("{0}: n={1}, min={2:F1} ms, median={3:F1} ms, max={4:F1} ms, peak working set={5:F1} MiB",
                group.Key, group.Count(), group.Min(x => x.Milliseconds),
                (times[(times.Length - 1) / 2] + times[times.Length / 2]) / 2,
                group.Max(x => x.Milliseconds), group.Max(x => x.PeakWorkingSetBytes) / 1048576.0);
        }
        Console.WriteLine("Performance report: " + path);
    }

    public static void Native(Func<Snapshot> capture, string directory, string source, int iterations)
    {
        Snapshot state = capture();
        var report = Report(state, source);
        string path = Path.Combine(directory, "performance-native.json");
        JournalProject project = null;
        try
        {
            Measure(report, "create", () => project = JournalProject.Create(path, state));
            project.BeginSession();
            for (int i = 0; i < iterations; i++)
            {
                Measure(report, "native-capture", () => state = capture());
                Measure(report, "unchanged-review", () => project.Record(state));
            }
            report.Sessions = 1;
            report.StorageBytes = new FileInfo(path).Length +
                Directory.GetFiles(path + ".scribe", "*", SearchOption.AllDirectories).Sum(x => new FileInfo(x).Length);
        }
        finally { if (project != null) project.Dispose(); }
        Measure(report, "resume", () => { using (JournalProject.Open(path)) { } });
        Save(report, Path.Combine(directory, "performance-native-report.json"));
    }

    private static Snapshot Synthetic(int records, int largeCells)
    {
        var state = new Snapshot { ModId = "synthetic-performance", ModName = "synthetic.mod" };
        for (int i = 0; i < records; i++)
        {
            var record = new Record { Id = "item" + i, Type = "ITEM", Name = "Item " + i };
            for (int j = 0; j < (i == 0 ? largeCells : 8); j++)
                record.Cells.Add(new Cell { Layer = "active", Path = "/fields/" + j,
                    Source = "synthetic.mod", Value = Value.Of("Int32", "10") });
            state.Records.Add(record);
        }
        return state;
    }

    public static void History(string directory, string source, int records, int entries, int sessions, int largeCells)
    {
        Snapshot state = Synthetic(records, largeCells);
        var report = Report(state, source);
        string path = Path.Combine(directory, "history.json");
        JournalProject project = null;
        try
        {
            Measure(report, "create", () => project = JournalProject.Create(path, state));
            int perSession = entries / sessions;
            for (int i = 0; i < entries; i++)
            {
                if (i % perSession == 0) project.BeginSession();
                Snapshot next = state.Copy();
                next.Records[0].Name = "Observation " + i;
                Measure(report, "changed-review", () => project.Record(next));
                state = next;
                if ((i + 1) % perSession == 0)
                    Measure(report, "compile-pending", () => project.Compile());
            }
            report.Entries = project.EntryCount;
            report.Sessions = sessions;
            for (int i = 0; i < 3; i++)
            {
                Measure(report, "unchanged-review", () => project.Record(state));
                Measure(report, "compile-noop", () => {
                    if (project.Compile() != 0) throw new Exception("Repeated compilation was not a no-op.");
                });
            }
            report.StorageBytes = new FileInfo(path).Length +
                Directory.GetFiles(path + ".scribe", "*", SearchOption.AllDirectories).Sum(x => new FileInfo(x).Length);
        }
        finally { if (project != null) project.Dispose(); }
        for (int i = 0; i < 3; i++)
            Measure(report, "resume-retained", () => {
                using (var reopened = JournalProject.Open(path))
                    if (reopened.EntryCount != entries || reopened.Fingerprint != state.Fingerprint())
                        throw new Exception("Retained-history replay changed state or lost entries.");
            });
        Save(report, Path.Combine(directory, "performance-report.json"));
    }
}

internal static class PerformanceProgram
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 6) throw new ArgumentException("Expected directory, source, records, entries, sessions and large-record cells.");
            int records = Int32.Parse(args[2], CultureInfo.InvariantCulture);
            int entries = Int32.Parse(args[3], CultureInfo.InvariantCulture);
            int sessions = Int32.Parse(args[4], CultureInfo.InvariantCulture);
            int cells = Int32.Parse(args[5], CultureInfo.InvariantCulture);
            if (IntPtr.Size != 4 || records < 1 || sessions < 1 || entries < sessions ||
                entries % sessions != 0 || cells < 1)
                throw new ArgumentException("Use an x86 host and positive dimensions; entries must divide evenly across sessions.");
            PerformanceTests.History(args[0], args[1], records, entries, sessions, cells);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
