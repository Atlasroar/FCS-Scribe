using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FcsScribe;

internal static class TrackingTests
{
    private static int passed;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAILED: " + message);
        passed++;
    }

    private static void Reject<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (Exception error) { if (error is T) { passed++; return; } throw; }
        throw new Exception("FAILED to reject: " + message);
    }

    private static Snapshot State(string value)
    {
        return new Snapshot {
            ModId = "project", ModName = "project.mod",
            Records = new List<Record> { new Record { Id = "item", Type = "ITEM", Name = "Item",
                Cells = new List<Cell> { new Cell { Layer = "active", Path = "/fields/value", Value = Value.Of("Int32", value) } } } }
        };
    }

    public static int Run()
    {
        string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tracking-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "tracking.json");
            TrackingDocument first = TrackingDocument.Create(State("10"), null);
            string hash = TrackingStorage.Save(path, first, null);
            LoadedTracking loaded = TrackingStorage.Load(path);
            Check(loaded.Legacy == null && loaded.FileHash == hash && loaded.Document.StateFingerprint == first.StateFingerprint,
                "new checkpoint streams to disk and validates on load");
            Check(loaded.Document.ResumeTracker().Export(State("10")).Changes.Count == 0, "resumed baseline is saved state");
            Packet packet = first.ResumeTracker().Export(State("11"));
            Check(loaded.Document.ResumeTracker().Export(State("11")).BatchId == packet.BatchId,
                "resume retains batch identity for uncheckpointed edits");
            TrackingDocument second = first.Next(State("11"), packet);
            string hash2 = TrackingStorage.Save(path, second, hash);
            Check(File.Exists(path + ".bak") && TrackingStorage.HashFile(path + ".bak") == hash,
                "atomic replacement keeps the previous complete checkpoint");
            LoadedTracking reopened = TrackingStorage.Load(path);
            Check(reopened.Document.Entries.Count == 1 && reopened.Document.ResumeTracker().Export(State("11")).Changes.Count == 0,
                "successful save advances baseline exactly once");
            Check(reopened.Document.LatestEvidence(packet.SourceFingerprint).BatchId == packet.BatchId,
                "checkpointed evidence remains available for manual AI export");
            Packet nextPacket = reopened.Document.ResumeTracker().Export(State("12"));
            Check(nextPacket.Changes.Single().Before.Text == "11" && nextPacket.Changes.Single().After.Text == "12",
                "next interval compares against last validated entry, not session start");
            TrackingDocument third = reopened.Document.Next(State("12"), nextPacket);
            TrackingStorage.Save(path, third, hash2);
            Check(TrackingStorage.Load(path).Document.Entries.Count == 2, "multiple checkpoints append history in the same file");
            Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "successful saves leave no temporary files");
            TrackingDocument corrupt = Json.Read<TrackingDocument>(Json.Write(third));
            corrupt.State.Records[0].Cells[0].Value.Text = "999";
            Reject<InvalidDataException>(corrupt.Validate, "snapshot tampering is rejected");
            corrupt = Json.Read<TrackingDocument>(Json.Write(third));
            corrupt.Entries[1].PreviousFingerprint = new string('0', 64);
            Reject<InvalidDataException>(corrupt.Validate, "broken history chain is rejected");
            corrupt = Json.Read<TrackingDocument>(Json.Write(third));
            corrupt.Entries[0].Packet.Changes[0].After.Text = "777";
            Reject<InvalidDataException>(corrupt.Validate, "changed evidence with stale batch hash is rejected");
            Snapshot other = State("11"); other.ModId = "other-project";
            Reject<InvalidOperationException>(delegate { first.ResumeTracker().Export(other); }, "wrong mod cannot resume");
            Snapshot reordered = State("11"); reordered.LoadOrder.Add("different.mod");
            Reject<InvalidOperationException>(delegate { first.ResumeTracker().Export(reordered); }, "changed load order needs a new baseline");
            Packet legacy = new Tracker(State("9")).Export(State("10"));
            legacy.SchemaVersion = 1;
            string oldPath = Path.Combine(directory, "legacy.json");
            File.WriteAllText(oldPath, Json.Pretty(Json.Write(legacy)));
            byte[] original = File.ReadAllBytes(oldPath);
            LoadedTracking old = TrackingStorage.Load(oldPath);
            Check(old.Legacy.SchemaVersion == 1 && old.Document == null, "legacy AI exports are distinguished from checkpoints");
            TrackingDocument migrated = TrackingDocument.Create(State("12"), old.Legacy);
            TrackingStorage.PreserveLegacy(oldPath, old.FileHash);
            string migratedHash = TrackingStorage.Save(oldPath, migrated, old.FileHash);
            Check(File.ReadAllBytes(oldPath + ".legacy.bak").SequenceEqual(original) &&
                migrated.ImportedEvidence.Count == 1 && migrated.ResumeTracker().Export(State("12")).Changes.Count == 0,
                "legacy conversion preserves bytes/evidence and starts a fresh current-state baseline");
            Packet later = migrated.ResumeTracker().Export(State("13"));
            TrackingStorage.Save(oldPath, migrated.Next(State("13"), later), migratedHash);
            Check(File.ReadAllBytes(oldPath + ".legacy.bak").SequenceEqual(original),
                "later checkpoint saves never replace the original legacy backup");
            Reject<InvalidOperationException>(delegate { TrackingDocument.Create(other, legacy); }, "wrong-mod legacy evidence rejected");
            Reject<IOException>(delegate { TrackingStorage.Save(path, first, null); }, "new sessions do not overwrite existing JSON");
            string before = TrackingStorage.HashFile(path);
            File.WriteAllText(path, "external edit");
            Reject<IOException>(delegate { TrackingStorage.Save(path, third, before); }, "external edits are not silently overwritten");
            Check(File.ReadAllText(path) == "external edit" && Directory.GetFiles(directory, "*.tmp").Length == 0,
                "failed save retains external content and removes temporary files");
            Reject<InvalidDataException>(delegate { TrackingStorage.Save(Path.Combine(directory, "game.mod"), first, null); },
                "mod files cannot be checkpoint targets");
            Reject<DirectoryNotFoundException>(delegate {
                TrackingStorage.Save(Path.Combine(directory, "missing", "tracking.json"), first, null);
            }, "unwritable/missing destination is reported");
            foreach (int seconds in PluginSettings.Intervals)
            {
                var settings = new PluginSettings { ReviewSeconds = seconds };
                settings.Validate();
                Check(settings.DirectoryFor(directory) == directory, "interval " + seconds + " defaults to active mod directory");
            }
            foreach (int seconds in new[] { -1, 1, 29, 31, 120 })
                Reject<InvalidDataException>(delegate { new PluginSettings { ReviewSeconds = seconds }.Validate(); },
                    "unsupported interval " + seconds);
            string custom = Path.Combine(directory, "custom"); Directory.CreateDirectory(custom);
            var configured = new PluginSettings { ReviewSeconds = 300, ExportDirectory = custom };
            string settingsPath = Path.Combine(directory, "settings.json");
            TrackingStorage.WriteAtomic(settingsPath, configured, null);
            PluginSettings saved = PluginSettings.Load(settingsPath);
            Check(saved.ReviewSeconds == 300 && saved.DirectoryFor(directory) == custom, "settings persist interval and custom output folder");
            Reject<InvalidDataException>(delegate { new PluginSettings { ExportDirectory = "relative" }.Validate(); }, "relative settings paths rejected");
            Reject<InvalidDataException>(delegate { new PluginSettings { ExportDirectory = @"C:exports" }.Validate(); }, "drive-relative settings paths rejected");
            Reject<InvalidDataException>(delegate { new PluginSettings { ExportDirectory = @"\exports" }.Validate(); }, "root-relative settings paths rejected");
            Check(PluginSettings.Load(Path.Combine(directory, "absent.json")).ReviewSeconds == 0, "first launch defaults to manual review");
            File.WriteAllText(settingsPath, "{}");
            Reject<SerializationException>(delegate { PluginSettings.Load(settingsPath); }, "missing required settings do not silently become defaults");
            CheckSettings(directory);
            CheckExports(directory);
            return passed;
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CheckExports(string directory)
    {
        string root = Path.Combine(directory, "export-protection");
        Directory.CreateDirectory(root);
        string anchor = Path.Combine(root, "inactive.json");
        Packet range;
        using (var project = JournalProject.Create(anchor, State("10")))
        {
            project.BeginSession();
            project.Record(State("11"));
            project.Compile();
            var recordings = project.ExportRecordings(State("11"));
            range = RangeExport.Create(project.ProjectId, recordings, 0, recordings.Count - 1, State("11"));
        }
        string standalone = Path.Combine(root, "standalone.json");
        TrackingStorage.Save(standalone, TrackingDocument.Create(State("10"), null), null);
        File.Copy(standalone, standalone + ".bak");
        File.Copy(standalone, standalone + ".legacy.bak");
        var protectedPaths = new[] { anchor, anchor.ToUpperInvariant(), standalone, standalone + ".bak",
            standalone + ".legacy.bak", anchor + ".bak", anchor + ".legacy.bak",
            Path.Combine(anchor + ".scribe", "project.json"),
            Path.Combine(anchor + ".scribe", "project.json.bak"),
            Path.Combine(anchor + ".scribe", "journal.jsonl"),
            Path.Combine(anchor + ".scribe", "sessions", "new.json"),
            Path.Combine(anchor + ".scribe", "recovery", "new.json"), anchor + ".scribe",
            Path.Combine(root, "missing.json.scribe", "new.json") };
        var hashes = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(x => x, TrackingStorage.HashFile);
        foreach (string path in protectedPaths)
            Reject<InvalidOperationException>(() => ExportStorage.ObserveTarget(path, null),
                "inactive tracking storage is protected without an open project: " + path);
        Check(hashes.All(x => TrackingStorage.HashFile(x.Key) == x.Value) &&
            Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length == hashes.Count,
            "rejected inactive anchors, backups and storage retain every original byte and file");
        string pending = Path.Combine(root, "pending.json");
        Directory.CreateDirectory(pending + ".scribe");
        Reject<InvalidOperationException>(() => ExportStorage.WriteJson(pending, new WorkshopExport(), null),
            "an orphaned initialization folder reserves its missing anchor");
        string active = Path.Combine(root, "active.json");
        Reject<InvalidOperationException>(() => ExportStorage.ObserveTarget(active, active.ToUpperInvariant()),
            "the active anchor stays protected even if externally deleted");
        string suspicious = Path.Combine(root, "inspect.json");
        foreach (string text in new[] {
            "{\"Format\":\"FCS-Scribe tracking project\",\"SchemaVersion\":999}",
            "{\"Fo\\u0072mat\":\"FCS-Scribe tracking project\"}",
            "{\"Format\":\"ordinary\",\"Format\":\"FCS-Scribe tracking project\"}",
            "{\"Format\":\"FCS-Scribe session storage\"}",
            "{\"Format\":\"FCS-Scribe session\"}", "{\"Format\":\"FCS-Scribe journal entry\"}" })
        {
            File.WriteAllText(suspicious, text);
            Reject<InvalidOperationException>(() => ExportStorage.WriteJson(suspicious, new WorkshopExport(), null),
                "recognition does not require valid schema, canonical spelling or adjacent storage");
            Check(File.ReadAllText(suspicious) == text, "recognized tracking JSON was not overwritten");
        }
        foreach (string text in new[] { "", "{", "null", "[]", "{} trailing", "{}{}",
            "{\"Format\":{}}", "{\"ordinary\":true" })
        {
            File.WriteAllText(suspicious, text);
            Reject<InvalidDataException>(() => ExportStorage.WriteJson(suspicious, new WorkshopExport(), null),
                "unclassifiable existing JSON requires a new filename: " + text);
            Check(File.ReadAllText(suspicious) == text, "unclassifiable destination retained exact bytes");
        }
        Packet packet = new Tracker(State("10")).Export(State("11"));
        foreach (string text in new[] { Json.Write(packet), Json.Write(range), Json.Write(new WorkshopExport()),
            Json.Write(DraftWorkspace.Create(packet, "unfinished")), "{\"Format\":\"ordinary\",\"nested\":{\"Format\":\"FCS-Scribe tracking project\"}}" })
        {
            File.WriteAllText(suspicious, text);
            string backup = suspicious + ".bak";
            File.WriteAllText(backup, Json.Write(TrackingDocument.Create(State("10"), null)));
            string backupHash = TrackingStorage.HashFile(backup);
            ExportStorage.WriteJson(suspicious, new WorkshopExport(), null);
            Check(Json.Read<WorkshopExport>(File.ReadAllText(suspicious)).SchemaVersion == 1 &&
                File.ReadAllText(suspicious).Contains(Environment.NewLine) && TrackingStorage.HashFile(backup) == backupHash,
                "valid existing exports/ordinary JSON remain replaceable, formatted, without touching neighboring backups");
        }
        string lockedHash = TrackingStorage.HashFile(suspicious);
        using (var locked = new FileStream(suspicious, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Reject<IOException>(() => ExportStorage.WriteJson(suspicious, packet, null),
                "unreadable destinations fail explicitly before writing");
        Check(TrackingStorage.HashFile(suspicious) == lockedHash, "unreadable destination retained exact bytes");
        string changed = Json.Write(packet) + " ";
        Reject<IOException>(() => ExportStorage.WriteJson(suspicious, new WorkshopExport(), null, step => {
            if (step == "flush") File.WriteAllText(suspicious, changed);
        }), "a concurrent ordinary destination change is not overwritten");
        Check(File.ReadAllText(suspicious) == changed, "concurrent content remains intact");
        string tracking = Json.Write(TrackingDocument.Create(State("10"), null));
        Reject<InvalidOperationException>(() => ExportStorage.WriteJson(suspicious, packet, null, step => {
            if (step == "flush") File.WriteAllText(suspicious, tracking);
        }), "a destination converted to tracking after observation is reclassified before commit");
        Check(File.ReadAllText(suspicious) == tracking, "concurrent tracking conversion retained exact bytes");
        string race = Path.Combine(root, "race.json");
        Reject<InvalidOperationException>(() => ExportStorage.WriteJson(race, packet, null, step => {
            if (step == "flush") Directory.CreateDirectory(race + ".scribe");
        }), "newly reserved storage cancels export before creating its anchor");
        Check(!File.Exists(race) && Directory.GetFiles(root, "*.tmp").Length == 0,
            "failed exports leave no destination or temporary files");
        string valid = Path.Combine(root, "inactive.json.scribe-copy");
        Directory.CreateDirectory(valid);
        ExportStorage.WriteJson(Path.Combine(valid, "normal.json"), packet, null);
        Check(File.Exists(Path.Combine(valid, "normal.json")), "a similarly prefixed sibling is not tracking storage");
        foreach (string stepToFail in new[] { "write", "flush", "commit" })
        {
            string faulted = Path.Combine(root, "fault-" + stepToFail + ".json");
            foreach (bool exists in new[] { false, true })
            {
                if (exists) File.WriteAllText(faulted, Json.Write(new WorkshopExport()));
                string before = exists ? TrackingStorage.HashFile(faulted) : null;
                Reject<IOException>(() => ExportStorage.WriteJson(faulted, packet, null, step => {
                    if (step == stepToFail) throw new IOException("Injected export " + step + " failure.");
                }), "precommit export failure is explicit: " + stepToFail);
                Check((exists ? TrackingStorage.HashFile(faulted) == before : !File.Exists(faulted)) &&
                    Directory.GetFiles(root, "*.tmp").Length == 0,
                    "precommit failure preserves an existing destination or leaves a new one absent");
            }
        }
        File.WriteAllText(suspicious, Json.Write(packet), System.Text.Encoding.Unicode);
        ExportStorage.WriteJson(suspicious, new WorkshopExport(), null);
        Check(Json.Read<WorkshopExport>(File.ReadAllText(suspicious)).SchemaVersion == 1,
            "a valid BOM-marked UTF-16 destination remains replaceable");
        File.WriteAllText(suspicious, Json.Write(range), new System.Text.UTF8Encoding(true));
        ExportStorage.WriteJson(suspicious, new WorkshopExport(), null);
        Check(Json.Read<WorkshopExport>(File.ReadAllText(suspicious)).SchemaVersion == 1,
            "a valid BOM-marked UTF-8 range export remains replaceable");
        Reject<InvalidOperationException>(() => ExportStorage.ObserveTarget(
            Path.Combine(anchor + ".scribe.", "new.json"), null), "trailing-dot folder aliases cannot bypass reserved storage");
        Reject<NotSupportedException>(() => ExportStorage.ObserveTarget(suspicious + ":export.json", null),
            "alternate data stream spellings are not export locations");
    }

    private static void CheckSettings(string directory)
    {
        string path = Path.Combine(directory, "recovery-settings.json");
        LoadedSettings absent = SettingsStorage.Load(path);
        Check(absent.Error == null && absent.Value.ReviewSeconds == 0 && absent.FileHash == null &&
            !File.Exists(path), "only a missing settings file yields unwritten first-launch defaults");
        string[] invalid = { "", "{", "null", "{}", "{\"SchemaVersion\":1}",
            "{\"ReviewSeconds\":30}", "{\"SchemaVersion\":2,\"ReviewSeconds\":0}",
            "{\"SchemaVersion\":1,\"ReviewSeconds\":42}",
            "{\"SchemaVersion\":1,\"ReviewSeconds\":30,\"ExportDirectory\":\"relative\"}",
            "{\"SchemaVersion\":1,\"ReviewSeconds\":30,\"ExportDirectory\":\"C:\\\\bad\\u0000path\"}" };
        foreach (string text in invalid)
        {
            File.WriteAllText(path, text);
            LoadedSettings loaded = SettingsStorage.Load(path);
            Check(loaded.Error != null && loaded.Value == null && loaded.FileHash == TrackingStorage.HashFile(path) &&
                File.ReadAllText(path) == text, "invalid settings expose an error and exact observed hash without writing: " + text);
        }
        File.WriteAllText(path, Json.Write(new PluginSettings {
            ReviewSeconds = 30, ExportDirectory = Path.Combine(directory, "unavailable-custom") }));
        Check(SettingsStorage.Load(path).Error is DirectoryNotFoundException,
            "a missing configured directory is a recovery error, not a missing settings file");
        Reject<InvalidDataException>(() => new PluginSettings { ExportDirectory = "C:\\bad\0path" }.Validate(),
            "invalid path characters produce an explicit settings validation error");
        File.WriteAllBytes(path, new byte[] { 239, 187, 191, 123, 32, 13, 10 });
        byte[] original = File.ReadAllBytes(path);
        LoadedSettings broken = SettingsStorage.Load(path);
        var repaired = new PluginSettings { ReviewSeconds = 60 };
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            LoadedSettings blocked = SettingsStorage.Load(path);
            Check(blocked.Error is IOException && blocked.Value == null && blocked.FileHash == null,
                "actual sharing denial is recovery with unknown bytes, not first launch");
            Reject<IOException>(() => SettingsStorage.Save(path, repaired, blocked.FileHash, true),
                "an unreadable original cannot be replaced using an unknown hash");
        }
        Check(File.ReadAllBytes(path).SequenceEqual(original), "unreadable recovery refuses writes and retains original bytes");
        using (var locked = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            Reject<IOException>(() => SettingsStorage.Save(path, repaired, broken.FileHash, true),
                "a cooperating instance's settings lease blocks repair before backup or replacement");
        Check(Directory.GetFiles(directory, "recovery-settings.json.recovery-*.bak").Length == 0,
            "blocked repairs do not create backup artifacts");
        using (var readOnlyLease = File.OpenRead(path))
            Reject<IOException>(() => SettingsStorage.Save(path, repaired, broken.FileHash, true),
                "actual replacement sharing denial fails after backup without claiming a completed repair");
        string attemptedBackup = Directory.GetFiles(directory, "recovery-settings.json.recovery-*.bak").Single();
        Check(File.ReadAllBytes(path).SequenceEqual(original) && File.ReadAllBytes(attemptedBackup).SequenceEqual(original) &&
            Directory.GetFiles(directory, "*.tmp").Length == 0,
            "a failed replacement retains the original plus a complete recovery copy and removes its temporary file");
        SavedSettings saved = SettingsStorage.Save(path, repaired, broken.FileHash, true);
        Check(File.ReadAllBytes(saved.RecoveryBackup).SequenceEqual(original) &&
            File.ReadAllBytes(path + ".bak").SequenceEqual(original) &&
            saved.FileHash == TrackingStorage.HashFile(path) && PluginSettings.Load(path).ReviewSeconds == 60,
            "explicit repair preserves original bytes in both a unique recovery backup and atomic previous-file backup");
        File.WriteAllText(path, "second corrupt original");
        LoadedSettings second = SettingsStorage.Load(path);
        SavedSettings secondSave = SettingsStorage.Save(path, repaired, second.FileHash, true);
        Check(saved.RecoveryBackup != secondSave.RecoveryBackup &&
            File.ReadAllBytes(saved.RecoveryBackup).SequenceEqual(original) &&
            File.ReadAllText(secondSave.RecoveryBackup) == "second corrupt original",
            "later repairs never rotate away or overwrite earlier recovery originals");
        string currentHash = secondSave.FileHash;
        Reject<IOException>(() => SettingsStorage.Save(path, repaired, second.FileHash, true),
            "a stale repair cannot overwrite a subsequently repaired file");
        Check(Directory.GetFiles(directory, "recovery-settings.json.recovery-*.bak").Length == 3,
            "stale repair fails before adding a misleading original backup");
        using (var gate = new ManualResetEventSlim(false))
        {
            var attempts = Enumerable.Range(0, 2).Select(index => Task.Factory.StartNew(() => {
                gate.Wait();
                try { SettingsStorage.Save(path, new PluginSettings { ReviewSeconds = 300 }, currentHash, false); return true; }
                catch (IOException) { return false; }
            })).ToArray();
            gate.Set(); Task.WaitAll(attempts);
            Check(attempts.Count(x => x.Result) == 1 && PluginSettings.Load(path).ReviewSeconds == 300 &&
                TrackingStorage.HashFile(path + ".bak") == currentHash,
                "simultaneous editors serialize compare/backup/write and allow exactly one changed-settings save");
        }
        LoadedSettings fresh = SettingsStorage.Load(path);
        Check(fresh.Error == null && fresh.FileHash == TrackingStorage.HashFile(path),
            "explicit reload can observe and adopt the other instance's settings and matching bytes");
        SettingsStorage.Save(path, new PluginSettings(), fresh.FileHash, false);
        Check(File.ReadAllBytes(saved.RecoveryBackup).SequenceEqual(original),
            "ordinary later settings saves retain permanent recovery originals");
        string observed = TrackingStorage.HashFile(path);
        File.Delete(path);
        Reject<IOException>(() => SettingsStorage.Save(path, repaired, observed, false),
            "a file removed since observation is not recreated silently");
        Check(!File.Exists(path) && Directory.GetFiles(directory, "*.tmp").Length == 0,
            "failed settings saves leave no replacement or temporary files");
        Directory.CreateDirectory(path);
        Check(SettingsStorage.Load(path).Error is UnauthorizedAccessException,
            "a directory occupying the settings filename is not treated as first launch");
    }
}
