using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace FcsScribe
{
    public sealed class HistorySessionInfo
    {
        public readonly string SessionId, FileName, Sha256;
        public readonly DateTime LastActivityUtc;
        public readonly long Bytes;
        public readonly int Recordings;
        public readonly bool Active, FullyCompiled;
        public bool Eligible { get { return !Active && FullyCompiled; } }
        public string Status { get { return Active ? "Active - retained" : FullyCompiled ? "Eligible" : "Uncompiled - retained"; } }

        internal HistorySessionInfo(string id, string file, string hash, DateTime activity, long bytes,
            int recordings, bool active, bool compiled)
        {
            SessionId = id; FileName = file; Sha256 = hash; LastActivityUtc = activity; Bytes = bytes;
            Recordings = recordings; Active = active; FullyCompiled = compiled;
        }
    }

    public sealed class HistoryStorageInventory
    {
        public readonly string ProjectId, BaselineHash, HeadId, Fingerprint, JournalHash, MarkerHash;
        public readonly long BaselineBytes, JournalBytes, OtherBytes;
        public readonly ReadOnlyCollection<HistorySessionInfo> Sessions;
        public long SessionBytes { get { return Sessions.Sum(session => session.Bytes); } }
        public long TotalBytes { get { return checked(BaselineBytes + JournalBytes + OtherBytes + SessionBytes); } }

        internal HistoryStorageInventory(string project, string baseline, string head, string fingerprint,
            string journal, string marker, long baselineBytes, long journalBytes, long otherBytes,
            IEnumerable<HistorySessionInfo> sessions)
        {
            ProjectId = project; BaselineHash = baseline; HeadId = head; Fingerprint = fingerprint;
            JournalHash = journal; MarkerHash = marker; BaselineBytes = baselineBytes;
            JournalBytes = journalBytes; OtherBytes = otherBytes; Sessions = sessions.ToList().AsReadOnly();
        }

        public List<HistorySessionInfo> Select(DateTime? olderThanUtc, int? keepNewest)
        {
            if (olderThanUtc.HasValue && olderThanUtc.Value.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Archive age limits must use UTC.", "olderThanUtc");
            if (keepNewest.HasValue && keepNewest.Value < 0)
                throw new ArgumentOutOfRangeException("keepNewest");
            IEnumerable<HistorySessionInfo> eligible = Sessions.Where(session => session.Eligible)
                .OrderByDescending(session => session.LastActivityUtc).ThenBy(session => session.SessionId, StringComparer.Ordinal);
            if (keepNewest.HasValue) eligible = eligible.Skip(keepNewest.Value);
            if (olderThanUtc.HasValue) eligible = eligible.Where(session => session.LastActivityUtc < olderThanUtc.Value);
            return eligible.ToList();
        }
    }

    [DataContract]
    public sealed class HistoryArchiveSession
    {
        [DataMember(Order = 0)] public string FileName;
        [DataMember(Order = 1)] public string SessionId;
        [DataMember(Order = 2)] public string Sha256;
        [DataMember(Order = 3)] public long Bytes;
        [DataMember(Order = 4)] public int Recordings;
    }

    [DataContract]
    public sealed class HistoryArchiveManifest
    {
        public const string FormatName = "FCS-Scribe compiled session archive";
        [DataMember(Order = 0)] public string Format = FormatName;
        [DataMember(Order = 1)] public int SchemaVersion = 1;
        [DataMember(Order = 2)] public string ArchiveId;
        [DataMember(Order = 3)] public string ProjectId;
        [DataMember(Order = 4)] public string BaselineHash;
        [DataMember(Order = 5)] public string ModId;
        [DataMember(Order = 6)] public string CreatedUtc;
        [DataMember(Order = 7)] public string HeadId;
        [DataMember(Order = 8)] public string Fingerprint;
        [DataMember(Order = 9)] public string JournalHash;
        [DataMember(Order = 10)] public List<HistoryArchiveSession> Sessions;
    }

    public sealed class HistoryRestorePreview
    {
        public readonly string ManifestPath, ManifestHash;
        public readonly ReadOnlyCollection<HistoryArchiveSession> Sessions;
        public readonly int AlreadyPresent;
        public long Bytes { get { return Sessions.Sum(session => session.Bytes); } }
        internal HistoryRestorePreview(string path, string hash, IEnumerable<HistoryArchiveSession> sessions, int present)
        { ManifestPath = path; ManifestHash = hash; Sessions = sessions.ToList().AsReadOnly(); AlreadyPresent = present; }
    }

    public sealed class HistoryArchiveResult
    {
        public readonly string ManifestPath;
        public readonly int Changed, AlreadyPresent;
        public readonly long Bytes;
        internal HistoryArchiveResult(string manifest, int changed, int present, long bytes)
        { ManifestPath = manifest; Changed = changed; AlreadyPresent = present; Bytes = bytes; }
    }

    public sealed partial class JournalProject
    {
        private void VerifyArchiveSources()
        {
            EnsureOpen();
            TrackingStorage.Verify(PathName, baselineHash);
            TrackingStorage.Verify(JournalPath, journalHash);
            TrackingStorage.Verify(Path.Combine(DirectoryName, "project.json"), markerHash);
            foreach (var session in sessionHashes) TrackingStorage.Verify(session.Key, session.Value);
            var disk = Directory.GetFiles(Path.Combine(DirectoryName, "sessions"), "*.jsonl");
            if (disk.Length != sessions.Count || disk.Any(path => !sessionHashes.ContainsKey(path)))
                throw new IOException("Session files changed outside Scribe. Reopen and validate the project before managing history.");
        }

        private Dictionary<string, string> CompiledArchiveEntries()
        {
            var compiled = new Dictionary<string, string>(StringComparer.Ordinal);
            if (journalHash != null)
                ReadLines(JournalPath, false, null, line => {
                    ValidateLine(line);
                    Stamp stamp;
                    if (!entries.TryGetValue(line.Record.EntryId, out stamp) || !stamp.Compiled || stamp.Hash != line.Hash ||
                        compiled.ContainsKey(line.Record.EntryId))
                        throw new InvalidDataException("The main journal does not match the verified compiled history.");
                    compiled.Add(line.Record.EntryId, line.Hash);
                });
            return compiled;
        }

        private HistorySessionInfo InspectArchiveSession(string path, Dictionary<string, string> compiled)
        {
            RequirePlainPath(path);
            string hash = TrackingStorage.HashFile(path);
            SessionHeader header = null;
            string previous = null, fingerprint = null;
            int count = 0;
            bool complete = true;
            DateTime activity = DateTime.MinValue;
            ReadLines(path, true, value => {
                ValidateHeader(value); header = value; previous = value.ParentEntryId; fingerprint = value.ParentFingerprint;
                complete = entries[previous].Compiled && (previous == ProjectId || compiled.ContainsKey(previous));
                activity = DateTime.Parse(value.StartedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }, line => {
                ValidateLine(line);
                Stamp stamp;
                if (line.Record.SessionId != header.SessionId || line.Record.PreviousEntryId != previous ||
                    line.Record.PreviousFingerprint != fingerprint || !entries.TryGetValue(line.Record.EntryId, out stamp) ||
                    stamp.Hash != line.Hash)
                    throw new InvalidDataException("A session does not match its verified local recording chain.");
                string compiledHash;
                complete &= stamp.Compiled && compiled.TryGetValue(line.Record.EntryId, out compiledHash) && compiledHash == line.Hash;
                previous = line.Record.EntryId; fingerprint = line.Record.Packet.SourceFingerprint; count++;
                DateTime recorded = DateTime.Parse(line.Record.RecordedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if (recorded > activity) activity = recorded;
            });
            TrackingStorage.Verify(path, hash);
            return new HistorySessionInfo(header.SessionId, Path.GetFileName(path), hash, activity,
                new FileInfo(path).Length, count, String.Equals(path, SessionPath, StringComparison.OrdinalIgnoreCase), complete);
        }

        public HistoryStorageInventory HistoryInventory()
        {
            VerifyArchiveSources();
            var compiled = CompiledArchiveEntries();
            var result = sessions.Select(path => InspectArchiveSession(path, compiled)).ToList();
            if (result.Select(session => session.SessionId).Distinct(StringComparer.Ordinal).Count() != result.Count)
                throw new InvalidDataException("Duplicate session identities cannot be archived.");
            long baselineBytes = new FileInfo(PathName).Length;
            long journalBytes = journalHash == null ? 0 : new FileInfo(JournalPath).Length;
            long total = 0;
            foreach (string file in StorageFiles(DirectoryName))
                total = checked(total + new FileInfo(file).Length);
            foreach (string suffix in new[] { ".bak", ".legacy.bak" })
                if (File.Exists(PathName + suffix)) total = checked(total + new FileInfo(PathName + suffix).Length);
            long other = checked(total - journalBytes - result.Sum(session => session.Bytes));
            VerifyArchiveSources();
            return new HistoryStorageInventory(ProjectId, baselineHash, HeadId, Fingerprint, journalHash, markerHash,
                baselineBytes, journalBytes, other, result);
        }

        private static IEnumerable<string> StorageFiles(string directory)
        {
            RequirePlainPath(directory);
            foreach (string file in Directory.GetFiles(directory)) { RequirePlainPath(file); yield return file; }
            foreach (string child in Directory.GetDirectories(directory))
                foreach (string file in StorageFiles(child)) yield return file;
        }

        private static void RequirePlainPath(string path)
        {
            path = Path.GetFullPath(path);
            if (path.Substring(Path.GetPathRoot(path).Length).Contains(":"))
                throw new InvalidOperationException("History storage does not use alternate data streams.");
            for (string current = path; current != null; current = Path.GetDirectoryName(current))
            {
                string name = Path.GetFileName(current);
                if (name != name.TrimEnd(' ', '.'))
                    throw new InvalidOperationException("History paths cannot have trailing dots or spaces.");
                if ((File.Exists(current) || Directory.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("History archives do not follow junctions or symbolic links: " + current);
            }
        }

        internal static void RequireArchivePath(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                throw new InvalidOperationException("Choose an absolute private archive path.");
            string root = Path.GetPathRoot(path);
            if (root.Length == 1 || root.EndsWith(":", StringComparison.Ordinal))
                throw new InvalidOperationException("Choose a fully qualified drive or network archive path, not a drive-relative path.");
            ExportStorage.RequireLocation(path, null);
            RequirePlainPath(path);
        }

        private void VerifyArchivePreview(HistoryStorageInventory preview)
        {
            if (preview == null || preview.ProjectId != ProjectId || preview.BaselineHash != baselineHash ||
                preview.HeadId != HeadId || preview.Fingerprint != Fingerprint ||
                preview.JournalHash != journalHash || preview.MarkerHash != markerHash)
                throw new IOException("History changed after the preview. Refresh Manage History and preview again.");
        }

        public HistoryArchiveResult ArchiveSessions(HistoryStorageInventory preview, IEnumerable<string> selectedIds,
            string destination, CancellationToken cancellation)
        { return ArchiveSessions(preview, selectedIds, destination, cancellation, null); }

        internal HistoryArchiveResult ArchiveSessions(HistoryStorageInventory preview, IEnumerable<string> selectedIds,
            string destination, CancellationToken cancellation, Action<string> step)
        {
            VerifyArchivePreview(preview);
            RequireArchivePath(destination);
            if (!Directory.Exists(destination)) throw new DirectoryNotFoundException("Choose an existing private archive folder.");
            if (selectedIds == null) throw new ArgumentNullException("selectedIds");
            var ids = selectedIds.ToList();
            if (ids.Count == 0 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
                throw new InvalidOperationException("Select one or more distinct eligible sessions.");
            HistoryStorageInventory current = HistoryInventory();
            var selected = new List<HistorySessionInfo>();
            foreach (string id in ids)
            {
                HistorySessionInfo old = preview.Sessions.SingleOrDefault(session => session.SessionId == id);
                HistorySessionInfo item = current.Sessions.SingleOrDefault(session => session.SessionId == id);
                if (old == null || item == null || !old.Eligible || !item.Eligible || old.FileName != item.FileName ||
                    old.Sha256 != item.Sha256 || old.Bytes != item.Bytes)
                    throw new IOException("A selected session changed or is active/uncompiled. Refresh the archive preview.");
                selected.Add(item);
            }
            string archiveId = Guid.NewGuid().ToString("N");
            string folder = Path.Combine(destination, "Scribe-archive-" + archiveId);
            RequireArchivePath(folder);
            if (Directory.Exists(folder) || File.Exists(folder)) throw new IOException("The new archive location already exists.");
            string manifestPath = Path.Combine(folder, "manifest.json");
            var manifest = new HistoryArchiveManifest {
                ArchiveId = archiveId, ProjectId = ProjectId, BaselineHash = baselineHash, ModId = State.ModId,
                CreatedUtc = UtcNow(), HeadId = HeadId, Fingerprint = Fingerprint, JournalHash = journalHash,
                Sessions = selected.Select(session => new HistoryArchiveSession {
                    FileName = session.FileName, SessionId = session.SessionId, Sha256 = session.Sha256,
                    Bytes = session.Bytes, Recordings = session.Recordings }).ToList()
            };
            if (System.Text.Encoding.UTF8.GetByteCount(Json.Write(manifest)) > 8 * 1024 * 1024)
                throw new InvalidOperationException("This selection exceeds the supported 8 MiB archive manifest limit. Select fewer sessions.");
            cancellation.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(folder, "sessions"));
            try
            {
                foreach (HistorySessionInfo session in selected)
                {
                    cancellation.ThrowIfCancellationRequested();
                    CopyArchiveFile(Path.Combine(DirectoryName, "sessions", session.FileName),
                        Path.Combine(folder, "sessions", session.FileName), session.Sha256, session.Bytes);
                    if (step != null) step("copied");
                }
                cancellation.ThrowIfCancellationRequested();
                VerifyArchiveSources();
                string manifestHash = TrackingStorage.WriteAtomic(manifestPath, manifest, null,
                    value => { if (step != null) step("manifest-" + value); }, false);
                var verified = ReadArchiveManifest(manifestPath);
                if (verified.Item2 != manifestHash) throw new IOException("The committed archive manifest changed.");
                VerifyArchivedFiles(verified.Item1, folder, CompiledArchiveEntries());
                if (step != null) step("archive-verified");
                VerifyArchiveSources();
                using (var receipt = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (HashArchiveStream(receipt) != manifestHash) throw new IOException("The archive manifest changed before removal.");
                    foreach (HistorySessionInfo session in selected)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        string path = Path.Combine(DirectoryName, "sessions", session.FileName);
                        using (var archived = new FileStream(Path.Combine(folder, "sessions", session.FileName),
                            FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            if (archived.Length != session.Bytes || HashArchiveStream(archived) != session.Sha256)
                                throw new IOException("Archived bytes changed before original removal.");
                            if (step != null) step("before-remove");
                            TrackingStorage.Verify(PathName, baselineHash);
                            TrackingStorage.Verify(JournalPath, journalHash);
                            TrackingStorage.Verify(Path.Combine(DirectoryName, "project.json"), markerHash);
                            DeleteVerifiedSession(path, session.Sha256);
                            sessions.Remove(path); sessionHashes.Remove(path); compiledSessions.Remove(path);
                        }
                        if (step != null) step("removed");
                    }
                }
                VerifyArchiveSources();
                return new HistoryArchiveResult(manifestPath, selected.Count, 0, selected.Sum(session => session.Bytes));
            }
            catch (OperationCanceledException error)
            {
                throw new OperationCanceledException("Archival cancelled. Originals may be partly retained; no recordings were deleted. " +
                    "Keep the archive at " + folder + ". If manifest.json exists, use Restore Archive to restore verified copies.", error, cancellation);
            }
            catch (IOException error)
            {
                throw ArchiveFailure(folder, error);
            }
            catch (UnauthorizedAccessException error) { throw ArchiveFailure(folder, error); }
            catch (InvalidDataException error) { throw ArchiveFailure(folder, error); }
        }

        private static IOException ArchiveFailure(string folder, Exception error)
        {
            return new IOException("Archival did not complete. Preserve the archive at " + folder +
                " and all remaining originals. A committed manifest permits verified restore; incomplete copies without it are not a valid archive. " +
                "No main-journal recordings were removed. " + error.Message, error);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileDisposition
        {
            [MarshalAs(UnmanagedType.U1)] public bool DeleteFile;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security,
            uint creation, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int infoClass,
            ref FileDisposition information, uint size);

        private static void DeleteVerifiedSession(string path, string hash)
        {
            RequirePlainPath(path);
            // Delete the exact exclusively opened, hash-verified file, not a path that could be replaced between checks.
            using (SafeFileHandle handle = CreateFile(path, 0x80010000, 0, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new IOException("Cannot exclusively open the original session for removal: " + path,
                    new Win32Exception(Marshal.GetLastWin32Error()));
                using (var input = new FileStream(handle, FileAccess.Read))
                {
                    if (HashArchiveStream(input) != hash) throw new IOException("Session changed before guarded removal: " + path);
                    var disposition = new FileDisposition { DeleteFile = true };
                    if (!SetFileInformationByHandle(handle, 4, ref disposition, (uint)Marshal.SizeOf(disposition)))
                        throw new IOException("The verified original session could not be removed: " + path,
                            new Win32Exception(Marshal.GetLastWin32Error()));
                }
            }
        }

        private static void CopyArchiveFile(string source, string destination, string hash, long bytes)
        {
            RequirePlainPath(source);
            RequirePlainPath(destination);
            if (File.Exists(destination) || Directory.Exists(destination))
                throw new IOException("An archive/restore destination already exists: " + destination);
            string temporary = Path.Combine(Path.GetDirectoryName(destination), Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (input.Length != bytes || HashArchiveStream(input) != hash)
                        throw new IOException("Session bytes changed before copying: " + source);
                    input.Position = 0;
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { input.CopyTo(output); output.Flush(true); }
                }
                TrackingStorage.Verify(temporary, hash);
                File.Move(temporary, destination);
                TrackingStorage.Verify(destination, hash);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static string HashArchiveStream(Stream stream)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private Tuple<HistoryArchiveManifest, string> ReadArchiveManifest(string path)
        {
            RequireArchivePath(path);
            HistoryArchiveManifest manifest;
            string hash;
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length > 8 * 1024 * 1024) throw new InvalidDataException("Archive manifest exceeds the supported 8 MiB limit.");
                hash = ExportStorage.ObserveTarget(path, PathName);
                manifest = Json.ReadStream<HistoryArchiveManifest>(input);
            }
            if (manifest == null || manifest.Format != HistoryArchiveManifest.FormatName || manifest.SchemaVersion != 1 ||
                !IsId(manifest.ArchiveId) || manifest.ProjectId != ProjectId || manifest.BaselineHash != baselineHash ||
                manifest.ModId != State.ModId || !IsUtc(manifest.CreatedUtc) || !IsId(manifest.HeadId) ||
                !IsHash(manifest.Fingerprint) || (manifest.JournalHash != null && !IsHash(manifest.JournalHash)) ||
                manifest.Sessions == null || manifest.Sessions.Count == 0)
                throw new InvalidDataException("The archive manifest is invalid or belongs to another project/baseline.");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (HistoryArchiveSession session in manifest.Sessions)
                if (session == null || !IsId(session.SessionId) || !ids.Add(session.SessionId) ||
                    String.IsNullOrWhiteSpace(session.FileName) || session.FileName != Path.GetFileName(session.FileName) ||
                    session.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    session.FileName != session.FileName.TrimEnd(' ', '.') ||
                    !session.FileName.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase) || !files.Add(session.FileName) ||
                    !IsHash(session.Sha256) || session.Bytes <= 0 || session.Recordings < 0)
                    throw new InvalidDataException("The archive contains an invalid, duplicate or unsafe session entry.");
            TrackingStorage.Verify(path, hash);
            return Tuple.Create(manifest, hash);
        }

        private void VerifyArchivedFile(HistoryArchiveSession session, string folder, Dictionary<string, string> compiled)
        {
            string path = Path.Combine(folder, "sessions", session.FileName);
            RequirePlainPath(path);
            TrackingStorage.Verify(path, session.Sha256);
            HistorySessionInfo actual = InspectArchiveSession(path, compiled);
            if (!actual.FullyCompiled || actual.SessionId != session.SessionId || actual.Sha256 != session.Sha256 ||
                actual.Bytes != session.Bytes || actual.Recordings != session.Recordings)
                throw new InvalidDataException("Archived session bytes/identity do not match fully compiled evidence: " + session.FileName);
        }

        private void VerifyArchivedFiles(HistoryArchiveManifest manifest, string folder, Dictionary<string, string> compiled)
        {
            foreach (HistoryArchiveSession session in manifest.Sessions) VerifyArchivedFile(session, folder, compiled);
        }

        public HistoryRestorePreview PreviewRestoreArchive(string manifestPath)
        {
            VerifyArchiveSources();
            var loaded = ReadArchiveManifest(manifestPath);
            string folder = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
            VerifyArchivedFiles(loaded.Item1, folder, CompiledArchiveEntries());
            var observedNames = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string source in sessions)
            {
                string id = ReadHeader(source).SessionId;
                if (observedNames.ContainsKey(id)) throw new InvalidDataException("Duplicate observed session identities cannot be restored.");
                observedNames.Add(id, source);
            }
            int present = 0;
            foreach (HistoryArchiveSession session in loaded.Item1.Sessions)
            {
                string path = Path.Combine(DirectoryName, "sessions", session.FileName);
                if (Directory.Exists(path)) throw new IOException("A restore target is a directory: " + path);
                string existing;
                if (observedNames.TryGetValue(session.SessionId, out existing) &&
                    !String.Equals(existing, path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The archived session identity already exists under another filename.");
                if (!File.Exists(path)) continue;
                TrackingStorage.Verify(path, session.Sha256); present++;
            }
            TrackingStorage.Verify(manifestPath, loaded.Item2);
            VerifyArchiveSources();
            return new HistoryRestorePreview(Path.GetFullPath(manifestPath), loaded.Item2, loaded.Item1.Sessions, present);
        }

        public HistoryArchiveResult RestoreArchive(HistoryRestorePreview preview, CancellationToken cancellation)
        { return RestoreArchive(preview, cancellation, null); }

        internal HistoryArchiveResult RestoreArchive(HistoryRestorePreview preview, CancellationToken cancellation, Action<string> step)
        {
            if (preview == null) throw new ArgumentNullException("preview");
            TrackingStorage.Verify(preview.ManifestPath, preview.ManifestHash);
            HistoryRestorePreview current = PreviewRestoreArchive(preview.ManifestPath);
            if (current.ManifestHash != preview.ManifestHash)
                throw new IOException("The archive changed after the restore preview.");
            string folder = Path.GetDirectoryName(current.ManifestPath);
            int restored = 0, present = 0;
            long bytes = 0;
            try
            {
                foreach (HistoryArchiveSession session in current.Sessions)
                {
                    cancellation.ThrowIfCancellationRequested();
                    VerifyArchiveSources();
                    TrackingStorage.Verify(current.ManifestPath, current.ManifestHash);
                    string path = Path.Combine(DirectoryName, "sessions", session.FileName);
                    if (File.Exists(path)) { TrackingStorage.Verify(path, session.Sha256); present++; continue; }
                    if (step != null) step("before-restore");
                    CopyArchiveFile(Path.Combine(folder, "sessions", session.FileName), path, session.Sha256, session.Bytes);
                    sessions.Add(path); sessionHashes.Add(path, session.Sha256); compiledSessions.Add(path);
                    restored++; bytes = checked(bytes + session.Bytes);
                    if (step != null) step("restored");
                }
                VerifyArchiveSources();
                return new HistoryArchiveResult(current.ManifestPath, restored, present, bytes);
            }
            catch (OperationCanceledException error)
            {
                throw new OperationCanceledException("Restore cancelled after " + restored +
                    " originals restored. Archive copies remain at " + folder + "; retry restores only missing originals.", error, cancellation);
            }
            catch (IOException error)
            {
                throw new IOException("Restore did not complete; " + restored + " originals were restored. " +
                    "Keep the archive at " + folder + " and remaining project files. Nothing was overwritten. " + error.Message, error);
            }
            catch (UnauthorizedAccessException error)
            {
                throw new IOException("Restore could not write an original session after " + restored +
                    " restored. Archive copies remain at " + folder + ". " + error.Message, error);
            }
        }
    }
}
