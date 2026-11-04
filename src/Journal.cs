using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace FcsScribe
{
    [DataContract]
    public sealed class SnapshotDelta
    {
        [DataMember(Order = 0)] public Snapshot Context;
        [DataMember(Order = 1)] public List<Record> Updated;
        [DataMember(Order = 2)] public List<string> Removed;

        public static SnapshotDelta Between(Snapshot before, Snapshot after)
        {
            var old = before.Records.ToDictionary(x => x.Id, StringComparer.Ordinal);
            return new SnapshotDelta {
                Context = after.Context(),
                Updated = after.Records.Where(x => !old.ContainsKey(x.Id) || !Same(old[x.Id], x)).ToList(),
                Removed = before.Records.Select(x => x.Id).Except(after.Records.Select(x => x.Id), StringComparer.Ordinal).ToList()
            };
        }

        private static bool Same(Record left, Record right)
        {
            if (left.Type != right.Type || left.Name != right.Name || left.Cells.Count != right.Cells.Count) return false;
            var cells = left.Cells.ToDictionary(Snapshot.Key, StringComparer.Ordinal);
            return right.Cells.All(x => cells.ContainsKey(Snapshot.Key(x)) &&
                cells[Snapshot.Key(x)].Value.Same(x.Value) &&
                cells[Snapshot.Key(x)].Source == x.Source && cells[Snapshot.Key(x)].ReferencedId == x.ReferencedId);
        }

        public Snapshot Apply(Snapshot before)
        {
            if (Context == null || Updated == null || Removed == null || Context.Records == null || Context.Records.Count != 0)
                throw new InvalidDataException("Incomplete session delta.");
            Context.Validate();
            if (Context.ModId != before.ModId || Context.ModName != before.ModName ||
                !Context.LoadOrder.SequenceEqual(before.LoadOrder) || !Context.LockedMods.SequenceEqual(before.LockedMods))
                throw new InvalidDataException("The session delta changes project or loaded/locked context.");
            var records = before.Records.ToDictionary(x => x.Id, StringComparer.Ordinal);
            var touched = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in Removed)
                if (id == null || !touched.Add(id) || !records.Remove(id))
                    throw new InvalidDataException("Invalid or duplicate removed record in session delta.");
            foreach (Record record in Updated)
            {
                if (record == null || record.Id == null || !touched.Add(record.Id))
                    throw new InvalidDataException("Invalid or duplicate updated record in session delta.");
                records[record.Id] = record;
            }
            Snapshot result = Context.Context();
            result.Records = records.Values.ToList();
            result.Validate();
            return result;
        }
    }

    [DataContract]
    public sealed class SessionHeader
    {
        public const string FormatName = "FCS-Scribe session";
        [DataMember(Order = 0)] public string Format = FormatName;
        [DataMember(Order = 1)] public int SchemaVersion = 1;
        [DataMember(Order = 2)] public string ProjectId;
        [DataMember(Order = 3)] public string SessionId;
        [DataMember(Order = 4)] public string StartedUtc;
        [DataMember(Order = 5)] public string ParentEntryId;
        [DataMember(Order = 6)] public string ParentFingerprint;
    }

    [DataContract]
    public sealed class JournalRecord
    {
        [DataMember(Order = 0)] public string ProjectId;
        [DataMember(Order = 1)] public string EntryId;
        [DataMember(Order = 2)] public string SessionId;
        [DataMember(Order = 3)] public string PreviousEntryId;
        [DataMember(Order = 4)] public string PreviousFingerprint;
        [DataMember(Order = 5)] public string RecordedUtc;
        [DataMember(Order = 6)] public Packet Packet;
        [DataMember(Order = 7)] public SnapshotDelta Delta;
    }

    [DataContract]
    public sealed class JournalLine
    {
        public const string FormatName = "FCS-Scribe journal entry";
        [DataMember(Order = 0)] public string Format = FormatName;
        [DataMember(Order = 1)] public int SchemaVersion = 1;
        [DataMember(Order = 2)] public JournalRecord Record;
        [DataMember(Order = 3)] public string Hash;
    }

    [DataContract]
    public sealed class JournalInitialization
    {
        [DataMember(Order = 0)] public string BaselineHash;
        [DataMember(Order = 1)] public string SourceHash;
    }

    [DataContract]
    public sealed class JournalMarker
    {
        [DataMember(Order = 0)] public string Format = "FCS-Scribe session storage";
        [DataMember(Order = 1)] public int SchemaVersion = 1;
        [DataMember(Order = 2)] public string ProjectId;
        [DataMember(Order = 3)] public string HeadId;
        [DataMember(Order = 4)] public string Fingerprint;
        [DataMember(Order = 5, EmitDefaultValue = false)] public JournalInitialization Initialization;
    }

    public sealed class LegacyTrackingException : IOException
    {
        public readonly Packet Packet;
        public readonly string FileHash;
        public LegacyTrackingException(Packet packet, string hash) : base("This AI export requires explicit baseline conversion.")
        { Packet = packet; FileHash = hash; }
    }

    public sealed class IncompleteJournalException : IOException
    {
        public readonly string FilePath, FileHash;
        public readonly long ValidLength;
        public IncompleteJournalException(string path, long length) : base(
            "An interrupted final journal line requires explicit recovery: " + path)
        { FilePath = path; ValidLength = length; FileHash = TrackingStorage.HashFile(path); }
    }

    public sealed class JournalProject : IDisposable
    {
        private sealed class Stamp
        {
            public string Hash, Fingerprint;
            public bool Compiled;
            public DateTime RecordedUtc;
        }

        private readonly FileStream lease, anchor;
        private readonly Dictionary<string, Stamp> entries = new Dictionary<string, Stamp>(StringComparer.Ordinal);
        private readonly List<string> sessions = new List<string>();
        private readonly HashSet<string> compiledSessions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> sessionHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<RecordingEvidence> evidence = new List<RecordingEvidence>();
        private string sessionHash, journalHash, markerHash;
        private bool disposed, faulted;
        public readonly string PathName, DirectoryName, JournalPath, ProjectId;
        public Snapshot State { get; private set; }
        public string Fingerprint { get; private set; }
        public string HeadId { get; private set; }
        public string CapturedUtc { get; private set; }
        public string SessionPath { get; private set; }
        public string SessionId { get; private set; }
        public Packet LastEvidence { get; private set; }
        public int EntryCount { get { return entries.Count - 1; } }
        public int PendingCount { get { return entries.Values.Count(x => !x.Compiled); } }
        public DateTime? OldestPendingUtc
        {
            get { return entries.Values.Where(x => !x.Compiled).Select(x => (DateTime?)x.RecordedUtc).Min(); }
        }

        private JournalProject(string path)
        {
            PathName = Path.GetFullPath(path);
            DirectoryName = PathName + ".scribe";
            JournalPath = Path.Combine(DirectoryName, "journal.jsonl");
            Directory.CreateDirectory(Path.Combine(DirectoryName, "sessions"));
            lease = AcquireLease(PathName);
            try
            {
                string markerPath = Path.Combine(DirectoryName, "project.json");
                if (!File.Exists(PathName) && File.Exists(markerPath))
                    throw new IOException("Session storage exists without its tracking baseline. If a new-project initialization " +
                        "was interrupted, retry Start tracking > New with this same filename. Unrecognized storage is never " +
                        "replaced; restore its original baseline or choose a new filename.");
                anchor = new FileStream(PathName, FileMode.Open, FileAccess.Read, FileShare.Read);
                LoadedTracking loaded = TrackingStorage.Load(PathName);
                if (loaded.Legacy != null) throw new LegacyTrackingException(loaded.Legacy, loaded.FileHash);
                TrackingDocument root = loaded.Document;
                ProjectId = root.BaselineId;
                State = root.State; Fingerprint = root.StateFingerprint;
                HeadId = root.BaselineId; CapturedUtc = root.ValidatedUtc;
                LastEvidence = root.LatestEvidence(Fingerprint);
                foreach (Packet packet in root.ImportedEvidence)
                    evidence.Add(new RecordingEvidence { EntryId = "imported-" + packet.BatchId,
                        Kind = "imported", RecordedUtc = packet.ExportedUtc, Packet = packet });
                for (int i = 0; i < root.Entries.Count; i++)
                {
                    Packet packet = root.Entries[i].Packet;
                    evidence.Add(new RecordingEvidence { EntryId = "checkpoint-" + i.ToString(CultureInfo.InvariantCulture) + "-" + packet.BatchId,
                        Kind = "checkpoint", RecordedUtc = packet.ExportedUtc, Packet = packet });
                }
                entries.Add(HeadId, new Stamp { Fingerprint = Fingerprint, Compiled = true });
                JournalMarker marker = null;
                if (File.Exists(markerPath))
                {
                    marker = ReadMarker(markerPath, out markerHash);
                    if (marker == null || marker.Format != "FCS-Scribe session storage" || marker.SchemaVersion != 1 ||
                        marker.ProjectId != ProjectId || !IsId(marker.HeadId))
                        throw new InvalidDataException("The session storage marker belongs to another project or is invalid.");
                    if (marker.Initialization != null)
                    {
                        ValidateInitialization(marker);
                        RequireEmptyHistory(PathName);
                        if (root.JournalVersion != 1 || root.Entries.Count != 0 ||
                            root.InitialFingerprint != root.StateFingerprint ||
                            marker.Fingerprint != root.StateFingerprint || marker.Initialization.BaselineHash != loaded.FileHash)
                            throw new InvalidDataException("The interrupted initialization does not match this baseline. " +
                                "Restore the matching files; nothing was replaced.");
                        markerHash = FinishInitialization(PathName, marker, markerHash, null);
                        marker.Initialization = null;
                    }
                }
                else if (root.JournalVersion != 0)
                    throw new InvalidDataException("The project's session storage is missing or incomplete. " +
                        "Restore its matching .json.scribe folder, including project.json.");
                if (File.Exists(JournalPath))
                {
                    ReadLines(JournalPath, false, null, line => {
                        ValidateLine(line);
                        ReplayValidated(line, true);
                    });
                    journalHash = TrackingStorage.HashFile(JournalPath);
                }
                var remaining = System.IO.Directory.GetFiles(Path.Combine(DirectoryName, "sessions"), "*.jsonl")
                    .Select(pathName => new { Path = pathName, Header = ReadHeader(pathName) }).ToList();
                while (remaining.Count > 0)
                {
                    var next = remaining.FirstOrDefault(x => x.Header != null && x.Header.ParentEntryId != null &&
                        entries.ContainsKey(x.Header.ParentEntryId));
                    if (next == null) throw new InvalidDataException("A session depends on missing history. Restore its required session files.");
                    ValidateHeader(next.Header);
                    string previous = next.Header.ParentEntryId, fingerprint = next.Header.ParentFingerprint;
                    bool pending = false;
                    ReadLines(next.Path, true, null, line => {
                        ValidateLine(line);
                        if (line.Record.SessionId != next.Header.SessionId || line.Record.PreviousEntryId != previous ||
                            line.Record.PreviousFingerprint != fingerprint)
                            throw new InvalidDataException("The session has a broken local entry chain.");
                        ReplayValidated(line, false);
                        pending |= !entries[line.Record.EntryId].Compiled;
                        previous = line.Record.EntryId; fingerprint = line.Record.Packet.SourceFingerprint;
                    });
                    sessions.Add(next.Path);
                    sessionHashes.Add(next.Path, TrackingStorage.HashFile(next.Path));
                    if (!pending) compiledSessions.Add(next.Path);
                    remaining.Remove(next);
                }
                if (marker != null && (!entries.ContainsKey(marker.HeadId) || entries[marker.HeadId].Fingerprint != marker.Fingerprint))
                    throw new InvalidDataException("Required recorded history is missing. Restore uncompiled sessions or the main journal.");
                if (marker == null) WriteMarker(HeadId, Fingerprint);
            }
            catch { Dispose(); throw; }
        }

        public static JournalProject Open(string path)
        {
            TrackingStorage.RequireJson(path);
            return new JournalProject(path);
        }

        public static JournalProject Create(string path, Snapshot current)
        {
            return Create(path, current, null);
        }

        internal static JournalProject Create(string path, Snapshot current, Action<string> checkpoint)
        {
            TrackingStorage.RequireJson(path);
            path = Path.GetFullPath(path);
            if (System.IO.Directory.Exists(path)) throw new IOException("The tracking JSON destination is a directory. Choose a file path.");
            using (AcquireLease(path))
            {
                if (File.Exists(path))
                    throw new IOException("The tracking JSON already exists. Use Start tracking > Resume existing JSON. " +
                        "A failed initialization may already have committed its baseline; Resume validates it without overwriting.");
                TrackingDocument document = TrackingDocument.Create(current, null);
                document.JournalVersion = 1;
                InitializeProject(path, document, null, checkpoint);
            }
            return Open(path);
        }

        public static JournalProject ConvertLegacy(string path, Snapshot current, LegacyTrackingException legacy)
        {
            return ConvertLegacy(path, current, legacy, null);
        }

        internal static JournalProject ConvertLegacy(string path, Snapshot current, LegacyTrackingException legacy,
            Action<string> checkpoint)
        {
            TrackingStorage.RequireJson(path);
            path = Path.GetFullPath(path);
            using (AcquireLease(path))
            {
                TrackingDocument document = TrackingDocument.Create(current, legacy.Packet);
                document.JournalVersion = 1;
                InitializeProject(path, document, legacy.FileHash, checkpoint);
            }
            return Open(path);
        }

        private static void InitializeProject(string path, TrackingDocument document, string sourceHash,
            Action<string> checkpoint)
        {
            document.Validate();
            RequireEmptyHistory(path);
            string markerPath = Path.Combine(path + ".scribe", "project.json");
            string markerHash = null;
            if (File.Exists(markerPath))
            {
                JournalMarker previous = ReadMarker(markerPath, out markerHash);
                ValidateInitialization(previous);
                if (previous.Initialization.SourceHash != sourceHash)
                    throw new IOException("Unfinished initialization belongs to a different source. Reload the original JSON " +
                        "or choose a new filename; existing storage was not replaced.");
            }
            RequireOwnedMarkerBackup(markerPath, sourceHash);
            TrackingStorage.Verify(path, sourceHash);
            RequireBaselineBackup(path, sourceHash);
            if (sourceHash != null)
            {
                TrackingStorage.PreserveLegacy(path, sourceHash);
            }
            var marker = new JournalMarker { ProjectId = document.BaselineId, HeadId = document.BaselineId,
                Fingerprint = document.StateFingerprint, Initialization = new JournalInitialization {
                    BaselineHash = Json.StreamHash(document), SourceHash = sourceHash
                } };
            markerHash = TrackingStorage.WriteAtomic(markerPath, marker, markerHash,
                InitializationCheckpoint(path, marker, checkpoint, "marker-"));
            TrackingStorage.WriteAtomic(path, document, sourceHash,
                InitializationCheckpoint(path, marker, checkpoint, "baseline-"));
            FinishInitialization(path, marker, markerHash, checkpoint);
        }

        private static string FinishInitialization(string path, JournalMarker marker, string markerHash,
            Action<string> checkpoint)
        {
            RequireEmptyHistory(path);
            string markerPath = Path.Combine(path + ".scribe", "project.json");
            RequireOwnedMarkerBackup(markerPath, marker.Initialization.SourceHash);
            RequireLegacyBackup(path, marker.Initialization.SourceHash);
            TrackingStorage.Verify(path, marker.Initialization.BaselineHash);
            var complete = new JournalMarker { ProjectId = marker.ProjectId, HeadId = marker.HeadId,
                Fingerprint = marker.Fingerprint };
            return TrackingStorage.WriteAtomic(markerPath, complete, markerHash,
                InitializationCheckpoint(path, marker, checkpoint, "final-marker-"));
        }

        private static Action<string> InitializationCheckpoint(string path, JournalMarker marker,
            Action<string> checkpoint, string phase)
        {
            return step => {
                if (checkpoint != null) checkpoint(phase + step);
                if (step != "commit") return;
                RequireEmptyHistory(path);
                RequireOwnedMarkerBackup(Path.Combine(path + ".scribe", "project.json"), marker.Initialization.SourceHash);
                RequireLegacyBackup(path, marker.Initialization.SourceHash);
                if (phase != "final-marker-") RequireBaselineBackup(path, marker.Initialization.SourceHash);
                TrackingStorage.Verify(path, phase == "final-marker-" ?
                    marker.Initialization.BaselineHash : marker.Initialization.SourceHash);
            };
        }

        private static void RequireBaselineBackup(string path, string sourceHash)
        {
            if (System.IO.Directory.Exists(path + ".bak") ||
                (File.Exists(path + ".bak") && (sourceHash == null || TrackingStorage.HashFile(path + ".bak") != sourceHash)) ||
                (sourceHash == null && (File.Exists(path + ".legacy.bak") || System.IO.Directory.Exists(path + ".legacy.bak"))))
                throw new IOException("An unrelated baseline/legacy backup already exists. Restore its matching project " +
                    "or choose a different copy/filename; no backup was overwritten.");
        }

        private static void RequireLegacyBackup(string path, string sourceHash)
        {
            if (sourceHash != null && (!File.Exists(path + ".legacy.bak") ||
                TrackingStorage.HashFile(path + ".legacy.bak") != sourceHash))
                throw new IOException("The preserved legacy backup is missing or changed. Restore it before resuming; nothing was replaced.");
        }

        private static JournalMarker ReadMarker(string path, out string hash)
        {
            using (var stream = File.OpenRead(path))
            {
                JournalMarker marker = Json.ReadStream<JournalMarker>(stream);
                stream.Position = 0;
                using (var sha = SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                return marker;
            }
        }

        private static void ValidateInitialization(JournalMarker marker)
        {
            if (marker == null || marker.Format != "FCS-Scribe session storage" || marker.SchemaVersion != 1 ||
                !IsId(marker.ProjectId) || marker.HeadId != marker.ProjectId || !IsHash(marker.Fingerprint) ||
                marker.Initialization == null || !IsHash(marker.Initialization.BaselineHash) ||
                (marker.Initialization.SourceHash != null && !IsHash(marker.Initialization.SourceHash)))
                throw new InvalidDataException("Existing session storage is not a recognized unfinished initialization. " +
                    "Restore its original baseline or choose a new filename. No existing storage was removed.");
        }

        private static void RequireOwnedMarkerBackup(string markerPath, string sourceHash)
        {
            if (System.IO.Directory.Exists(markerPath + ".bak"))
                throw new IOException("The session marker backup path is a directory. Choose another filename; existing contents were not changed.");
            if (!File.Exists(markerPath + ".bak")) return;
            if (!File.Exists(markerPath))
                throw new IOException("A session marker backup exists without its marker. Restore the matching project " +
                    "or choose a new filename; existing backups are never claimed as a new project.");
            string hash;
            JournalMarker backup = ReadMarker(markerPath + ".bak", out hash);
            ValidateInitialization(backup);
            if (backup.Initialization.SourceHash != sourceHash)
                throw new IOException("The existing session marker backup belongs to another source. It was not overwritten.");
        }

        private static void RequireEmptyHistory(string path)
        {
            string directory = path + ".scribe", sessions = Path.Combine(directory, "sessions"),
                recovery = Path.Combine(directory, "recovery");
            if (File.Exists(Path.Combine(directory, "journal.jsonl")) ||
                System.IO.Directory.Exists(Path.Combine(directory, "journal.jsonl")) ||
                File.Exists(Path.Combine(directory, "journal.jsonl.bak")) ||
                System.IO.Directory.Exists(Path.Combine(directory, "journal.jsonl.bak")) ||
                File.Exists(sessions) || File.Exists(recovery) ||
                (System.IO.Directory.Exists(recovery) && System.IO.Directory.GetFileSystemEntries(recovery).Length != 0) ||
                (System.IO.Directory.Exists(sessions) && System.IO.Directory.GetFileSystemEntries(sessions).Length != 0))
                throw new IOException("Existing session history cannot be claimed by project initialization. Restore its " +
                    "matching baseline or choose a new filename; no history was replaced.");
        }

        private void WriteMarker(string headId, string fingerprint)
        {
            markerHash = TrackingStorage.WriteAtomic(Path.Combine(DirectoryName, "project.json"),
                new JournalMarker { ProjectId = ProjectId, HeadId = headId, Fingerprint = fingerprint }, markerHash);
        }

        private static FileStream AcquireLease(string path)
        {
            string directory = path + ".scribe";
            Directory.CreateDirectory(directory);
            return new FileStream(Path.Combine(directory, "writer.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
        }

        public Tracker ResumeTracker()
        {
            EnsureOpen();
            return new Tracker(State, HeadId, CapturedUtc, true);
        }

        public void BeginSession()
        {
            EnsureOpen();
            WriteMarker(HeadId, Fingerprint);
            string id = Guid.NewGuid().ToString("N");
            var header = new SessionHeader { ProjectId = ProjectId, SessionId = id,
                StartedUtc = UtcNow(), ParentEntryId = HeadId, ParentFingerprint = Fingerprint };
            string path = Path.Combine(DirectoryName, "sessions",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss'Z'", CultureInfo.InvariantCulture) + "_" + id + ".jsonl");
            string temporary = path + ".tmp";
            try { Append(temporary, header, null); File.Move(temporary, path); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            SessionPath = path; SessionId = id; sessionHash = TrackingStorage.HashFile(path);
            sessions.Add(path);
            sessionHashes.Add(path, sessionHash);
            compiledSessions.Add(path);
        }

        public Packet Record(Snapshot current)
        {
            EnsureOpen();
            if (SessionPath == null) throw new InvalidOperationException("Begin a session before recording.");
            Packet packet = ResumeTracker().Export(current);
            if (packet.SourceFingerprint == Fingerprint)
            {
                TrackingStorage.Verify(SessionPath, sessionHash);
                return packet;
            }
            var record = new JournalRecord { ProjectId = ProjectId, EntryId = Guid.NewGuid().ToString("N"),
                SessionId = SessionId, PreviousEntryId = HeadId, PreviousFingerprint = Fingerprint,
                RecordedUtc = UtcNow(), Packet = packet, Delta = SnapshotDelta.Between(State, current) };
            var line = new JournalLine { Record = record, Hash = Json.StreamHash(record) };
            ValidateLine(line);
            try
            {
                sessionHash = Append(SessionPath, line, sessionHash);
                WriteMarker(record.EntryId, packet.SourceFingerprint);
            }
            catch { faulted = true; throw; }
            entries.Add(record.EntryId, new Stamp { Hash = line.Hash, Fingerprint = packet.SourceFingerprint,
                RecordedUtc = DateTime.Parse(record.RecordedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) });
            evidence.Add(Evidence(record));
            sessionHashes[SessionPath] = sessionHash;
            compiledSessions.Remove(SessionPath);
            State = current; Fingerprint = packet.SourceFingerprint; HeadId = record.EntryId; CapturedUtc = record.RecordedUtc;
            if (packet.Changes.Count > 0) LastEvidence = packet;
            return packet;
        }

        public List<RecordingEvidence> ExportRecordings(Snapshot current)
        {
            EnsureOpen();
            Packet pending = ResumeTracker().Export(current);
            TrackingStorage.Verify(JournalPath, journalHash);
            TrackingStorage.Verify(Path.Combine(DirectoryName, "project.json"), markerHash);
            foreach (var session in sessionHashes) TrackingStorage.Verify(session.Key, session.Value);
            var result = evidence.ToList();
            if (pending.SourceFingerprint != Fingerprint)
                result.Add(new RecordingEvidence { EntryId = "pending-" + Json.Hash(HeadId + ":" + pending.SourceFingerprint),
                    SessionId = SessionId, RecordedUtc = pending.ExportedUtc, Kind = "pending", Packet = pending });
            return Json.Read<List<RecordingEvidence>>(Json.Write(result));
        }

        private static RecordingEvidence Evidence(JournalRecord record)
        {
            return new RecordingEvidence { EntryId = record.EntryId, SessionId = record.SessionId,
                RecordedUtc = record.RecordedUtc, Kind = "recorded", Packet = record.Packet };
        }

        public int Compile()
        {
            EnsureOpen();
            TrackingStorage.Verify(JournalPath, journalHash);
            int count = 0;
            FileStream output = null;
            try
            {
                foreach (string path in sessions)
                {
                    TrackingStorage.Verify(path, sessionHashes[path]);
                    if (compiledSessions.Contains(path)) continue;
                    SessionHeader header = null;
                    string previous = null, fingerprint = null;
                    ReadLines(path, true, value => {
                        ValidateHeader(value); header = value;
                        previous = value.ParentEntryId; fingerprint = value.ParentFingerprint;
                    }, line => {
                        ValidateLine(line);
                        Stamp stamp;
                        if (line.Record.SessionId != header.SessionId || line.Record.PreviousEntryId != previous ||
                            line.Record.PreviousFingerprint != fingerprint ||
                            !entries.TryGetValue(line.Record.EntryId, out stamp) || stamp.Hash != line.Hash)
                            throw new InvalidDataException("A session changed outside Scribe; reopen and validate the project.");
                        previous = line.Record.EntryId; fingerprint = line.Record.Packet.SourceFingerprint;
                        if (stamp.Compiled) return;
                        if (output == null)
                        {
                            TrackingStorage.Verify(JournalPath, journalHash);
                            output = new FileStream(JournalPath, journalHash == null ? FileMode.CreateNew : FileMode.Append,
                                FileAccess.Write, FileShare.Read);
                        }
                        Json.WriteStream(output, line); output.WriteByte(10); output.Flush(true);
                        stamp.Compiled = true; count++;
                    });
                    compiledSessions.Add(path);
                }
            }
            catch { faulted = true; throw; }
            finally { if (output != null) output.Dispose(); }
            if (count > 0) journalHash = TrackingStorage.HashFile(JournalPath);
            return count;
        }

        private void ReplayValidated(JournalLine line, bool compiled)
        {
            Stamp known;
            if (entries.TryGetValue(line.Record.EntryId, out known))
            {
                if (compiled || known.Hash != line.Hash)
                    throw new InvalidDataException("Duplicate or conflicting journal entry.");
                return;
            }
            JournalRecord record = line.Record;
            if (record.PreviousEntryId != HeadId || record.PreviousFingerprint != Fingerprint)
                throw new InvalidDataException("The project contains divergent or reordered history.");
            Snapshot next = record.Delta.Apply(State);
            if (next.Fingerprint() != record.Packet.SourceFingerprint ||
                Json.StreamHash(next.Context()) != Json.StreamHash(record.Packet.Context))
                throw new InvalidDataException("The replayed delta does not match its saved checkpoint.");
            entries.Add(record.EntryId, new Stamp { Hash = line.Hash,
                Fingerprint = record.Packet.SourceFingerprint, Compiled = compiled,
                RecordedUtc = DateTime.Parse(record.RecordedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) });
            evidence.Add(Evidence(record));
            State = next; Fingerprint = record.Packet.SourceFingerprint;
            HeadId = record.EntryId; CapturedUtc = record.RecordedUtc;
            if (record.Packet.Changes.Count > 0) LastEvidence = record.Packet;
        }

        private void ValidateHeader(SessionHeader header)
        {
            Stamp parent;
            if (header == null || header.Format != SessionHeader.FormatName || header.SchemaVersion != 1 ||
                header.ProjectId != ProjectId || !IsId(header.SessionId) || !IsUtc(header.StartedUtc) ||
                header.ParentEntryId == null || !entries.TryGetValue(header.ParentEntryId, out parent) ||
                parent.Fingerprint != header.ParentFingerprint)
                throw new InvalidDataException("Invalid or incompatible session header.");
        }

        private void ValidateLine(JournalLine line)
        {
            if (line == null || line.Format != JournalLine.FormatName || line.SchemaVersion != 1 || line.Record == null)
                throw new InvalidDataException("Unsupported journal entry.");
            JournalRecord record = line.Record;
            if (record.ProjectId != ProjectId || !IsId(record.EntryId) || !IsId(record.SessionId) ||
                !IsId(record.PreviousEntryId) || !IsUtc(record.RecordedUtc) || record.Delta == null ||
                Json.StreamHash(record) != line.Hash)
                throw new InvalidDataException("Invalid journal entry identity, time or hash.");
            TrackingDocument.ValidatePacket(record.Packet);
            if (record.Packet.BaselineId != record.PreviousEntryId)
                throw new InvalidDataException("Journal evidence belongs to another baseline.");
        }

        private static SessionHeader ReadHeader(string path)
        {
            CheckComplete(path);
            using (var reader = new StreamReader(path, new UTF8Encoding(false, true)))
                return Json.Read<SessionHeader>(reader.ReadLine());
        }

        private static void ReadLines(string path, bool session, Action<SessionHeader> header, Action<JournalLine> entry)
        {
            CheckComplete(path);
            using (var reader = new StreamReader(path, new UTF8Encoding(false, true)))
            {
                if (session)
                {
                    SessionHeader value = Json.Read<SessionHeader>(reader.ReadLine());
                    if (header != null) header(value);
                }
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (String.IsNullOrWhiteSpace(line)) throw new InvalidDataException("Empty journal line.");
                    entry(Json.Read<JournalLine>(line));
                }
            }
        }

        private static void CheckComplete(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length > 0)
                {
                    stream.Position = stream.Length - 1;
                    if (stream.ReadByte() == 10) return;
                    while (stream.Position > 1)
                    {
                        stream.Position -= 2;
                        if (stream.ReadByte() == 10) throw new IncompleteJournalException(path, stream.Position);
                    }
                }
                throw new IncompleteJournalException(path, 0);
            }
        }

        public static string RecoverTail(string projectPath, IncompleteJournalException error)
        {
            string root = Path.GetFullPath(projectPath) + ".scribe" + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(error.FilePath);
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(Path.GetExtension(path), ".jsonl", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Recovery is restricted to this project's journal files.");
            using (AcquireLease(Path.GetFullPath(projectPath)))
            {
                string recovery = Path.Combine(root, "recovery");
                Directory.CreateDirectory(recovery);
                string backup = Path.Combine(recovery, Guid.NewGuid().ToString("N") + ".bak");
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    using (var sha = SHA256.Create())
                        if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != error.FileHash)
                            throw new IOException("The interrupted log changed before recovery.");
                    stream.Position = 0;
                    using (var output = new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { stream.CopyTo(output); output.Flush(true); }
                    stream.SetLength(error.ValidLength); stream.Flush(true);
                }
                if (error.ValidLength == 0) File.Delete(path);
                return backup;
            }
        }

        private static string Append<T>(string path, T value, string expectedHash)
        {
            TrackingStorage.Verify(path, expectedHash);
            using (var stream = new FileStream(path, expectedHash == null ? FileMode.CreateNew : FileMode.Append,
                FileAccess.Write, FileShare.Read))
            {
                Json.WriteStream(stream, value);
                stream.WriteByte(10);
                stream.Flush(true);
            }
            return TrackingStorage.HashFile(path);
        }

        private void EnsureOpen()
        {
            if (disposed) throw new ObjectDisposedException("JournalProject");
            if (faulted) throw new IOException("A journal write failed. Reopen the project and validate its logs before recording again.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (anchor != null) anchor.Dispose();
            if (lease != null) lease.Dispose();
        }

        private static bool IsId(string text) { Guid id; return Guid.TryParse(text, out id); }
        private static bool IsHash(string text)
        { return text != null && text.Length == 64 && text.All(x => "0123456789abcdef".Contains(x)); }
        private static bool IsUtc(string text)
        {
            DateTime value;
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value) &&
                value.Kind == DateTimeKind.Utc;
        }
        private static string UtcNow() { return DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture); }
    }

    public sealed class JournalSchedule
    {
        public const long CompilationMilliseconds = 30 * 60 * 1000;
        public long NextReview { get; private set; }
        public long NextCompilation { get; private set; }
        public DateTime? OldestPendingUtc { get; private set; }
        private int seconds;
        public JournalSchedule() { NextReview = NextCompilation = Int64.MaxValue; }

        public void Start(long now, DateTime utcNow, int intervalSeconds, DateTime? oldestPendingUtc)
        {
            ValidateInterval(intervalSeconds);
            ValidateTimes(utcNow, oldestPendingUtc);
            seconds = intervalSeconds;
            Reviewed(now);
            OldestPendingUtc = null; NextCompilation = Int64.MaxValue;
            UpdatePending(now, utcNow, oldestPendingUtc);
        }

        public void Configure(long now, int intervalSeconds)
        {
            ValidateInterval(intervalSeconds);
            if (seconds == intervalSeconds) return;
            seconds = intervalSeconds;
            Reviewed(now);
        }

        public void UpdatePending(long now, DateTime utcNow, DateTime? oldestPendingUtc)
        {
            ValidateTimes(utcNow, oldestPendingUtc);
            if (!oldestPendingUtc.HasValue)
            {
                OldestPendingUtc = null; NextCompilation = Int64.MaxValue;
                return;
            }
            if (OldestPendingUtc == oldestPendingUtc) return;
            long ageTicks = utcNow.Ticks - oldestPendingUtc.Value.Ticks;
            long remaining = ageTicks < 0 ? 0 :
                Math.Max(0, CompilationMilliseconds - ageTicks / TimeSpan.TicksPerMillisecond);
            long deadline = checked(now + remaining);
            NextCompilation = OldestPendingUtc.HasValue ? Math.Min(NextCompilation, deadline) : deadline;
            OldestPendingUtc = oldestPendingUtc;
        }

        public bool ReviewDue(long now) { return seconds > 0 && now >= NextReview; }
        public bool CompilationDue(long now) { return seconds > 0 && OldestPendingUtc.HasValue && now >= NextCompilation; }
        public void Reviewed(long now) { NextReview = seconds == 0 ? Int64.MaxValue : checked(now + seconds * 1000L); }

        private static void ValidateInterval(int intervalSeconds)
        {
            if (!PluginSettings.Intervals.Contains(intervalSeconds)) throw new ArgumentOutOfRangeException("intervalSeconds");
        }
        private static void ValidateTimes(DateTime utcNow, DateTime? oldestPendingUtc)
        {
            if (utcNow.Kind != DateTimeKind.Utc ||
                (oldestPendingUtc.HasValue && oldestPendingUtc.Value.Kind != DateTimeKind.Utc))
                throw new ArgumentException("Compilation scheduling requires UTC timestamps.");
        }
    }
}
