using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using FcsScribe;

internal static class MarkdownTests
{
    private static int passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAILED: " + name);
        passed++;
    }

    private static void Reject<T>(Action action, string name) where T : Exception
    {
        try { action(); }
        catch (Exception error) { if (error is T) { passed++; return; } throw; }
        throw new Exception("FAILED to reject: " + name);
    }

    private static Snapshot State(string value)
    {
        return new Snapshot { ModId = "markdown-fixture", ModName = "fixture.mod",
            LoadOrder = new List<string> { "base.mod", "fixture.mod" },
            Dependencies = new List<string> { "base.mod" }, LockedMods = new List<string> { "locked.mod" },
            Records = new List<Record> { new Record { Id = "item", Type = "ITEM", Name = "Item",
                Cells = new List<Cell> {
                    new Cell { Path = "/fields/value", Layer = "active", Source = "fixture.mod", Value = Value.Of("Int32", value) },
                    new Cell { Path = "/fields/value", Layer = "effective", Source = "fixture.mod", Value = Value.Of("Int32", value) }
                } } } };
    }

    private static Snapshot Copy(Snapshot state) { return Json.Read<Snapshot>(Json.Write(state)); }
    private static Packet Packet(Snapshot before, Snapshot after) { return new Tracker(before).Export(after); }
    private static NoteDraft Draft(Packet packet)
    {
        NoteDraft draft = Json.Read<NoteDraft>(Json.Write(packet.ResponseTemplate));
        foreach (NoteEntry entry in draft.Entries) entry.Text = "Reviewed [link](https://example.invalid) ![image](x).\n# <script>not a heading</script>";
        return draft;
    }

    public static int Run()
    {
        Snapshot before = State("10"), after = State("11");
        before.Warnings.Add("Earlier warning | <b>literal</b>");
        after.Warnings.Add("Current warning");
        Packet packet = Packet(before, after);
        string original = Json.Write(packet), text = MarkdownExport.Render(packet);
        Check(text == MarkdownExport.Render(packet) && Json.Write(packet) == original,
            "Markdown is deterministic for the same packet and does not mutate evidence");
        Check(text.Contains("Int32: \"10\"") && text.Contains("Int32: \"11\"") &&
            text.Contains("| active |") && text.Contains("| effective |") && text.Contains("fixture\\.mod"),
            "factual rows retain exact typed values, layers and source labels");
        Check(Regex.Matches(text, "Field/reference path: /fields/value").Count == 1 &&
            Regex.Matches(text, "<a id=\"evidence-").Count == packet.Changes.Count &&
            text.Contains("not separate gameplay edits"), "stored/effective rows share a field section, with one anchor per evidence view");
        Check(text.Contains(packet.BatchId) && text.Contains(packet.BaselineId) &&
            text.Contains(packet.SourceFingerprint) && text.Contains(Json.StreamHash(packet)) &&
            text.Contains("not a vanilla comparison") && text.Contains("Base/locked field ownership remains unresolved") &&
            text.Contains("Current warning"), "Markdown retains auditable identity and partial/provenance caveats");
        CultureInfo culture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
            Check(MarkdownExport.Render(packet) == text, "Markdown output is independent of the process culture");
        }
        finally { Thread.CurrentThread.CurrentCulture = culture; }

        Snapshot valuesBefore = State("0"), valuesAfter = State("-1");
        var kinds = new[] { "boolean", "string", "null", "float64", "UInt64", "string", "removed-reference" };
        var oldValues = new[] { "false", "", null, "0.000000000000000123", "18446744073709551615", "line\r\n|\\\"`\u0000", null };
        var newValues = new[] { "true", "null", "present", "9007199254740993.5", "18446744073709551614", "other", "restored" };
        for (int i = 0; i < kinds.Length; i++)
        {
            valuesBefore.Records[0].Cells.Add(new Cell { Path = "/fields/typed-" + i, Layer = "active",
                Value = Value.Of(kinds[i], oldValues[i]) });
            valuesAfter.Records[0].Cells.Add(new Cell { Path = "/fields/typed-" + i, Layer = "active",
                Value = Value.Of(kinds[i], newValues[i]), ReferencedId = i == 6 ? "missing-reference" : null });
        }
        string typed = MarkdownExport.Render(Packet(valuesBefore, valuesAfter));
        Check(typed.Contains("boolean: \"false\"") && typed.Contains("string: \"\"") &&
            typed.Contains("null: (no payload)") && typed.Contains("string: \"null\"") &&
            typed.Contains("removed\\-reference: (no payload)"),
            "false, empty, null, literal null and removed-reference remain distinct");
        Check(typed.Contains("18446744073709551615") && typed.Contains("9007199254740993\\.5") &&
            typed.Contains("0\\.000000000000000123") && typed.Contains("Int32: \"\\-1\""),
            "large integers, float text and negative quantities are never parsed or rounded");
        Check(typed.Contains("\\\\r\\\\n") && typed.Contains("\\\\u0000") &&
            typed.Contains("missing\\-reference (UNRESOLVED)"), "literal controls and unresolved references stay visible");
        Snapshot added = Copy(before);
        added.Records[0].Cells.Add(new Cell { Path = "/fields/new", Layer = "active", Value = Value.Of("string", "") });
        Check(MarkdownExport.Render(Packet(before, added)).Contains("missing: (no payload)"),
            "a missing value is not rendered as empty or null");

        Snapshot unsafeBefore = State("10"), unsafeAfter = State("11");
        unsafeBefore.ModName = unsafeAfter.ModName = "| <script> ![image](x) & \u2603";
        unsafeAfter.Records[0].Name = "# fake\n| <img src=x> [link](x) `code`";
        unsafeAfter.Warnings.Add("</table>\n# fake warning");
        string escaped = MarkdownExport.Render(Packet(unsafeBefore, unsafeAfter));
        Check(!escaped.Contains("<script>") && !escaped.Contains("<img") && !escaped.Contains("![image]") &&
            !escaped.Contains("\n# fake") && escaped.Contains("&lt;script&gt;") && escaped.Contains("\\|"),
            "untrusted names/warnings cannot introduce HTML, images, links, headings or table cells");
        unsafeAfter.Warnings.Add("https://example.invalid www.example.invalid");
        string urls = MarkdownExport.Render(Packet(unsafeBefore, unsafeAfter));
        Check(!urls.Contains("https://example") && urls.Contains("https&#58;//example") &&
            urls.Contains("www\\.example"), "literal URLs do not retain GFM auto-link triggers");

        Snapshot middle = Copy(after); middle.Warnings.Add("Historical context-only warning");
        Snapshot reverted = Copy(before);
        Snapshot pending = Copy(reverted); pending.Warnings.Add("Pending context warning");
        string session = Guid.NewGuid().ToString("N");
        var recordings = new List<RecordingEvidence> {
            new RecordingEvidence { EntryId = Guid.NewGuid().ToString("N"), SessionId = session,
                RecordedUtc = "2026-11-05T10:00:00Z", Kind = "recorded", Packet = packet },
            new RecordingEvidence { EntryId = Guid.NewGuid().ToString("N"), SessionId = session,
                RecordedUtc = "2026-11-05T10:01:00Z", Kind = "recorded", Packet = Packet(after, middle) },
            new RecordingEvidence { EntryId = Guid.NewGuid().ToString("N"), SessionId = session,
                RecordedUtc = "2026-11-05T10:02:00Z", Kind = "recorded", Packet = Packet(middle, reverted) },
            new RecordingEvidence { EntryId = "pending-context", RecordedUtc = "2026-11-05T10:03:00Z",
                Kind = "pending", Packet = Packet(reverted, pending) }
        };
        Packet range = RangeExport.Create(Guid.NewGuid().ToString("N"), recordings, 0, 3, pending);
        string rangeText = MarkdownExport.Render(range);
        Check(Regex.Matches(rangeText, "^### Recording ", RegexOptions.Multiline).Count == 4 &&
            Regex.Matches(rangeText, "<a id=\"evidence-").Count == range.Changes.Count &&
            rangeText.Contains(recordings[0].EntryId + "&#58;change\\-000001") &&
            rangeText.Contains(recordings[2].EntryId + "&#58;change\\-000001"),
            "range Markdown preserves ordered reversals, context-only recordings and qualified evidence");
        Check(rangeText.Contains("Historical context\\-only warning") && rangeText.Contains("Pending context warning") &&
            rangeText.Contains("Unrecorded editor state") && rangeText.Contains("not the end of the selected range"),
            "historical/current warnings and pending-state boundaries remain in the range document");
        NoteDraft draft = Draft(range);
        string reviewed = MarkdownExport.RenderReviewed(range, draft, pending);
        Check(reviewed.Contains("## Reviewed draft prose") && reviewed.Contains("separate from captured facts") &&
            reviewed.Contains("](#evidence-000001)") && !reviewed.Contains("# <script>") &&
            reviewed.Contains("> \\# &lt;script&gt;"), "reviewed prose is escaped, separate, cited and not labelled native approval");
        Check(MarkdownExport.Render(range) == rangeText && !rangeText.Contains("## Reviewed draft prose"),
            "factual export never automatically includes an editable draft");
        NoteDraft incomplete = Draft(range); incomplete.Entries[0].ChangeIds.RemoveAt(0);
        Reject<InvalidDataException>(() => MarkdownExport.RenderReviewed(range, incomplete, pending), "reviewed Markdown rejects missing evidence");
        NoteDraft duplicate = Draft(range); duplicate.Entries[0].ChangeIds.Add(duplicate.Entries[0].ChangeIds[0]);
        Reject<InvalidDataException>(() => MarkdownExport.RenderReviewed(range, duplicate, pending), "reviewed Markdown rejects duplicate evidence");
        NoteDraft wrong = Draft(range); wrong.BatchId = "wrong";
        Reject<InvalidDataException>(() => MarkdownExport.RenderReviewed(range, wrong, pending), "reviewed Markdown rejects a mismatched draft");
        Snapshot stale = Copy(pending); stale.Records[0].Name = "stale";
        Reject<InvalidOperationException>(() => MarkdownExport.RenderReviewed(range, draft, stale), "reviewed Markdown rejects stale current state");
        NoteDraft missing = Draft(range); missing.Entries[0].TargetRecordId = "missing";
        Reject<InvalidDataException>(() => MarkdownExport.RenderReviewed(range, missing, pending), "reviewed Markdown rejects a missing target");
        NoteDraft nul = Draft(range); nul.Entries[0].Text = "NUL\0";
        Reject<InvalidDataException>(() => MarkdownExport.RenderReviewed(range, nul, pending), "reviewed Markdown rejects NUL prose");
        Reject<InvalidDataException>(() => MarkdownExport.Render(null), "Markdown rejects a missing packet");
        Packet bad = Json.Read<Packet>(original); bad.Changes[0].After.Text = "tampered";
        Reject<InvalidDataException>(() => MarkdownExport.Render(bad), "Markdown rejects changed packet evidence");
        Packet legacy = Json.Read<Packet>(original); legacy.SchemaVersion = 1;
        Check(MarkdownExport.Render(legacy).Contains("## Captured facts"), "original schema-1 packets remain exportable");
        Check(MarkdownExport.Render(Packet(before, before)).Contains("not an all-clear"),
            "empty historical packets do not imply complete coverage");
        CheckStorage(text, reviewed);
        return passed;
    }

    private static void CheckStorage(string text, string other)
    {
        string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "markdown-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "changes.md");
            ExportStorage.WriteMarkdown(path, text, null);
            string hash = TrackingStorage.HashFile(path);
            Check(File.ReadAllText(path) == text && File.ReadAllBytes(path)[0] == (byte)'<' &&
                ExportStorage.ObserveMarkdownTarget(path, null) == hash, "Markdown writes deterministic BOM-free UTF-8 with a validated body hash");
            ExportStorage.WriteMarkdown(path, other, null);
            Check(File.ReadAllText(path) == other && !File.Exists(path + ".bak"), "an intact generated export is replaceable without rotating unrelated backups");
            string observed = ExportStorage.ObserveMarkdownTarget(path, null);
            ExportStorage.WriteMarkdown(path, text, null);
            Reject<IOException>(() => ExportStorage.WriteMarkdown(path, other, null, observed, null), "stale observed Markdown cannot overwrite a concurrent generated export");
            File.AppendAllText(path, "\nManual edit");
            string edited = TrackingStorage.HashFile(path);
            Reject<InvalidDataException>(() => ExportStorage.WriteMarkdown(path, text, null), "edited generated Markdown is not silently replaced");
            Check(TrackingStorage.HashFile(path) == edited, "rejected Markdown replacement preserves manual edits");
            foreach (string content in new[] { "Unrelated notes", "{ broken JSON", Json.Write(TrackingDocument.Create(State("1"), null)),
                "<!-- FCS-Scribe Markdown export v1: " })
            {
                File.WriteAllText(path, content);
                Reject<InvalidDataException>(() => ExportStorage.WriteMarkdown(path, text, null), "unrelated/damaged/renamed tracking data is not a Markdown overwrite target");
                Check(File.ReadAllText(path) == content, "unclassifiable Markdown destination bytes remain unchanged");
            }
            foreach (string name in new[] { "mod.mod", "notes.txt", "todo.txt", "settings.json", "copy.md.bak", "copy.md." })
                Reject<InvalidOperationException>(() => ExportStorage.RequireLocation(Path.Combine(directory, name + ".json.scribe", "new.md"), null),
                    "all reserved storage paths reject new Markdown files");
            foreach (string name in new[] { "mod.mod", "notes.txt", "todo.txt", "settings.json", "copy.md.bak" })
                Reject<InvalidDataException>(() => ExportStorage.WriteMarkdown(Path.Combine(directory, name), text, null), "Markdown requires its own .md extension");
            string anchor = Path.Combine(directory, "tracking.json");
            Directory.CreateDirectory(anchor + ".scribe");
            Reject<InvalidOperationException>(() => ExportStorage.WriteMarkdown(anchor, text, null), "inactive tracking anchors remain protected");
            Reject<InvalidOperationException>(() => ExportStorage.WriteMarkdown(Path.Combine(anchor + ".scribe", "new.md"), text, null),
                "Markdown cannot create a file in inactive reserved storage");
            string fresh = Path.Combine(directory, "fresh.md");
            Reject<InvalidDataException>(() => ExportStorage.WriteMarkdown(fresh, "invalid export", null), "invalid outgoing Markdown is not committed");
            Check(!File.Exists(fresh), "invalid outgoing Markdown leaves a new target absent");
            foreach (string stepToFail in new[] { "write", "flush", "commit" })
                foreach (bool exists in new[] { false, true })
                {
                    if (exists) File.WriteAllText(fresh, text, new UTF8Encoding(false));
                    string before = exists ? TrackingStorage.HashFile(fresh) : null;
                    Reject<IOException>(() => ExportStorage.WriteMarkdown(fresh, other, null, before, step => {
                        if (step == stepToFail) throw new IOException("Injected Markdown " + step + " failure.");
                    }), "Markdown precommit errors are explicit");
                    Check((exists ? TrackingStorage.HashFile(fresh) == before : !File.Exists(fresh)) &&
                        Directory.GetFiles(directory, "*.tmp").Length == 0, "Markdown failure preserves originals and cleans only its temporary file");
                    if (exists) File.Delete(fresh);
                }
            File.WriteAllText(fresh, text, new UTF8Encoding(false));
            string original = TrackingStorage.HashFile(fresh);
            Reject<InvalidDataException>(() => ExportStorage.WriteMarkdown(fresh, other, null, original, step => {
                if (step == "commit") File.WriteAllText(fresh, "{\"Format\":\"FCS-Scribe tracking\"}");
            }), "Markdown precommit reclassification rejects a destination converted into JSON");
            Check(File.ReadAllText(fresh) == "{\"Format\":\"FCS-Scribe tracking\"}" &&
                Directory.GetFiles(directory, "*.tmp").Length == 0, "external conversion survives failed Markdown replacement");
            File.WriteAllText(fresh, text, new UTF8Encoding(false));
            using (var locked = File.OpenRead(fresh))
                Reject<IOException>(() => ExportStorage.WriteMarkdown(fresh, other, null), "actual sharing denial is not reported as Markdown success");
            Check(File.ReadAllText(fresh) == text && Directory.GetFiles(directory, "*.tmp").Length == 0,
                "sharing failure retains complete original Markdown bytes");
        }
        finally { Directory.Delete(directory, true); }
    }
}
