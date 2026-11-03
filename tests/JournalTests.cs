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
            CheckInitialization(directory);
            CheckRangeExports(directory);
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
                Check(reopened.ExportRecordings(reopened.State).Count == 4,
                    "recording ranges recover original evidence from the journal after compiled sessions are archived");
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

    private static LegacyTrackingException Legacy(string path)
    {
        File.WriteAllText(path, Json.Pretty(Json.Write(new Tracker(State("9")).Export(State("10")))));
        try { using (JournalProject.Open(path)) { } }
        catch (LegacyTrackingException error) { return error; }
        throw new Exception("Legacy fixture was not recognized.");
    }

    private static string MarkerPath(string path) { return Path.Combine(path + ".scribe", "project.json"); }

    private static void CheckInitialization(string directory)
    {
        string[] stages = { "marker-write", "marker-flush", "marker-commit", "marker-committed",
            "baseline-write", "baseline-flush", "baseline-commit", "baseline-committed",
            "final-marker-write", "final-marker-flush", "final-marker-commit", "final-marker-committed" };
        foreach (bool conversion in new[] { false, true })
        foreach (string stage in stages)
        {
            string path = Path.Combine(directory, (conversion ? "convert-" : "create-") + stage + ".json");
            LegacyTrackingException legacy = conversion ? Legacy(path) : null;
            Action<string> fail = step => { if (step == stage) throw new IOException("Injected " + step); };
            Reject<IOException>(() => {
                using (conversion ? JournalProject.ConvertLegacy(path, State("12"), legacy, fail) :
                    JournalProject.Create(path, State("12"), fail)) { }
            }, "initialization reports interruption at " + stage + " (legacy=" + conversion + ")");
            bool committed = File.Exists(path) && TrackingStorage.Load(path).Document != null;
            if (conversion)
                Check(TrackingStorage.HashFile(path + ".legacy.bak") == legacy.FileHash &&
                    (committed || TrackingStorage.HashFile(path) == legacy.FileHash),
                    "conversion interruption preserves original bytes and legacy backup at " + stage);
            Check(Directory.GetFiles(path + ".scribe", "*.tmp").Length == 0 &&
                Directory.GetFiles(directory, Path.GetFileName(path) + ".*.tmp").Length == 0,
                "ordinary initialization failures clean only their temporary writes at " + stage);
            if (committed)
            {
                string hash = TrackingStorage.HashFile(path);
                Reject<IOException>(() => JournalProject.Create(path, State("99")),
                    "committed baseline is never replaced by a New retry at " + stage);
                using (var resumed = JournalProject.Open(path))
                {
                    Check(resumed.Fingerprint == State("12").Fingerprint() && TrackingStorage.HashFile(path) == hash,
                        "Resume finishes or accepts committed initialization without rewriting the baseline at " + stage);
                    resumed.BeginSession(); resumed.Record(State("13"));
                }
            }
            else
            {
                using (var retried = conversion ? JournalProject.ConvertLegacy(path, State("14"), legacy) :
                    JournalProject.Create(path, State("14")))
                {
                    Check(retried.Fingerprint == State("14").Fingerprint(),
                        "uncommitted retry starts from current state without an orphan-marker block at " + stage);
                    retried.BeginSession(); retried.Record(State("13"));
                }
            }
            using (var reopened = JournalProject.Open(path))
                Check(reopened.Fingerprint == State("13").Fingerprint() && reopened.EntryCount == 1,
                    "recovered project records and replays normally after " + stage);
            Check(!File.ReadAllText(MarkerPath(path)).Contains("\"Initialization\""),
                "completed markers retain their preexisting serialized contract");
        }

        string denied = Path.Combine(directory, "denied.json");
        Reject<UnauthorizedAccessException>(() => JournalProject.Create(denied, State("10"), step => {
            if (step == "baseline-write") throw new UnauthorizedAccessException("Injected access denial.");
        }), "access-denied failure is explicit after marker persistence");
        using (var retried = JournalProject.Create(denied, State("11")))
            Check(retried.Fingerprint == State("11").Fingerprint(), "retry works after simulated permissions are restored");

        string blocked = Path.Combine(directory, "blocked-target.json");
        Reject<IOException>(() => JournalProject.Create(blocked, State("10"), step => {
            if (step == "baseline-commit") Directory.CreateDirectory(blocked);
        }), "real directory collision interrupts baseline commit");
        Check(Directory.Exists(blocked) && !File.Exists(blocked), "failed commit never deletes a colliding destination");
        Directory.Delete(blocked);
        using (JournalProject.Create(blocked, State("11"))) { }
        Check(true, "same filename retries after the user fixes the target collision");

        string locked = Path.Combine(directory, "locked-conversion.json");
        LegacyTrackingException source = Legacy(locked);
        FileStream held = null;
        try
        {
            Reject<IOException>(() => JournalProject.ConvertLegacy(locked, State("12"), source, step => {
                if (step == "baseline-commit") held = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read);
            }), "real sharing violation interrupts legacy replacement");
        }
        finally { if (held != null) held.Dispose(); }
        Check(TrackingStorage.HashFile(locked) == source.FileHash &&
            TrackingStorage.HashFile(locked + ".legacy.bak") == source.FileHash,
            "sharing failure preserves the legacy source and backup exactly");
        using (JournalProject.ConvertLegacy(locked, State("13"), source)) { }
        Check(true, "legacy conversion retries after the competing reader closes");

        string repeated = Path.Combine(directory, "repeated-initialization.json");
        for (int i = 0; i < 2; i++)
            Reject<IOException>(() => JournalProject.Create(repeated, State("10"), step => {
                if (step == "baseline-flush") throw new IOException("Interrupted again.");
            }), "repeated uncommitted attempts retain recoverable ownership");
        string pendingHash = TrackingStorage.HashFile(MarkerPath(repeated));
        Check(File.Exists(MarkerPath(repeated) + ".bak"), "retry retains the preceding owned initialization marker");
        bool actionable = false;
        try { using (JournalProject.Open(repeated)) { } }
        catch (IOException error) { actionable = error.Message.Contains("retry Start tracking > New"); }
        Check(actionable && TrackingStorage.HashFile(MarkerPath(repeated)) == pendingHash,
            "Resume of an uncommitted initialization explains New retry without mutating its marker");
        using (JournalProject.Create(repeated, State("14"))) { }
        Check(true, "retry accepts only recognizable prior initialization backups");

        CheckInitializationOwnership(directory);
    }

    private static void CheckInitializationOwnership(string directory)
    {
        string path = Path.Combine(directory, "old-orphan.json");
        Directory.CreateDirectory(path + ".scribe");
        string oldId = Guid.NewGuid().ToString("N");
        string old = Json.Write(new JournalMarker { ProjectId = oldId,
            HeadId = oldId, Fingerprint = State("10").Fingerprint() });
        File.WriteAllText(MarkerPath(path), old);
        Reject<InvalidDataException>(() => JournalProject.Create(path, State("10")),
            "unmarked old-version orphan cannot be assumed to belong to a failed attempt");
        Check(File.ReadAllText(MarkerPath(path)) == old && !File.Exists(path),
            "unrecognized preexisting marker remains byte-identical");

        foreach (string artifact in new[] { "journal.jsonl", "journal.jsonl.bak", "sessions", "recovery", "sessions\\old.jsonl",
            "sessions\\unfinished.tmp", "recovery\\old.bak" })
        {
            string root = Path.Combine(directory, "history-" + Guid.NewGuid().ToString("N") + ".json");
            string existing = Path.Combine(root + ".scribe", artifact);
            Directory.CreateDirectory(Path.GetDirectoryName(existing));
            File.WriteAllText(existing, "Existing data must survive.");
            Reject<IOException>(() => JournalProject.Create(root, State("10")), "existing history blocks initialization: " + artifact);
            Check(File.ReadAllText(existing) == "Existing data must survive." && !File.Exists(root),
                "initialization does not remove history: " + artifact);
        }
        foreach (string suffix in new[] { ".bak", ".legacy.bak" })
        {
            string root = Path.Combine(directory, "backup-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(root + suffix, "Unrelated backup.");
            Reject<IOException>(() => JournalProject.Create(root, State("10")), "existing baseline backup blocks New: " + suffix);
            Check(File.ReadAllText(root + suffix) == "Unrelated backup." && !File.Exists(root),
                "existing backup is never claimed or overwritten: " + suffix);
        }
        string changed = Path.Combine(directory, "changed-before-commit.json");
        Reject<IOException>(() => JournalProject.Create(changed, State("10"), step => {
            if (step == "baseline-commit") File.WriteAllText(changed, "External writer.");
        }), "concurrent baseline creation blocks commit");
        Check(File.ReadAllText(changed) == "External writer.", "failed initialization preserves the external destination");

        string markerChanged = Path.Combine(directory, "marker-changed.json");
        Reject<IOException>(() => JournalProject.Create(markerChanged, State("10"), step => {
            if (step == "final-marker-commit") File.WriteAllText(MarkerPath(markerChanged), "External marker.");
        }), "observed marker changes block finalization");
        Check(File.ReadAllText(MarkerPath(markerChanged)) == "External marker." &&
            TrackingStorage.Load(markerChanged).Document != null, "external marker and committed baseline both survive");

        string backupChanged = Path.Combine(directory, "marker-backup-changed.json");
        Reject<InvalidDataException>(() => JournalProject.Create(backupChanged, State("10"), step => {
            if (step == "final-marker-commit") File.WriteAllText(MarkerPath(backupChanged) + ".bak", old);
        }), "unrelated marker backup arriving before commit blocks replacement");
        Check(File.ReadAllText(MarkerPath(backupChanged) + ".bak") == old,
            "a newly observed foreign marker backup is not overwritten");

        string mismatch = Path.Combine(directory, "mismatched-root.json");
        Reject<IOException>(() => JournalProject.Create(mismatch, State("10"), step => {
            if (step == "baseline-committed") throw new IOException("Interrupted after baseline.");
        }), "fixture retains pending initialization after baseline commit");
        TrackingDocument replacement = TrackingDocument.Create(State("11"), null); replacement.JournalVersion = 1;
        File.WriteAllText(mismatch, Json.Write(replacement));
        string mismatchHash = TrackingStorage.HashFile(mismatch), markerHash = TrackingStorage.HashFile(MarkerPath(mismatch));
        Reject<InvalidDataException>(() => JournalProject.Open(mismatch), "unrelated baseline cannot complete a pending initialization");
        Check(TrackingStorage.HashFile(mismatch) == mismatchHash && TrackingStorage.HashFile(MarkerPath(mismatch)) == markerHash,
            "failed recovery preserves mismatched root and marker bytes");

        string backupOnly = Path.Combine(directory, "unclaimed-marker-backup.json");
        Directory.CreateDirectory(backupOnly + ".scribe");
        File.WriteAllText(MarkerPath(backupOnly) + ".bak", old);
        Reject<IOException>(() => JournalProject.Create(backupOnly, State("10")),
            "a lone marker backup is not silently claimed as a failed attempt");
        Check(File.ReadAllText(MarkerPath(backupOnly) + ".bak") == old && !File.Exists(backupOnly),
            "unclaimed marker backup remains byte-identical");

        string conversion = Path.Combine(directory, "conversion-backup-lost.json");
        LegacyTrackingException legacy = Legacy(conversion);
        Reject<IOException>(() => JournalProject.ConvertLegacy(conversion, State("12"), legacy, step => {
            if (step == "baseline-committed") throw new IOException("Interrupted conversion.");
        }), "legacy fixture stops after committing baseline");
        string rootHash = TrackingStorage.HashFile(conversion), intentHash = TrackingStorage.HashFile(MarkerPath(conversion));
        File.Move(conversion + ".legacy.bak", conversion + ".preserved.json");
        Reject<IOException>(() => JournalProject.Open(conversion), "recovery requires the intact original legacy backup");
        Check(TrackingStorage.HashFile(conversion) == rootHash && TrackingStorage.HashFile(MarkerPath(conversion)) == intentHash,
            "missing legacy backup blocks finalization without changing baseline or marker");
        File.Move(conversion + ".preserved.json", conversion + ".legacy.bak");
        using (JournalProject.Open(conversion)) { }
        Check(true, "restoring the exact legacy backup enables recovery");

        string different = Path.Combine(directory, "changed-conversion-source.json");
        legacy = Legacy(different);
        Reject<IOException>(() => JournalProject.ConvertLegacy(different, State("12"), legacy, step => {
            if (step == "baseline-write") throw new IOException("Uncommitted conversion.");
        }), "legacy fixture retains an uncommitted source identity");
        string originalBackup = TrackingStorage.HashFile(different + ".legacy.bak");
        intentHash = TrackingStorage.HashFile(MarkerPath(different));
        File.WriteAllText(different, Json.Write(new Tracker(State("9")).Export(State("11"))));
        string externalHash = TrackingStorage.HashFile(different);
        LegacyTrackingException external = null;
        try { using (JournalProject.Open(different)) { } }
        catch (LegacyTrackingException error) { external = error; }
        Reject<IOException>(() => JournalProject.ConvertLegacy(different, State("14"), external),
            "changed legacy source cannot claim another attempt's initialization");
        Check(TrackingStorage.HashFile(different) == externalHash &&
            TrackingStorage.HashFile(MarkerPath(different)) == intentHash &&
            TrackingStorage.HashFile(different + ".legacy.bak") == originalBackup,
            "source mismatch preserves external source, original legacy backup and pending marker");
    }

    private static void CheckRangeExports(string directory)
    {
        string path = Path.Combine(directory, "range.json");
        string originalPacket, originalEntry, firstSession;
        Packet fullRange;
        using (var project = JournalProject.Create(path, State("10")))
        {
            project.BeginSession(); firstSession = project.SessionId;
            Check(project.ExportRecordings(State("10")).Count == 0, "empty tracking has no invented export evidence");
            string head = project.HeadId;
            var pending = project.ExportRecordings(State("11"));
            Check(pending.Count == 1 && pending[0].Kind == "pending" && project.HeadId == head && project.EntryCount == 0,
                "pending edits are selectable without being recorded");
            Packet unsaved = RangeExport.Create(project.ProjectId, pending, 0, 0, State("11"));
            Check(unsaved.SchemaVersion == 3 && unsaved.Changes.Single().Id == pending[0].EntryId + ":change-000001",
                "range schema namespaces change IDs by stable entry identity");
            Check(RangeExport.Create(project.ProjectId, project.ExportRecordings(State("11")), 0, 0, State("11")).BatchId == unsaved.BatchId,
                "re-exporting unchanged pending edits preserves range identity");
            string aiPath = Path.Combine(directory, "range-ai.json");
            File.WriteAllText(aiPath, Json.Write(unsaved));
            TrackingDocument.ValidatePacket(Json.Read<Packet>(File.ReadAllText(aiPath)));
            Check(true, "schema-3 ranges round-trip and validate their original evidence");
            Reject<InvalidDataException>(() => TrackingStorage.Load(aiPath), "range handoffs cannot be mistaken for tracking baselines");
            Packet first = project.Record(State("11"));
            originalPacket = Json.Write(first); originalEntry = project.HeadId;
            Check(!originalPacket.Contains("\"Recordings\"") && !originalPacket.Contains("\"ProjectId\"") && first.SchemaVersion == 2,
                "new optional range fields do not change schema-2 journal serialization");
            project.Record(State("12"));
            Check(project.Compile() == 2, "range fixture contains compiled ticks");
            project.Record(State("10"));
            Snapshot context = State("10"); context.Warnings.Add("Historical context warning.");
            context.Workshop = new WorkshopMetadata { Author = "Context-only author" };
            project.Record(context);
            Check(project.LastEvidence.SourceFingerprint != context.Fingerprint(),
                "context-only updates reproduce the former latest-batch export gap");
            project.Record(context.Copy());
            Check(project.EntryCount == 4, "unchanged reviews are absent from selectable history");
            project.BeginSession();
            Snapshot latest = context.Copy(); latest.Records[0].Cells[0].Value = Value.Of("Int32", "13");
            project.Record(latest);
            var choices = project.ExportRecordings(latest);
            Check(choices.Count == 5 && choices[0].SessionId == firstSession && choices[4].SessionId != firstSession,
                "range choices include several ticks and multiple sessions exactly once");
            Check(choices[3].Packet.Changes.Count == 0 && Json.Write(choices[0].Packet) == originalPacket &&
                choices[0].EntryId == originalEntry, "choices preserve original identities and context-only recordings");
            string[] files = Directory.GetFiles(path + ".scribe", "*", SearchOption.AllDirectories)
                .Where(x => Path.GetFileName(x) != "writer.lock").Concat(new[] { path }).ToArray();
            var hashes = files.ToDictionary(x => x, TrackingStorage.HashFile);
            head = project.HeadId; int count = project.PendingCount;
            fullRange = RangeExport.Create(project.ProjectId, choices, 0, 4, latest);
            Check(fullRange.Recordings.Count == 5 && fullRange.Changes.Count == 4 &&
                fullRange.Changes.Select(x => x.Id).Distinct().Count() == 4,
                "all ticks accumulate with no colliding per-batch change IDs or compiled/session duplicates");
            Check(fullRange.Changes[2].Before.Text == "12" && fullRange.Changes[2].After.Text == "10",
                "reversals are retained as chronological evidence, not collapsed to a net diff");
            Packet historical = RangeExport.Create(project.ProjectId, choices, 0, 1, latest);
            Check(historical.Changes.Count == 2 && historical.Recordings.Last().Packet.SourceFingerprint != historical.SourceFingerprint &&
                historical.SourceFingerprint == latest.Fingerprint(), "historical ranges bind to current state rather than their old endpoint");
            Packet afterContext = RangeExport.Create(project.ProjectId, choices, 0, 3, latest);
            Check(afterContext.Changes.Count == 3 && afterContext.Recordings.Last().Packet.Changes.Count == 0,
                "a context-only endpoint no longer strands earlier gameplay evidence");
            Reject<InvalidOperationException>(() => RangeExport.Create(project.ProjectId, choices, 3, 3, latest),
                "context-only ranges are explicitly rejected as record documentation");
            Reject<InvalidDataException>(() => RangeExport.Create(project.ProjectId, choices, -1, 1, latest), "negative range rejected");
            Reject<InvalidDataException>(() => RangeExport.Create(project.ProjectId, choices, 2, 1, latest), "reversed range rejected");
            Reject<InvalidDataException>(() => RangeExport.Create(project.ProjectId, choices, 0, 5, latest), "out-of-bounds range rejected");
            NoteDraft draft = Json.Read<NoteDraft>(Json.Write(historical.ResponseTemplate));
            foreach (NoteEntry entry in draft.Entries) entry.Text = "Documented historical observations.";
            DraftValidation.Check(historical, draft, latest, id => "");
            Check(true, "complete historical evidence is valid against the current editor state");
            draft.Entries[0].ChangeIds.RemoveAt(0);
            Reject<InvalidDataException>(() => DraftValidation.Check(historical, draft, latest, id => ""),
                "S01 full-coverage validation also applies to accumulated ranges");
            draft = Json.Read<NoteDraft>(Json.Write(historical.ResponseTemplate));
            foreach (NoteEntry entry in draft.Entries) entry.Text = "Documented historical observations.";
            Reject<InvalidOperationException>(() => DraftValidation.Check(historical, draft, context, id => ""),
                "gameplay changes stale historical-range drafts");
            Snapshot changedContext = latest.Copy(); changedContext.Workshop.Author = "Later author";
            Reject<InvalidOperationException>(() => DraftValidation.Check(historical, draft, changedContext, id => ""),
                "Workshop-only changes still stale range drafts");
            Check(hashes.All(x => TrackingStorage.HashFile(x.Key) == x.Value) && project.HeadId == head &&
                project.PendingCount == count, "range selection/building never advances, compiles or rewrites any history");
            Check(Json.Write(choices[0].Packet) == originalPacket && Json.Write(first) == originalPacket,
                "qualified range citations never rewrite original batch IDs or evidence");
            Snapshot removed = latest.Copy(); removed.Records.RemoveAt(0);
            Packet deletionTarget = RangeExport.Create(project.ProjectId, choices, 0, 1, removed);
            Check(deletionTarget.ResponseTemplate.Entries.Single().TargetRecordId == "",
                "historical targets deleted before export require an explicit surviving note target");
            Snapshot laterPending = latest.Copy(); laterPending.Records[0].Name += " later";
            var laterChoices = project.ExportRecordings(laterPending);
            Reject<InvalidDataException>(() => RangeExport.Create(project.ProjectId, laterChoices, 0, laterChoices.Count - 1, latest),
                "a selected unrecorded observation must still match export-time state");
            Packet mixed = RangeExport.Create(project.ProjectId, laterChoices, 2, laterChoices.Count - 1, laterPending);
            Check(mixed.Recordings.Last().Kind == "pending" && project.HeadId == head,
                "a range may include both saved recordings and explicitly unrecorded edits without advancing history");
            Packet altered = Json.Read<Packet>(Json.Write(fullRange));
            altered.Changes[0].After.Text = "invented";
            altered.BatchId = Json.Hash(altered.BaselineId + ":" + Json.Write(altered.Changes));
            altered.ResponseTemplate.BatchId = altered.BatchId;
            Reject<InvalidDataException>(() => TrackingDocument.ValidatePacket(altered), "flattened evidence must match original recording packets");
            altered = Json.Read<Packet>(Json.Write(fullRange)); altered.Recordings[0].Packet.BatchId = new string('0', 64);
            Reject<InvalidDataException>(() => TrackingDocument.ValidatePacket(altered), "original batch identity is validated");
            altered = Json.Read<Packet>(Json.Write(fullRange)); altered.Recordings[1].EntryId = altered.Recordings[0].EntryId;
            Reject<InvalidDataException>(() => TrackingDocument.ValidatePacket(altered), "duplicate original entry IDs rejected");
            altered = Json.Read<Packet>(Json.Write(fullRange)); altered.Recordings[0].Packet = fullRange;
            Reject<InvalidDataException>(() => TrackingDocument.ValidatePacket(altered), "nested range packets are not recursively accepted");
            altered = Json.Read<Packet>(Json.Write(fullRange)); altered.ProjectId = Guid.NewGuid().ToString("N");
            Reject<InvalidDataException>(() => TrackingDocument.ValidatePacket(altered), "range identity binds to project and ordered recording selection");
            altered = Json.Read<Packet>(Json.Write(fullRange)); altered.Recordings[0].SessionId = null;
            Reject<InvalidDataException>(() => TrackingDocument.ValidatePacket(altered), "recorded evidence requires its original session identity");
            altered = Json.Read<Packet>(Json.Write(fullRange)); altered.Recordings[0].Packet.Context.ModId = "other-project";
            Reject<InvalidDataException>(() => TrackingDocument.ValidatePacket(altered), "recordings from another project cannot be mixed into a range");
            Check(fullRange.Instructions.Contains("not a net diff") && fullRange.Instructions.Contains("CURRENT editor state"),
                "AI instructions distinguish observations and present-state validation");
            choices[0].Packet.Changes[0].After.Text = "outside mutation";
            Check(Json.Write(project.ExportRecordings(latest)[0].Packet) == originalPacket,
                "returned choices cannot mutate the project's validated history");
            string sessionText = File.ReadAllText(project.SessionPath);
            File.AppendAllText(project.SessionPath, "outside edit");
            Reject<IOException>(() => project.ExportRecordings(latest), "exports reject sources changed outside Scribe");
            File.WriteAllText(project.SessionPath, sessionText);
        }
        using (var reopened = JournalProject.Open(path))
        {
            reopened.BeginSession();
            var choices = reopened.ExportRecordings(reopened.State);
            Packet restored = RangeExport.Create(reopened.ProjectId, choices, 0, choices.Count - 1, reopened.State);
            Check(choices.Count == 5 && restored.BatchId == fullRange.BatchId &&
                Json.Write(restored.Changes) == Json.Write(fullRange.Changes),
                "restart restores the same accumulated range identity and evidence without recording");
            Check(Json.Write(choices[0].Packet) == originalPacket && choices[0].EntryId == originalEntry,
                "original packet identities survive restart and compilation");
            string compiledSession = Directory.GetFiles(Path.Combine(path + ".scribe", "sessions"), "*.jsonl")
                .First(x => File.ReadAllLines(x).Length > 1);
            Check(reopened.PendingCount == 3 && File.Exists(compiledSession), "range exports do not silently compile retained sessions");
        }
        string checkpoint = Path.Combine(directory, "range-checkpoint.json");
        TrackingDocument document = TrackingDocument.Create(State("10"), null);
        Packet oldPacket = document.ResumeTracker().Export(State("11"));
        document = document.Next(State("11"), oldPacket);
        TrackingStorage.Save(checkpoint, document, null);
        using (var previous = JournalProject.Open(checkpoint))
        {
            previous.BeginSession(); previous.Record(State("12"));
            var choices = previous.ExportRecordings(previous.State);
            Packet packet = RangeExport.Create(previous.ProjectId, choices, 0, choices.Count - 1, previous.State);
            Check(choices.Count == 2 && choices[0].Kind == "checkpoint" && Json.Write(choices[0].Packet) == Json.Write(oldPacket) &&
                packet.Changes.Count == 2, "previous checkpoint histories remain selectable alongside new session recordings");
        }
        string legacyPath = Path.Combine(directory, "range-imported.json");
        Packet legacyPacket = new Tracker(State("9")).Export(State("10"));
        legacyPacket.SchemaVersion = 1;
        File.WriteAllText(legacyPath, Json.Write(legacyPacket));
        var legacy = new LegacyTrackingException(legacyPacket, TrackingStorage.HashFile(legacyPath));
        using (var converted = JournalProject.ConvertLegacy(legacyPath, State("10"), legacy))
        {
            converted.BeginSession();
            var choices = converted.ExportRecordings(converted.State);
            Packet packet = RangeExport.Create(converted.ProjectId, choices, 0, 0, converted.State);
            Check(choices.Single().Kind == "imported" && packet.Recordings[0].Packet.SchemaVersion == 1,
                "converted legacy evidence is explicitly labelled and remains exportable");
        }
    }
}
