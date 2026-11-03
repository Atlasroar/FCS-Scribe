using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FcsScribe;

internal static class JournalTests
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
        var state = new Snapshot { ModId = "project", ModName = "project.mod" };
        for (int i = 0; i < 100; i++)
            state.Records.Add(new Record { Id = "item" + i, Type = "ITEM", Name = "Item " + i,
                Cells = new List<Cell> { new Cell { Layer = "active", Path = "/fields/value",
                    Value = Value.Of("Int32", i == 0 ? value : "10"), Source = "active" } } });
        return state;
    }
    public static int Run()
    {
        string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "journal-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string invalid = Path.Combine(directory, "game.mod");
            Reject<InvalidDataException>(() => JournalProject.Create(invalid, State("10")), "mod targets rejected before creating session storage");
            Check(!Directory.Exists(invalid + ".scribe"), "invalid targets leave no storage next to game/mod files");
            string path = Path.Combine(directory, "tracking.json");
            string anchorHash, firstSession, firstSessionId;
            using (var project = JournalProject.Create(path, State("10")))
            {
                anchorHash = TrackingStorage.HashFile(path);
                Reject<IOException>(() => JournalProject.Open(path), "only one Scribe writer can open a project");
                project.BeginSession(); firstSession = project.SessionPath; firstSessionId = project.SessionId;
                Check(Path.GetFileName(firstSession).EndsWith(firstSessionId + ".jsonl"), "timestamped session filenames include unique identities");
                Check(project.Compile() == 0 && !File.Exists(project.JournalPath), "empty compilation creates no incomplete journal");
                Check(project.Record(State("10")).Changes.Count == 0 && File.ReadAllLines(firstSession).Length == 1,
                    "unchanged reviews append nothing to the session");
                project.Record(State("11"));
                Check(project.EntryCount == 1 && project.PendingCount == 1 && !File.Exists(project.JournalPath),
                    "interval entries are durable without touching the main journal");
                JournalLine line = Json.Read<JournalLine>(File.ReadAllLines(firstSession)[1]);
                Check(line.Record.Delta.Updated.Count == 1 && line.Record.Delta.Context.Records.Count == 0 &&
                    !File.ReadAllLines(firstSession)[1].Contains("\"item99\""),
                    "delta entries omit unchanged records, not another full model snapshot");
                Check(line.Record.Packet.Changes.All(x => x.RecordId == "item0"), "packet evidence and delta concern the changed record");
                Check(project.ResumeTracker().Export(State("12")).Changes.Single().Before.Text == "11",
                    "manual and automatic recordings compare against the last durable entry");
                project.Record(State("12"));
                Check(project.PendingCount == 2 && TrackingStorage.HashFile(path) == anchorHash,
                    "subsequent recordings never rewrite the baseline anchor");
                Check(project.Compile() == 2 && project.PendingCount == 0, "compilation consolidates all pending session entries");
                string journalHash = TrackingStorage.HashFile(project.JournalPath);
                Check(project.Compile() == 0 && TrackingStorage.HashFile(project.JournalPath) == journalHash,
                    "repeated compilation is idempotent and does not rewrite the journal");
                Check(File.Exists(firstSession), "compilation never deletes session logs");
                project.Record(State("13"));
                Check(project.PendingCount == 1, "post-compilation recording stays in the session until the next compilation");
            }
            string secondSession;
            using (var reopened = JournalProject.Open(path))
            {
                Check(reopened.State.Fingerprint() == State("13").Fingerprint() && reopened.PendingCount == 1,
                    "reopen recovers both compiled history and uncompiled durable entries");
                reopened.BeginSession(); secondSession = reopened.SessionPath;
                Check(reopened.SessionId != firstSessionId && secondSession != firstSession,
                    "every reopen begins a distinct session even within the same timestamp");
                Packet next = reopened.Record(State("14"));
                Check(next.Changes.Single().Before.Text == "13", "new sessions continue the preceding session's last validated state");
                Check(reopened.Compile() == 2 && reopened.PendingCount == 0, "compilation spans multiple sessions without duplicates");
            }
            File.Delete(firstSession);
            using (var reopened = JournalProject.Open(path))
            {
                Check(reopened.State.Fingerprint() == State("14").Fingerprint(),
                    "compiled sessions can be archived externally without losing journal recovery");
                reopened.BeginSession();
                Snapshot metadata = State("14"); metadata.Dependencies.Add("saved-header.mod");
                Packet packet = reopened.Record(metadata);
                Check(packet.Changes.Count == 0 && reopened.PendingCount == 1,
                    "context-only fingerprint changes are durably replayable");
                Snapshot sources = metadata.Copy(); sources.Records[0].Cells[0].Source = "changed-source";
                reopened.Record(sources);
                Snapshot removed = sources.Copy(); removed.Records.RemoveAt(1);
                removed.Records.Add(new Record { Id = "new", Type = "ITEM", Name = "Created" });
                reopened.Record(removed);
                Check(reopened.State.Fingerprint() == removed.Fingerprint(), "record additions/removals and provenance changes remain exact");
            }
            string damaged;
            using (var reopened = JournalProject.Open(path))
            {
                Check(reopened.State.Records.Any(x => x.Id == "new") && !reopened.State.Records.Any(x => x.Id == "item1") &&
                    reopened.State.Records.Single(x => x.Id == "item0").Cells[0].Source == "changed-source",
                    "replay preserves record presence and source-only edits");
                reopened.BeginSession(); damaged = reopened.SessionPath;
            }
            long validLength = new FileInfo(damaged).Length;
            File.AppendAllText(damaged, "{\"interrupted");
            string damagedHash = TrackingStorage.HashFile(damaged);
            IncompleteJournalException incomplete = null;
            try { using (JournalProject.Open(path)) { } }
            catch (IncompleteJournalException error) { incomplete = error; }
            Check(incomplete != null && incomplete.ValidLength == validLength &&
                TrackingStorage.HashFile(damaged) == damagedHash, "interrupted lines are reported without silent truncation");
            string backup = JournalProject.RecoverTail(path, incomplete);
            Check(File.Exists(backup) && TrackingStorage.HashFile(backup) == damagedHash &&
                new FileInfo(damaged).Length == validLength, "explicit recovery preserves original bytes and removes only the incomplete tail");
            using (var recovered = JournalProject.Open(path))
            {
                Check(recovered.PendingCount == 3 && recovered.Compile() == 3,
                    "recovery retains preceding complete entries and compiles them once");
                recovered.BeginSession();
                string original = File.ReadAllText(recovered.SessionPath);
                string head = recovered.HeadId;
                File.WriteAllText(recovered.SessionPath, "outside edit");
                Reject<IOException>(() => recovered.Record(recovered.State.Copy()), "changed session file rejected even for unchanged reviews");
                Check(recovered.HeadId == head, "failed session validation never advances the baseline");
                File.WriteAllText(recovered.SessionPath, original);
            }
            string main = path + ".scribe" + Path.DirectorySeparatorChar + "journal.jsonl";
            File.AppendAllText(main, "{\"partial-compilation");
            incomplete = null;
            try { using (JournalProject.Open(path)) { } }
            catch (IncompleteJournalException error) { incomplete = error; }
            Check(incomplete != null && incomplete.FilePath == main, "interrupted main-journal compilation is detected");
            JournalProject.RecoverTail(path, incomplete);
            using (var recovered = JournalProject.Open(path))
                Check(recovered.PendingCount == 0 && recovered.Compile() == 0,
                    "recompilation after recovery does not duplicate already committed entries");
            string[] completeLines = File.ReadAllLines(secondSession);
            JournalLine corrupt = Json.Read<JournalLine>(completeLines[1]); corrupt.Hash = new string('0', 64);
            completeLines[1] = Json.Write(corrupt);
            File.WriteAllLines(secondSession, completeLines);
            string corruptHash = TrackingStorage.HashFile(secondSession);
            Reject<InvalidDataException>(() => JournalProject.Open(path), "complete but corrupt entries cannot be skipped as interrupted tails");
            Check(TrackingStorage.HashFile(secondSession) == corruptHash, "corruption rejection never rewrites source history");

            string missingPath = Path.Combine(directory, "lost.json"), missingSession;
            using (var missing = JournalProject.Create(missingPath, State("10")))
            { missing.BeginSession(); missing.Record(State("11")); missingSession = missing.SessionPath; }
            string archived = Path.Combine(directory, "archived-session.jsonl");
            File.Move(missingSession, archived);
            Reject<InvalidDataException>(() => JournalProject.Open(missingPath),
                "deleting the latest uncompiled session is detected by the durable head marker");
            File.Move(archived, missingSession);
            string detached = Path.Combine(directory, "detached.json"); File.Copy(missingPath, detached);
            Reject<InvalidDataException>(() => JournalProject.Open(detached),
                "copying a journal-enabled anchor without its storage folder fails explicitly");
            string oldCheckpoint = Path.Combine(directory, "v1.json");
            TrackingStorage.Save(oldCheckpoint, TrackingDocument.Create(State("10"), null), null);
            string oldHash = TrackingStorage.HashFile(oldCheckpoint);
            using (var compatible = JournalProject.Open(oldCheckpoint))
            {
                compatible.BeginSession(); compatible.Record(State("11"));
                Check(TrackingStorage.HashFile(oldCheckpoint) == oldHash,
                    "previous full-state tracking documents are compatible and never rewritten by session recordings");
            }

            var delta = SnapshotDelta.Between(State("10"), State("11"));
            Check(delta.Apply(State("10")).Fingerprint() == State("11").Fingerprint(), "delta replay reproduces the exact target fingerprint");
            delta.Removed.Add("missing");
            Reject<InvalidDataException>(() => delta.Apply(State("10")), "invalid removals cannot be silently ignored");
            string legacyPath = Path.Combine(directory, "old-ai.json");
            File.WriteAllText(legacyPath, Json.Write(new Tracker(State("9")).Export(State("10"))));
            string legacyHash = TrackingStorage.HashFile(legacyPath);
            LegacyTrackingException legacy = null;
            try { using (JournalProject.Open(legacyPath)) { } }
            catch (LegacyTrackingException error) { legacy = error; }
            Check(legacy != null && TrackingStorage.HashFile(legacyPath) == legacyHash, "legacy packets require explicit conversion");
            using (var converted = JournalProject.ConvertLegacy(legacyPath, State("12"), legacy))
                Check(converted.Fingerprint == State("12").Fingerprint() &&
                    TrackingStorage.HashFile(legacyPath + ".legacy.bak") == legacyHash,
                    "approved conversion preserves legacy bytes and starts from current state");

            foreach (int seconds in PluginSettings.Intervals)
            {
                var schedule = new JournalSchedule(); schedule.Reset(0, seconds);
                Check(seconds == 0 ? !schedule.ReviewDue(Int64.MaxValue) && !schedule.CompilationDue(Int64.MaxValue) :
                    !schedule.ReviewDue(seconds * 1000 - 1) && schedule.ReviewDue(seconds * 1000),
                    "manual/automatic deadline boundary " + seconds);
                if (seconds == 0) continue;
                schedule.Reviewed(1000000);
                Check(!schedule.CompilationDue(1799999) && schedule.CompilationDue(1800000),
                    "recordings do not postpone the thirty-minute compilation deadline " + seconds);
                schedule.Compiled(1800000);
                Check(!schedule.CompilationDue(3599999) && schedule.CompilationDue(3600000),
                    "successful compilation sets one subsequent deadline " + seconds);
            }
            return passed;
        }
        finally { Directory.Delete(directory, true); }
    }
}
