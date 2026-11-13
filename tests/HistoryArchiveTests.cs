using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FcsScribe;

internal static class HistoryArchiveTests
{
    private static int passed;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception("FAILED: " + message); passed++; }
    private static void Reject<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { passed++; return; }
        throw new Exception("FAILED to reject: " + message);
    }
    private static Snapshot State(string value)
    {
        return new Snapshot { ModId = "archive-test", ModName = "archive.mod",
            Records = new List<Record> { new Record { Id = "item", Name = "Item", Type = "ITEM",
                Cells = new List<Cell> { new Cell { Layer = "active", Path = "/fields/value",
                    Value = Value.Of("Int32", value), Source = "active" } } } } };
    }
    private static string Prepare(string root, string name)
    { string path = Path.Combine(root, name); Directory.CreateDirectory(path); return path; }
    private static void CompiledSessions(JournalProject project)
    {
        project.BeginSession(); project.Record(State("11")); project.Compile();
        project.BeginSession(); project.Record(State("12")); project.Compile();
        project.BeginSession();
    }
    private static string Catalogue(JournalProject project)
    { return Json.Write(project.ExportRecordings(project.State)); }

    private static void RoundTrip(string root)
    {
        string archiveRoot = Prepare(root, "roundtrip-archives");
        string path = Path.Combine(root, "roundtrip.json"), manifestPath;
        Dictionary<string, byte[]> original;
        string baseline, journal, marker, catalogue, active, head;
        using (var project = JournalProject.Create(path, State("10")))
        {
            CompiledSessions(project);
            project.BeginSession(); project.Record(State("13"));
            active = project.SessionPath; head = project.HeadId;
            original = Directory.GetFiles(Path.Combine(project.DirectoryName, "sessions"))
                .Where(file => file != active).ToDictionary(Path.GetFileName, File.ReadAllBytes);
            string recovery = Path.Combine(project.DirectoryName, "example.recovery");
            File.WriteAllText(recovery, "private recovery bytes");
            baseline = TrackingStorage.HashFile(path); journal = TrackingStorage.HashFile(project.JournalPath);
            marker = TrackingStorage.HashFile(Path.Combine(project.DirectoryName, "project.json"));
            catalogue = Catalogue(project);
            HistoryStorageInventory inventory = project.HistoryInventory();
            Check(inventory.Sessions.Count == 4 && inventory.Sessions.Count(item => item.Eligible) == 3 &&
                inventory.Sessions.Single(item => item.Active).Recordings == 1,
                "inventory distinguishes closed compiled sessions, empty closed originals and the active pending session");
            Check(inventory.BaselineBytes == new FileInfo(path).Length && inventory.OtherBytes >= new FileInfo(recovery).Length &&
                inventory.TotalBytes == new FileInfo(path).Length +
                    Directory.GetFiles(project.DirectoryName, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length),
                "usage separates baseline, journal, session originals and recovery/other storage");
            Check(inventory.Select(null, 1).Count == 2 && inventory.Select(DateTime.UtcNow.AddDays(1), 0).Count == 3 &&
                inventory.Select(DateTime.UtcNow.AddDays(-1), 0).Count == 0 && inventory.Select(null, 99).Count == 0,
                "age and keep-newest guidance only selects eligible sessions and combines both bounds");
            Reject<ArgumentException>(() => inventory.Select(DateTime.Now, null), "local-time age limits");
            Reject<ArgumentOutOfRangeException>(() => inventory.Select(null, -1), "negative retained session counts");
            Reject<InvalidOperationException>(() => project.ArchiveSessions(inventory, new string[0], archiveRoot, CancellationToken.None),
                "empty selection");
            Reject<IOException>(() => project.ArchiveSessions(inventory, new[] { project.SessionId }, archiveRoot, CancellationToken.None),
                "active or pending session archival");
            Reject<InvalidOperationException>(() => project.ArchiveSessions(inventory, new[] { inventory.Sessions[0].SessionId,
                inventory.Sessions[0].SessionId }, archiveRoot, CancellationToken.None), "duplicate selection");
            Reject<InvalidOperationException>(() => project.ArchiveSessions(inventory, new[] { inventory.Sessions[0].SessionId },
                project.DirectoryName, CancellationToken.None), "archive collision with protected tracking storage");
            Reject<InvalidOperationException>(() => project.ArchiveSessions(inventory, new[] { inventory.Sessions[0].SessionId },
                "relative-archive", CancellationToken.None), "relative archive parent paths");
            Reject<InvalidOperationException>(() => project.ArchiveSessions(inventory, new[] { inventory.Sessions[0].SessionId },
                Path.GetPathRoot(archiveRoot).TrimEnd(Path.DirectorySeparatorChar) + "relative-archive", CancellationToken.None),
                "drive-relative archive parent paths");
            HistoryArchiveResult result = project.ArchiveSessions(inventory, inventory.Select(null, 0).Select(item => item.SessionId),
                archiveRoot, CancellationToken.None);
            manifestPath = result.ManifestPath;
            Check(result.Changed == 3 && result.Bytes == inventory.Sessions.Where(item => item.Eligible).Sum(item => item.Bytes),
                "archive result reports exact removed original count and source bytes");
            Check(original.All(item => File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(manifestPath), "sessions", item.Key))
                .SequenceEqual(item.Value)) && File.Exists(active) && Directory.GetFiles(Path.GetDirectoryName(active), "*.jsonl").Length == 1,
                "verified archive retains byte-exact originals and removes only selected closed copies");
            Check(TrackingStorage.HashFile(path) == baseline && TrackingStorage.HashFile(project.JournalPath) == journal &&
                TrackingStorage.HashFile(Path.Combine(project.DirectoryName, "project.json")) == marker &&
                File.ReadAllText(recovery) == "private recovery bytes" && project.HeadId == head && project.PendingCount == 1 &&
                Catalogue(project) == catalogue,
                "archive changes neither baseline, journal, marker, recovery, current head nor original packets/order/pending debt");
            Check(project.HistoryInventory().Sessions.Count == 1 && project.Compile() == 1,
                "live observations are reconciled under the writer lease and subsequent compilation still works");
            HistoryRestorePreview restore = project.PreviewRestoreArchive(manifestPath);
            HistoryArchiveResult restored = project.RestoreArchive(restore, CancellationToken.None);
            Check(restored.Changed == 3 && restored.AlreadyPresent == 0 &&
                original.All(item => File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(active), item.Key)).SequenceEqual(item.Value)),
                "restore re-adds exact missing original bytes after newer journal compilation");
            Check(project.Compile() == 0 && project.HistoryInventory().Sessions.Count == 4 && Catalogue(project) == catalogue &&
                project.SessionPath == active && project.HeadId == head, "restore preserves the active session and evidence without duplication");
            restored = project.RestoreArchive(project.PreviewRestoreArchive(manifestPath), CancellationToken.None);
            Check(restored.Changed == 0 && restored.AlreadyPresent == 3 && restored.Bytes == 0,
                "an explicit repeated restore recognizes unchanged originals rather than overwriting them");
            project.Record(State("14")); Check(project.Compile() == 1, "recording after archive/restore keeps the current session writable");
        }
        using (var reopened = JournalProject.Open(path))
            Check(reopened.EntryCount == 4 && reopened.PendingCount == 0 && reopened.Fingerprint == State("14").Fingerprint() &&
                reopened.ExportRecordings(reopened.State).Count == 4 && reopened.Compile() == 0,
                "cold reopen retains all compiled recordings and valid restored originals");
    }

    private static void PartialOperations(string root)
    {
        foreach (string stage in new[] { "copied", "manifest-flush", "manifest-commit", "manifest-committed", "archive-verified", "removed" })
        {
            string destination = Prepare(root, "failure-" + stage);
            string path = Path.Combine(root, "failure-" + stage + ".json");
            using (var project = JournalProject.Create(path, State("10")))
            {
                CompiledSessions(project);
                HistoryStorageInventory inventory = project.HistoryInventory();
                string before = Catalogue(project), head = project.HeadId;
                Reject<IOException>(() => project.ArchiveSessions(inventory, inventory.Select(null, 0).Select(item => item.SessionId),
                    destination, CancellationToken.None, value => { if (value == stage) throw new IOException("Injected transfer interruption."); }),
                    "explicit partial operation at " + stage);
                string folder = Directory.GetDirectories(destination).Single();
                string manifest = Path.Combine(folder, "manifest.json");
                int remaining = project.HistoryInventory().Sessions.Count;
                Check(Catalogue(project) == before && project.HeadId == head && project.Compile() == 0,
                    "a transfer interruption retains coherent live evidence/head at " + stage);
                if (stage == "copied" || stage == "manifest-flush" || stage == "manifest-commit")
                {
                    Check(!File.Exists(manifest) && remaining == 3, "copy failure creates no restorable manifest and removes no originals");
                    continue;
                }
                Check(File.Exists(manifest) && remaining == (stage == "removed" ? 2 : 3),
                    "a durable complete manifest precedes every original removal");
                HistoryArchiveResult restored = project.RestoreArchive(project.PreviewRestoreArchive(manifest), CancellationToken.None);
                Check(restored.Changed == (stage == "removed" ? 1 : 0) && restored.AlreadyPresent == (stage == "removed" ? 1 : 2) &&
                    project.HistoryInventory().Sessions.Count == 3 && Catalogue(project) == before,
                    "partial archival can explicitly restore its removed subset without overwrites");
                var restorePreview = project.PreviewRestoreArchive(manifest);
                var archivedAgain = project.ArchiveSessions(project.HistoryInventory(), inventory.Select(null, 0).Select(item => item.SessionId),
                    destination, CancellationToken.None);
                Reject<IOException>(() => project.RestoreArchive(project.PreviewRestoreArchive(archivedAgain.ManifestPath),
                    CancellationToken.None, value => { if (value == "restored") throw new IOException("Injected restore interruption."); }),
                    "restore interruption after one committed original");
                Check(project.HistoryInventory().Sessions.Count == 2 && Catalogue(project) == before,
                    "partial restore observes its exact committed originals without replaying evidence twice");
                restored = project.RestoreArchive(restorePreview, CancellationToken.None);
                Check(restored.Changed == 1 && restored.AlreadyPresent == 1 && Catalogue(project) == before,
                    "a validated archive remains usable for retry after an interrupted restore");
            }
            using (var reopened = JournalProject.Open(path))
                Check(reopened.EntryCount == 2 && reopened.PendingCount == 0 && reopened.Compile() == 0,
                    "cold reopen is coherent after interrupted transfers at " + stage);
        }
    }

    private static void Protection(string root)
    {
        string destination = Prepare(root, "protection-archives");
        string path = Path.Combine(root, "protection.json");
        using (var project = JournalProject.Create(path, State("10")))
        {
            CompiledSessions(project);
            HistoryStorageInventory inventory = project.HistoryInventory();
            string first = Path.Combine(project.DirectoryName, "sessions", inventory.Select(null, 0)[0].FileName);
            byte[] original = File.ReadAllBytes(first);
            using (var held = new FileStream(first, FileMode.Open, FileAccess.Read, FileShare.Read))
                Reject<IOException>(() => project.ArchiveSessions(inventory, new[] { inventory.Select(null, 0)[0].SessionId },
                    destination, CancellationToken.None), "a reader denying delete prevents guarded original removal");
            Check(File.ReadAllBytes(first).SequenceEqual(original) && project.HistoryInventory().Sessions.Count == 3,
                "failed exclusive removal leaves original bytes and observations intact");
            Reject<IOException>(() => project.ArchiveSessions(inventory, new[] { inventory.Select(null, 0)[0].SessionId },
                destination, CancellationToken.None, value => { if (value == "before-remove") File.AppendAllText(first, "external edit"); }),
                "source edits after the archive commit are detected by the exclusive removal handle");
            Check(File.ReadAllText(first).EndsWith("external edit"),
                "guarded removal preserves a late external write instead of deleting by a stale path/hash check");
            File.WriteAllBytes(first, original);
            Reject<IOException>(() => project.ArchiveSessions(inventory, new[] { inventory.Select(null, 0)[0].SessionId },
                destination, CancellationToken.None, value => { if (value == "copied") File.AppendAllText(first, "external edit"); }),
                "changed source between archive copy and manifest commit");
            Check(File.ReadAllText(first).EndsWith("external edit") &&
                Directory.GetDirectories(destination).Count(folder => File.Exists(Path.Combine(folder, "manifest.json"))) == 2,
                "external source bytes are preserved and the stale copy never gets a committed manifest");
            File.WriteAllBytes(first, original);
            project.BeginSession(); project.Record(State("13"));
            Reject<IOException>(() => project.ArchiveSessions(inventory, new[] { inventory.Select(null, 0)[0].SessionId },
                destination, CancellationToken.None), "stale history preview after a new recording");
            var current = project.HistoryInventory();
            Check(current.Sessions.Count(item => item.Eligible) == 3 && current.Sessions.Count(item => !item.Eligible) == 1,
                "guidance excludes active recordings while retaining the previous closed empty original");
            project.Compile();
            var cancel = new CancellationTokenSource();
            current = project.HistoryInventory();
            Reject<OperationCanceledException>(() => project.ArchiveSessions(current, current.Select(null, 0).Select(item => item.SessionId),
                destination, cancel.Token, value => { if (value == "removed") cancel.Cancel(); }), "cancellation after a guarded removal");
            string manifestPath = Directory.GetDirectories(destination).Select(folder => Path.Combine(folder, "manifest.json"))
                .Where(File.Exists).Single(file => Json.Read<HistoryArchiveManifest>(File.ReadAllText(file)).Sessions.Count == 3);
            Check(project.HistoryInventory().Sessions.Count == 3 && project.Compile() == 0,
                "cancelled archival reconciles already-removed originals without faulting unchanged journal evidence");
            var manifest = Json.Read<HistoryArchiveManifest>(File.ReadAllText(manifestPath));
            string raw = File.ReadAllText(manifestPath);
            var preview = project.PreviewRestoreArchive(manifestPath);
            File.AppendAllText(manifestPath, " ");
            Reject<IOException>(() => project.RestoreArchive(preview, CancellationToken.None), "manifest changes after explicit restore preview");
            File.WriteAllText(manifestPath, raw);
            manifest.Sessions[0].FileName = "..\\outside.jsonl";
            File.WriteAllText(manifestPath, Json.Write(manifest));
            Reject<InvalidDataException>(() => project.PreviewRestoreArchive(manifestPath), "manifest traversal");
            File.WriteAllText(manifestPath, raw);
            manifest = Json.Read<HistoryArchiveManifest>(raw);
            manifest.Sessions.Add(manifest.Sessions[0]);
            File.WriteAllText(manifestPath, Json.Write(manifest));
            Reject<InvalidDataException>(() => project.PreviewRestoreArchive(manifestPath), "duplicate archive session IDs/filenames");
            File.WriteAllText(manifestPath, raw);
            manifest = Json.Read<HistoryArchiveManifest>(raw);
            manifest.ProjectId = Guid.NewGuid().ToString("N");
            File.WriteAllText(manifestPath, Json.Write(manifest));
            Reject<InvalidDataException>(() => project.PreviewRestoreArchive(manifestPath), "foreign project manifest");
            File.WriteAllText(manifestPath, raw);
            manifest = Json.Read<HistoryArchiveManifest>(raw);
            File.WriteAllText(manifestPath, raw + "{}");
            Reject<InvalidDataException>(() => project.PreviewRestoreArchive(manifestPath), "multiple JSON objects in one archive manifest");
            File.WriteAllText(manifestPath, raw);
            manifest.SchemaVersion = 2;
            File.WriteAllText(manifestPath, Json.Write(manifest));
            Reject<InvalidDataException>(() => project.PreviewRestoreArchive(manifestPath), "unsupported archive schema");
            File.WriteAllText(manifestPath, raw);
            manifest = Json.Read<HistoryArchiveManifest>(raw);
            string archivedFile = Path.Combine(Path.GetDirectoryName(manifestPath), "sessions", manifest.Sessions[0].FileName);
            byte[] archiveBytes = File.ReadAllBytes(archivedFile);
            File.AppendAllText(archivedFile, "damage");
            Reject<IOException>(() => project.PreviewRestoreArchive(manifestPath), "damaged archived payload");
            File.WriteAllBytes(archivedFile, archiveBytes);
            HistoryArchiveSession recordedSession = manifest.Sessions.First(item => item.Recordings > 0);
            string divergentFile = Path.Combine(Path.GetDirectoryName(manifestPath), "sessions", recordedSession.FileName);
            byte[] validBytes = File.ReadAllBytes(divergentFile);
            string[] lines = File.ReadAllLines(divergentFile);
            JournalLine divergent = Json.Read<JournalLine>(lines[1]);
            divergent.Record.EntryId = Guid.NewGuid().ToString("N"); divergent.Hash = Json.StreamHash(divergent.Record);
            lines[1] = Json.Write(divergent);
            File.WriteAllText(divergentFile, String.Join("\n", lines) + "\n");
            recordedSession.Bytes = new FileInfo(divergentFile).Length;
            recordedSession.Sha256 = TrackingStorage.HashFile(divergentFile);
            File.WriteAllText(manifestPath, Json.Write(manifest));
            Reject<InvalidDataException>(() => project.PreviewRestoreArchive(manifestPath),
                "a self-consistent archive branch with valid new checksums but entries absent from the verified main journal");
            File.WriteAllBytes(divergentFile, validBytes); File.WriteAllText(manifestPath, raw);
            string missing = manifest.Sessions.Select(item => Path.Combine(project.DirectoryName, "sessions", item.FileName))
                .Single(file => !File.Exists(file));
            File.WriteAllText(missing, "external restore conflict");
            Reject<IOException>(() => project.PreviewRestoreArchive(manifestPath), "unobserved conflicting restore destination");
            Check(File.ReadAllText(missing) == "external restore conflict", "restore conflict does not overwrite external intent");
            File.Delete(missing);
            HistoryArchiveResult restored = project.RestoreArchive(project.PreviewRestoreArchive(manifestPath), CancellationToken.None);
            Check(restored.Changed == 1 && restored.AlreadyPresent == 2 && project.EntryCount == 3 && project.Compile() == 0,
                "restore remains usable after rejected stale, foreign, damaged and conflicting attempts");
        }
    }

    private static void ColdArchiveAndPending(string root)
    {
        string destination = Prepare(root, "cold-archives");
        string path = Path.Combine(root, "cold.json"), manifestPath, evidence;
        using (var project = JournalProject.Create(path, State("10")))
        {
            CompiledSessions(project);
            project.Record(State("13"));
            project.BeginSession(); project.BeginSession();
            var inventory = project.HistoryInventory();
            Check(inventory.Sessions.Count(item => item.Eligible) == 2 &&
                inventory.Sessions.Count(item => !item.Active && !item.FullyCompiled) == 2,
                "closed pending logs and empty closed sessions whose parent is pending never become age-based candidates");
            evidence = Catalogue(project);
            var result = project.ArchiveSessions(inventory, inventory.Select(null, 0).Select(item => item.SessionId),
                destination, CancellationToken.None);
            manifestPath = result.ManifestPath;
            Check(project.PendingCount == 1 && Catalogue(project) == evidence &&
                project.HistoryInventory().Sessions.Count == 3,
                "archiving older compiled sessions preserves closed pending originals and their empty dependants");
        }
        using (var reopened = JournalProject.Open(path))
        {
            Check(reopened.EntryCount == 3 && reopened.PendingCount == 1 && Catalogue(reopened) == evidence &&
                reopened.Compile() == 1,
                "cold reopen without archived originals replays all evidence and can compile retained pending sources");
            reopened.BeginSession(); reopened.Record(State("14")); reopened.Compile();
            var restored = reopened.RestoreArchive(reopened.PreviewRestoreArchive(manifestPath), CancellationToken.None);
            Check(restored.Changed == 2 && reopened.EntryCount == 4 && reopened.ExportRecordings(reopened.State).Count == 4 &&
                reopened.PendingCount == 0 && reopened.Compile() == 0,
                "cold restore after further recording proves older archive entries against the extended main journal");
        }
        using (var foreign = JournalProject.Create(Path.Combine(root, "foreign.json"), State("10")))
            Reject<InvalidDataException>(() => foreign.PreviewRestoreArchive(manifestPath), "same-mod archives from a different baseline");
    }

    public static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "FcsScribeArchiveTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { RoundTrip(root); PartialOperations(root); Protection(root); ColdArchiveAndPending(root); return passed; }
        finally { Directory.Delete(root, true); }
    }
}
