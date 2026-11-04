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
    public sealed class TrackingEntry
    {
        [DataMember(Order = 0)] public string PreviousFingerprint;
        [DataMember(Order = 1)] public Packet Packet;
    }

    [DataContract]
    public sealed class TrackingDocument
    {
        public const string FormatName = "FCS-Scribe tracking project";
        [DataMember(Order = 0)] public string Format = FormatName;
        [DataMember(Order = 1)] public int SchemaVersion = 1;
        [DataMember(Order = 2)] public string InitialFingerprint;
        [DataMember(Order = 3)] public string StateFingerprint;
        [DataMember(Order = 4)] public string BaselineId;
        [DataMember(Order = 5)] public string ValidatedUtc;
        [DataMember(Order = 6)] public Snapshot State;
        [DataMember(Order = 7)] public List<TrackingEntry> Entries = new List<TrackingEntry>();
        [DataMember(Order = 8)] public List<Packet> ImportedEvidence = new List<Packet>();
        [DataMember(Order = 9, EmitDefaultValue = false)] public int JournalVersion;

        public static TrackingDocument Create(Snapshot state, Packet legacy)
        {
            string fingerprint = state.Fingerprint();
            var document = new TrackingDocument {
                State = state, StateFingerprint = fingerprint, InitialFingerprint = fingerprint,
                BaselineId = Guid.NewGuid().ToString("N"), ValidatedUtc = UtcNow()
            };
            if (legacy != null)
            {
                ValidatePacket(legacy);
                RequireMod(legacy.Context, state);
                document.ImportedEvidence.Add(legacy);
            }
            return document;
        }

        public void Validate()
        {
            Guid identity;
            DateTime timestamp;
            if (Format != FormatName || SchemaVersion != 1 || (JournalVersion != 0 && JournalVersion != 1) ||
                State == null || Entries == null ||
                ImportedEvidence == null || !Guid.TryParse(BaselineId, out identity) ||
                !DateTime.TryParse(ValidatedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out timestamp) ||
                timestamp.Kind != DateTimeKind.Utc || !IsHash(InitialFingerprint) || !IsHash(StateFingerprint))
                throw new InvalidDataException("The tracking document has an unsupported format or incomplete checkpoint.");
            if (State.Fingerprint() != StateFingerprint)
                throw new InvalidDataException("The saved snapshot does not match its checkpoint fingerprint.");
            string previous = InitialFingerprint;
            foreach (TrackingEntry entry in Entries)
            {
                if (entry == null || entry.PreviousFingerprint != previous)
                    throw new InvalidDataException("The tracking history has a broken checkpoint chain.");
                ValidatePacket(entry.Packet);
                RequireMod(entry.Packet.Context, State);
                previous = entry.Packet.SourceFingerprint;
            }
            if (previous != StateFingerprint)
                throw new InvalidDataException("The last validated entry does not match the saved snapshot.");
            foreach (Packet packet in ImportedEvidence) { ValidatePacket(packet); RequireMod(packet.Context, State); }
        }

        public Tracker ResumeTracker() { return new Tracker(State, BaselineId, ValidatedUtc); }

        public TrackingDocument Next(Snapshot current, Packet packet)
        {
            RequireMod(current, State);
            if (packet.BaselineId != BaselineId || packet.SourceFingerprint != current.Fingerprint())
                throw new InvalidDataException("The review does not belong to this checkpoint.");
            var next = Create(current, null);
            next.InitialFingerprint = InitialFingerprint;
            next.Entries = Entries.Concat(new[] { new TrackingEntry {
                PreviousFingerprint = StateFingerprint, Packet = packet
            }}).ToList();
            next.ImportedEvidence = ImportedEvidence.ToList();
            next.Validate();
            return next;
        }

        public Packet LatestEvidence(string fingerprint)
        {
            return Entries.Select(x => x.Packet).LastOrDefault(x => x.Changes.Count > 0 && x.SourceFingerprint == fingerprint);
        }

        private static void RequireMod(Snapshot left, Snapshot right)
        {
            if (left.ModId != right.ModId || left.ModName != right.ModName)
                throw new InvalidOperationException("The JSON belongs to a different active mod or project path. Nothing was overwritten.");
        }

        public static void ValidatePacket(Packet packet)
        {
            if (packet == null || (packet.SchemaVersion != 1 && packet.SchemaVersion != 2 && packet.SchemaVersion != 3) ||
                packet.Context == null || packet.Changes == null || String.IsNullOrEmpty(packet.BaselineId) ||
                !IsHash(packet.SourceFingerprint) || packet.ResponseTemplate == null)
                throw new InvalidDataException("The exported AI packet is incomplete or unsupported.");
            packet.Context.Validate();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Change change in packet.Changes)
                if (change == null || String.IsNullOrEmpty(change.Id) || !ids.Add(change.Id) ||
                    String.IsNullOrEmpty(change.RecordId) || String.IsNullOrEmpty(change.RecordType) ||
                    String.IsNullOrEmpty(change.Operation) || String.IsNullOrEmpty(change.Path) || !change.Path.StartsWith("/", StringComparison.Ordinal) ||
                    String.IsNullOrEmpty(change.Layer) || change.Before == null || change.After == null ||
                    String.IsNullOrEmpty(change.Before.Kind) || String.IsNullOrEmpty(change.After.Kind))
                    throw new InvalidDataException("The packet contains incomplete or duplicate change evidence.");
            if (packet.BatchId != Json.Hash(packet.BaselineId + ":" + Json.Write(packet.Changes)) ||
                packet.ResponseTemplate.BatchId != packet.BatchId ||
                packet.ResponseTemplate.SourceFingerprint != packet.SourceFingerprint ||
                packet.ResponseTemplate.Entries == null ||
                packet.ResponseTemplate.Entries.Any(x => x == null || x.ChangeIds == null || x.ChangeIds.Any(id => !ids.Contains(id))))
                throw new InvalidDataException("The packet's batch or response-template evidence is inconsistent.");
            var cited = packet.ResponseTemplate.Entries.SelectMany(x => x.ChangeIds).ToList();
            if (cited.Count != ids.Count || cited.Distinct(StringComparer.Ordinal).Count() != ids.Count)
                throw new InvalidDataException("The response template does not cover each change exactly once.");
            if (packet.SchemaVersion == 3) RangeExport.Validate(packet);
            else if (packet.ProjectId != null || packet.Recordings != null)
                throw new InvalidDataException("Recording ranges require AI packet schema 3.");
        }

        private static bool IsHash(string value)
        {
            return value != null && value.Length == 64 && value.All(c => "0123456789abcdef".Contains(c));
        }

        private static string UtcNow() { return DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture); }
    }

    public sealed class LoadedTracking
    {
        public TrackingDocument Document;
        public Packet Legacy;
        public string FileHash;
    }

    public static class TrackingStorage
    {
        public static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        public static LoadedTracking Load(string path)
        {
            RequireJson(path);
            using (var stream = File.OpenRead(path))
            {
                var result = new LoadedTracking();
                TrackingDocument document = Json.ReadStream<TrackingDocument>(stream);
                if (document == null) throw new InvalidDataException("The JSON is empty.");
                if (document.Format == TrackingDocument.FormatName)
                {
                    document.Validate();
                    document.State.ShareStrings();
                    result.Document = document;
                }
                else if (document.Format != null) throw new InvalidDataException("Unknown tracking document format.");
                else
                {
                    stream.Position = 0;
                    result.Legacy = Json.ReadStream<Packet>(stream);
                    TrackingDocument.ValidatePacket(result.Legacy);
                    if (result.Legacy.SchemaVersion == 3)
                        throw new InvalidDataException("A recording-range AI export is not a tracking baseline. Resume the original tracking JSON with its .scribe folder.");
                }
                stream.Position = 0;
                using (var sha = SHA256.Create())
                    result.FileHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                return result;
            }
        }

        public static void PreserveLegacy(string path, string expectedHash)
        {
            CheckUnchanged(path, expectedHash);
            string backup = path + ".legacy.bak";
            if (File.Exists(backup))
            {
                if (HashFile(backup) != expectedHash)
                    throw new IOException("A different legacy backup already exists; choose a different tracking file.");
            }
            else File.Copy(path, backup, false);
            if (HashFile(backup) != expectedHash) throw new IOException("The legacy export changed while it was being preserved.");
        }

        public static string Save(string path, TrackingDocument document, string expectedHash)
        {
            document.Validate();
            return WriteAtomic(path, document, expectedHash);
        }

        public static void Verify(string path, string expectedHash) { CheckUnchanged(path, expectedHash); }

        public static string WriteAtomic<T>(string path, T value, string expectedHash)
        {
            return WriteAtomic(path, value, expectedHash, null);
        }

        internal static string WriteAtomic<T>(string path, T value, string expectedHash, Action<string> checkpoint,
            bool preserveBackup = true, bool pretty = false)
        {
            RequireJson(path);
            CheckUnchanged(path, expectedHash);
            string temporary = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    if (checkpoint != null) checkpoint("write");
                    if (pretty)
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(Json.Pretty(Json.Write(value)));
                        output.Write(bytes, 0, bytes.Length);
                    }
                    else Json.WriteStream(output, value);
                    if (checkpoint != null) checkpoint("flush");
                    output.Flush(true);
                }
                string hash = HashFile(temporary);
                if (checkpoint != null) checkpoint("commit");
                CheckUnchanged(path, expectedHash);
                if (expectedHash == null) File.Move(temporary, path);
                else File.Replace(temporary, path, preserveBackup ? path + ".bak" : null);
                if (checkpoint != null) checkpoint("committed");
                return hash;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static void CheckUnchanged(string path, string expectedHash)
        {
            if (expectedHash == null)
            {
                if (File.Exists(path)) throw new IOException("This JSON already exists. Resume it or choose a new filename.");
            }
            else if (!File.Exists(path) || HashFile(path) != expectedHash)
                throw new IOException("The JSON changed or disappeared outside Scribe. Automatic review is paused; reload it before saving.");
        }

        public static void RequireJson(string path)
        {
            if (!String.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Choose a .json file; mod and game files are never tracking targets.");
        }
    }

    [DataContract]
    public sealed class PluginSettings
    {
        [DataMember(Order = 0, IsRequired = true)] public int SchemaVersion = 1;
        [DataMember(Order = 1, IsRequired = true)] public int ReviewSeconds;
        [DataMember(Order = 2)] public string ExportDirectory;
        public static readonly int[] Intervals = { 0, 30, 60, 300, 600 };
        public static string DefaultPath { get { return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FCS-Scribe", "settings.json"); } }

        public void Validate()
        {
            if (SchemaVersion != 1)
                throw new InvalidDataException("Unsupported settings schema. Expected schema 1; reload or explicitly save repaired settings.");
            if (!Intervals.Contains(ReviewSeconds))
                throw new InvalidDataException("Choose Manual, 30 seconds, 1 minute, 5 minutes or 10 minutes.");
            if (String.IsNullOrEmpty(ExportDirectory)) return;
            try
            {
                if (!Path.IsPathRooted(ExportDirectory) || Path.GetPathRoot(ExportDirectory).Length < 3)
                    throw new InvalidDataException("The custom export directory must be an absolute path.");
                Path.GetFullPath(ExportDirectory);
            }
            catch (ArgumentException error) { throw new InvalidDataException("The custom export directory is invalid.", error); }
            catch (NotSupportedException error) { throw new InvalidDataException("The custom export directory is unsupported.", error); }
            catch (PathTooLongException error) { throw new InvalidDataException("The custom export directory is too long.", error); }
        }

        public void ValidateForUse()
        {
            Validate();
            if (!String.IsNullOrEmpty(ExportDirectory) && !Directory.Exists(ExportDirectory))
                throw new DirectoryNotFoundException("The custom export directory is missing or unavailable: " + ExportDirectory);
        }

        public string DirectoryFor(string activeModDirectory)
        {
            Validate();
            string directory = String.IsNullOrEmpty(ExportDirectory) ? activeModDirectory : ExportDirectory;
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("The export directory does not exist: " + directory);
            return Path.GetFullPath(directory);
        }

        public static PluginSettings Load(string path)
        {
            string hash;
            return Load(path, out hash);
        }

        internal static PluginSettings Load(string path, out string hash)
        {
            hash = null;
            FileStream input;
            try { input = File.OpenRead(path); }
            catch (FileNotFoundException) { return new PluginSettings(); }
            catch (DirectoryNotFoundException) { return new PluginSettings(); }
            using (var stream = input)
            {
                using (var sha = SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                stream.Position = 0;
                var settings = Json.ReadStream<PluginSettings>(stream);
                if (settings == null) throw new InvalidDataException("Plugin settings are empty.");
                settings.ValidateForUse();
                return settings;
            }
        }
    }
}
