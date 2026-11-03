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
    public sealed class JournalMarker
    {
        [DataMember(Order = 0)] public string Format = "FCS-Scribe session storage";
        [DataMember(Order = 1)] public int SchemaVersion = 1;
        [DataMember(Order = 2)] public string ProjectId;
        [DataMember(Order = 3)] public string HeadId;
        [DataMember(Order = 4)] public string Fingerprint;
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
        }

        private readonly FileStream lease, anchor;
        private readonly Dictionary<string, Stamp> entries = new Dictionary<string, Stamp>(StringComparer.Ordinal);
        private readonly List<string> sessions = new List<string>();
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

        private JournalProject(string path)
        {
            PathName = Path.GetFullPath(path);
            DirectoryName = PathName + ".scribe";
            JournalPath = Path.Combine(DirectoryName, "journal.jsonl");
            Directory.CreateDirectory(Path.Combine(DirectoryName, "sessions"));
            lease = AcquireLease(PathName);
            try
            {
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
                string markerPath = Path.Combine(DirectoryName, "project.json");
                if (File.Exists(markerPath))
                {
                    using (var stream = File.OpenRead(markerPath)) marker = Json.ReadStream<JournalMarker>(stream);
                    markerHash = TrackingStorage.HashFile(markerPath);
                    if (marker == null || marker.Format != "FCS-Scribe session storage" || marker.SchemaVersion != 1 ||
                        marker.ProjectId != ProjectId || !IsId(marker.HeadId))
                        throw new InvalidDataException("The session storage marker belongs to another project or is invalid.");
                }
                else if (root.JournalVersion != 0)
                    throw new InvalidDataException("The project's session storage is missing. Restore its .json.scribe folder.");
                if (File.Exists(JournalPath))
                {
                    ReadLines(JournalPath, false, null, line => Replay(line, true));
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
                    ReadLines(next.Path, true, null, line => {
                        ValidateLine(line);
                        if (line.Record.SessionId != next.Header.SessionId || line.Record.PreviousEntryId != previous ||
                            line.Record.PreviousFingerprint != fingerprint)
                            throw new InvalidDataException("The session has a broken local entry chain.");
                        Replay(line, false);
                        previous = line.Record.EntryId; fingerprint = line.Record.Packet.SourceFingerprint;
                    });
                    sessions.Add(next.Path);
                    sessionHashes.Add(next.Path, TrackingStorage.HashFile(next.Path));
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
            TrackingStorage.RequireJson(path);
            path = Path.GetFullPath(path);
            using (AcquireLease(path))
            {
                if (File.Exists(path) || File.Exists(Path.Combine(path + ".scribe", "project.json")))
                    throw new IOException("The project already exists. Resume it or choose a new filename.");
                TrackingDocument document = TrackingDocument.Create(current, null);
                document.JournalVersion = 1;
                InitializeMarker(path, document);
                TrackingStorage.Save(path, document, null);
            }
            return Open(path);
        }

        public static JournalProject ConvertLegacy(string path, Snapshot current, LegacyTrackingException legacy)
        {
            TrackingStorage.RequireJson(path);
            using (AcquireLease(Path.GetFullPath(path)))
            {
                TrackingDocument document = TrackingDocument.Create(current, legacy.Packet);
                document.JournalVersion = 1;
                TrackingStorage.PreserveLegacy(path, legacy.FileHash);
                if (File.Exists(Path.Combine(path + ".scribe", "journal.jsonl")) ||
                    (System.IO.Directory.Exists(Path.Combine(path + ".scribe", "sessions")) &&
                    System.IO.Directory.GetFiles(Path.Combine(path + ".scribe", "sessions"), "*.jsonl").Length != 0))
                    throw new IOException("Existing session history must not be replaced during legacy conversion.");
                InitializeMarker(path, document);
                TrackingStorage.Save(path, document, legacy.FileHash);
            }
            return Open(path);
        }

        private static void InitializeMarker(string path, TrackingDocument document)
        {
            string marker = Path.Combine(path + ".scribe", "project.json");
            TrackingStorage.WriteAtomic(marker, new JournalMarker { ProjectId = document.BaselineId,
                HeadId = document.BaselineId, Fingerprint = document.StateFingerprint },
                File.Exists(marker) ? TrackingStorage.HashFile(marker) : null);
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
        }

        public Packet Record(Snapshot current)
        {
            EnsureOpen();
            if (SessionPath == null) throw new InvalidOperationException("Begin a session before recording.");
            Packet packet = ResumeTracker().Export(current);
            TrackingStorage.Verify(SessionPath, sessionHash);
            if (packet.SourceFingerprint == Fingerprint) return packet;
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
            entries.Add(record.EntryId, new Stamp { Hash = line.Hash, Fingerprint = packet.SourceFingerprint });
            evidence.Add(Evidence(record));
            sessionHashes[SessionPath] = sessionHash;
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
                }
            }
            catch { faulted = true; throw; }
            finally { if (output != null) output.Dispose(); }
            if (count > 0) journalHash = TrackingStorage.HashFile(JournalPath);
            return count;
        }

        private void Replay(JournalLine line, bool compiled)
        {
            ValidateLine(line);
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
                Fingerprint = record.Packet.SourceFingerprint, Compiled = compiled });
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
        private int seconds;
        public void Reset(long now, int intervalSeconds)
        {
            if (!PluginSettings.Intervals.Contains(intervalSeconds)) throw new ArgumentOutOfRangeException("intervalSeconds");
            seconds = intervalSeconds;
            Reviewed(now); Compiled(now);
        }
        public bool ReviewDue(long now) { return seconds > 0 && now >= NextReview; }
        public bool CompilationDue(long now) { return seconds > 0 && now >= NextCompilation; }
        public void Reviewed(long now) { NextReview = seconds == 0 ? Int64.MaxValue : now + seconds * 1000L; }
        public void Compiled(long now) { NextCompilation = seconds == 0 ? Int64.MaxValue : now + CompilationMilliseconds; }
    }
}
