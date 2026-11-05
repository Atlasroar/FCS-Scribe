using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace FcsScribe
{
    public static class MarkdownExport
    {
        public static string Render(Packet packet)
        {
            TrackingDocument.ValidatePacket(packet);
            return RenderDocument(packet, null);
        }

        public static string RenderReviewed(Packet packet, NoteDraft draft, Snapshot current)
        {
            TrackingDocument.ValidatePacket(packet);
            if (current == null) throw new ArgumentNullException("current");
            DraftValidation.Pending(packet, draft, current, delegate { return ""; });
            return RenderDocument(packet, draft);
        }

        private static string RenderDocument(Packet packet, NoteDraft draft)
        {
            var text = new StringBuilder();
            var evidence = packet.Changes.Select((change, index) => new { change.Id,
                Anchor = "evidence-" + (index + 1).ToString("D6", CultureInfo.InvariantCulture) })
                .ToDictionary(x => x.Id, x => x.Anchor, StringComparer.Ordinal);
            Line(text, "# " + Inline(packet.Context.ModName) + " - change catalogue");
            Line(text);
            Line(text, "Partial captured evidence, not a vanilla comparison, net diff or complete event log.");
            Line(text, Tracker.SamplingNotice);
            Line(text, "Stored/active and effective rows are evidence views of an observation, not separate gameplay edits. " +
                "Repeated edits and reversals remain separate recordings; an earlier After value is not necessarily current.");
            Line(text, "Base/locked field ownership remains unresolved. Load order and declared dependencies do not establish field provenance. " +
                "Workshop context is local/staged/cached, not verified Steam activity.");
            Line(text, "This export does not record, compile, apply native notes, save the mod or replace the editable draft/workspace.");
            Line(text);
            Line(text, "- Batch ID: " + Inline(packet.BatchId));
            Line(text, "- Baseline ID: " + Inline(packet.BaselineId));
            Line(text, "- Baseline captured UTC: " + Inline(packet.BaselineCapturedUtc));
            Line(text, "- Packet exported UTC: " + Inline(packet.ExportedUtc));
            Line(text, "- Packet serialization SHA-256: " + Json.StreamHash(packet));
            Line(text, "- Current-at-export fingerprint: " + Inline(packet.SourceFingerprint));
            Line(text, "- Evidence rows: " + packet.Changes.Count.ToString(CultureInfo.InvariantCulture));
            if (packet.SchemaVersion == 3)
            {
                Line(text, "- Project ID: " + Inline(packet.ProjectId));
                Line(text, "- Selected recordings: " + packet.Recordings.Count.ToString(CultureInfo.InvariantCulture));
                Line(text, "The outer context/fingerprint describes the editor at export, not the end of the selected range.");
            }
            Line(text);
            Line(text, "## Current-at-export context and warnings");
            Context(text, packet.Context);
            Line(text, "## Captured facts");
            if (packet.SchemaVersion == 3)
            {
                var changes = packet.Changes.ToDictionary(x => x.Id, StringComparer.Ordinal);
                for (int i = 0; i < packet.Recordings.Count; i++)
                {
                    RecordingEvidence recording = packet.Recordings[i];
                    Line(text, "### Recording " + (i + 1).ToString(CultureInfo.InvariantCulture) + ": " +
                        Inline(recording.RecordedUtc) + " (" + Inline(recording.Kind) + ")");
                    Line(text);
                    Line(text, "- Entry ID: " + Inline(recording.EntryId));
                    Line(text, "- Session ID: " + Inline(recording.SessionId));
                    Line(text, "- Original batch ID: " + Inline(recording.Packet.BatchId));
                    Line(text, "- Original source fingerprint: " + Inline(recording.Packet.SourceFingerprint));
                    if (recording.Kind == "pending")
                        Line(text, "**Unrecorded editor state: this entry is not saved in a session log.**");
                    Context(text, recording.Packet.Context);
                    Facts(text, recording.Packet.Changes.Select(x => changes[recording.EntryId + ":" + x.Id]), evidence);
                }
            }
            else Facts(text, packet.Changes, evidence);
            if (packet.Changes.Count == 0) Line(text, "No record-change evidence in this packet; this is not an all-clear.");
            if (draft != null)
            {
                Line(text, "## Reviewed draft prose");
                Line(text);
                Line(text, "User-selected draft text, separate from captured facts. Interpretations are not independently verified " +
                    "gameplay effects; exporting is not native approval or proof these notes were applied.");
                Line(text);
                foreach (NoteEntry entry in draft.Entries.OrderBy(x => x.TargetRecordId, StringComparer.Ordinal))
                {
                    Line(text, "### Target " + Inline(entry.TargetRecordId));
                    Line(text);
                    foreach (string line in entry.Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                        Line(text, "> " + Inline(line));
                    Line(text);
                    Line(text, "Evidence: " + String.Join(", ", entry.ChangeIds.OrderBy(x => evidence[x], StringComparer.Ordinal)
                        .Select(x => "[" + Inline(x) + "](#" + evidence[x] + ")")));
                    Line(text);
                }
            }
            return ExportStorage.MarkdownDocument(text.ToString());
        }

        private static void Context(StringBuilder text, Snapshot context)
        {
            Line(text, "- Mod ID: " + Inline(context.ModId));
            Line(text, "- Capture scope: " + Inline(context.Scope));
            Line(text, "- Load order (in captured order): " + List(context.LoadOrder));
            Line(text, "- Declared dependencies: " + List(context.Dependencies));
            Line(text, "- Locked context: " + List(context.LockedMods));
            foreach (string warning in context.Warnings) Line(text, "- WARNING: " + Inline(warning));
            if (context.Workshop != null)
            {
                Line(text, "- Workshop source: " + Inline(context.Workshop.Source));
                Line(text, "- Workshop capture status: " + Inline(context.Workshop.CaptureStatus));
                if (context.Workshop.Warnings != null)
                    foreach (string warning in context.Workshop.Warnings) Line(text, "- WORKSHOP WARNING: " + Inline(warning));
                if (context.Workshop.Issues != null)
                    foreach (WorkshopIssue issue in context.Workshop.Issues)
                    {
                        if (issue == null) throw new InvalidDataException("The Workshop availability context contains a missing issue.");
                        Line(text, "- WORKSHOP AVAILABILITY: " + Inline(issue.Component) + " / " +
                            Inline(issue.Code) + " / " + Inline(issue.Message));
                    }
            }
            Line(text);
        }

        private static void Facts(StringBuilder text, IEnumerable<Change> changes, IDictionary<string, string> evidence)
        {
            foreach (var record in changes.GroupBy(x => x.RecordId).OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                Change display = record.First();
                Line(text, "#### " + Inline(display.RecordType) + ": " + Inline(display.DisplayName));
                Line(text);
                Line(text, "Record ID: " + Inline(record.Key));
                Line(text);
                foreach (var field in record.GroupBy(x => x.Path).OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    Line(text, "Field/reference path: " + Inline(field.Key));
                    Line(text);
                    Line(text, "| Evidence ID | Operation | Layer | Before | After | Before source | After source | Reference |");
                    Line(text, "|---|---|---|---|---|---|---|---|");
                    foreach (Change change in field)
                        Line(text, "| <a id=\"" + evidence[change.Id] + "\"></a>" + Inline(change.Id) + " | " +
                            Inline(change.Operation) + " | " + Inline(change.Layer) + " | " + ValueText(change.Before) + " | " +
                            ValueText(change.After) + " | " + Inline(change.BeforeSource) + " | " + Inline(change.AfterSource) +
                            " | " + (change.ReferencedId == null ? "Not applicable" : Inline(change.ReferencedId) + " (" +
                                (change.ReferencedName == null ? "UNRESOLVED" : Inline(change.ReferencedName)) + ")") + " |");
                    Line(text);
                }
            }
        }

        private static string ValueText(Value value)
        {
            return Inline(value.Kind) + ": " + (value.Text == null ? "(no payload)" : Inline("\"" + Literal(value.Text) + "\""));
        }

        private static string List(IEnumerable<string> values)
        {
            return values.Any() ? String.Join(", ", values.Select(Inline)) : "None captured";
        }

        private static string Literal(string value)
        {
            var text = new StringBuilder();
            foreach (char c in value)
            {
                if (c == '\\' || c == '"') text.Append('\\').Append(c);
                else if (c == '\n') text.Append("\\n");
                else if (c == '\r') text.Append("\\r");
                else if (c == '\t') text.Append("\\t");
                else if (Char.IsControl(c)) text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else text.Append(c);
            }
            return text.ToString();
        }

        private static string Inline(string value)
        {
            if (value == null) return "Not reported";
            var text = new StringBuilder();
            foreach (char c in value)
            {
                if (c == '&') text.Append("&amp;");
                else if (c == '<') text.Append("&lt;");
                else if (c == '>') text.Append("&gt;");
                // GFM auto-links must not reinterpret literal field/draft URLs.
                else if (c == ':') text.Append("&#58;");
                else if (Char.IsControl(c))
                    text.Append("\\\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else
                {
                    if ("\\`*_{}[]()!#|~.-+".IndexOf(c) >= 0) text.Append('\\');
                    text.Append(c);
                }
            }
            return text.ToString();
        }

        private static void Line(StringBuilder text, string value = "")
        {
            text.Append(value).Append('\n');
        }
    }
}
