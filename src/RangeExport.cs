using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace FcsScribe
{
    [DataContract]
    public sealed class RecordingEvidence
    {
        [DataMember(Order = 0)] public string EntryId;
        [DataMember(Order = 1)] public string SessionId;
        [DataMember(Order = 2)] public string RecordedUtc;
        [DataMember(Order = 3)] public string Kind;
        [DataMember(Order = 4)] public Packet Packet;
    }

    public static class RangeExport
    {
        public static Packet Create(string projectId, IList<RecordingEvidence> available, int first, int last, Snapshot current)
        {
            if (available == null || first < 0 || last < first || last >= available.Count)
                throw new InvalidDataException("Choose a valid inclusive recording range.");
            var selected = Json.Read<List<RecordingEvidence>>(Json.Write(available.Skip(first).Take(last - first + 1).ToList()));
            ValidateRecordings(selected, current);
            List<Change> changes = Changes(selected);
            if (changes.Count == 0)
                throw new InvalidOperationException("This range contains only context updates, not record-change evidence. Choose a range containing record changes.");
            Packet packet = Tracker.CreatePacket(Baseline(projectId, selected),
                selected[0].Packet.BaselineCapturedUtc, current, changes);
            packet.SchemaVersion = 3;
            packet.ProjectId = projectId;
            packet.Recordings = selected;
            packet.Instructions += " This is an ordered RECORDING RANGE, not a net diff or a complete event log. " +
                "Changes uses EntryId:original-change-ID citations; use these qualified IDs in the response. " +
                "Recordings preserves each original packet, batch identity, timestamp, session and context. " +
                "Use ONLY the OUTER ResponseTemplate; nested templates and unqualified IDs are archival, not the response contract. " +
                "Repeated edits and reversals are separate observations; do not describe an earlier After value as the current value. " +
                "A pending entry is unrecorded editor state, not a saved session entry. Context-only entries supply no new record evidence. " +
                "The outer Context and SourceFingerprint describe the CURRENT editor state, not the end of the selected range. " +
                "Review warnings from every selected recording as well as current Context.";
            TrackingDocument.ValidatePacket(packet);
            return packet;
        }

        internal static List<Change> Changes(IEnumerable<RecordingEvidence> recordings)
        {
            var changes = new List<Change>();
            foreach (RecordingEvidence recording in recordings)
                foreach (Change original in recording.Packet.Changes)
                {
                    Change copy = Json.Read<Change>(Json.Write(original));
                    copy.Id = recording.EntryId + ":" + original.Id;
                    changes.Add(copy);
                }
            return changes;
        }

        private static string Baseline(string projectId, IEnumerable<RecordingEvidence> recordings)
        {
            Guid identity;
            if (!Guid.TryParse(projectId, out identity))
                throw new InvalidDataException("The recording range has no valid project identity.");
            return Json.Hash("fcs-scribe-recording-range-v1:" + projectId + ":" +
                String.Join(":", recordings.Select(x => x.EntryId + ":" + x.Packet.BatchId)));
        }

        internal static void Validate(Packet packet)
        {
            ValidateRecordings(packet.Recordings, packet.Context);
            if (packet.Changes.Count == 0 || packet.BaselineId != Baseline(packet.ProjectId, packet.Recordings) ||
                Json.Write(packet.Changes) != Json.Write(Changes(packet.Recordings)) ||
                packet.Recordings.Count(x => x.Kind == "pending") > 1 ||
                packet.Recordings.Any(x => x.Kind == "pending" &&
                    (x != packet.Recordings.Last() || x.Packet.SourceFingerprint != packet.SourceFingerprint)))
                throw new InvalidDataException("The range does not match its original recording evidence.");
        }

        private static void ValidateRecordings(IList<RecordingEvidence> recordings, Snapshot current)
        {
            if (recordings == null || recordings.Count == 0)
                throw new InvalidDataException("The range has no original recordings.");
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (RecordingEvidence recording in recordings)
            {
                DateTime utc;
                Guid identity;
                if (recording == null || String.IsNullOrEmpty(recording.EntryId) || recording.EntryId.Contains(":") ||
                    !identities.Add(recording.EntryId) || recording.Packet == null ||
                    (recording.Packet.SchemaVersion != 1 && recording.Packet.SchemaVersion != 2) ||
                    (recording.Kind != "recorded" && recording.Kind != "pending" &&
                    recording.Kind != "checkpoint" && recording.Kind != "imported") ||
                    (recording.Kind == "recorded" && (!Guid.TryParse(recording.EntryId, out identity) ||
                    !Guid.TryParse(recording.SessionId, out identity))) ||
                    (recording.SessionId != null && !Guid.TryParse(recording.SessionId, out identity)) ||
                    !DateTime.TryParse(recording.RecordedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out utc) ||
                    utc.Kind != DateTimeKind.Utc)
                    throw new InvalidDataException("Invalid, duplicate or unsupported original recording.");
                TrackingDocument.ValidatePacket(recording.Packet);
                if (recording.Packet.Context.ModId != current.ModId || recording.Packet.Context.ModName != current.ModName)
                    throw new InvalidDataException("The recording belongs to another mod or project path.");
            }
        }
    }
}
