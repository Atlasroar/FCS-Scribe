using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FcsScribe;

internal static class CoreTests
{
    private static int passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAILED: " + name);
        passed++;
    }

    private static void Reject(Action action, string name)
    {
        try { action(); }
        catch (InvalidOperationException) { passed++; return; }
        catch (InvalidDataException) { passed++; return; }
        throw new Exception("FAILED to reject: " + name);
    }

    private static Snapshot State()
    {
        return new Snapshot {
            ModId = "test-mod", ModName = "test.mod",
            LoadOrder = new List<string> { "base.mod", "test.mod" },
            Dependencies = new List<string> { "base.mod" },
            Records = new List<Record> {
                new Record {
                    Id = "stable-1", Type = "ITEM", Name = "Sword",
                    Cells = new List<Cell> {
                        Cell("/fields/damage", "active", "Int32", "10"),
                        Cell("/fields/damage", "effective", "Int32", "10"),
                        Cell("/fields/flag", "effective", "boolean", "false"),
                        Cell("/fields/text", "effective", "string", "")
                    }
                }
            }
        };
    }

    private static Cell Cell(string path, string layer, string kind, string text)
    {
        return new Cell { Path = path, Layer = layer, Value = Value.Of(kind, text), Source = "test.mod" };
    }

    private static Snapshot Clone(Snapshot state) { return Json.Read<Snapshot>(Json.Write(state)); }

    private static NoteDraft Draft(Packet packet)
    {
        return new NoteDraft {
            BatchId = packet.BatchId, SourceFingerprint = packet.SourceFingerprint,
            Entries = new List<NoteEntry> { new NoteEntry {
                TargetRecordId = "stable-1", Text = "Damage increased to 11.",
                ChangeIds = packet.Changes.Select(x => x.Id).ToList()
            }}
        };
    }

    public static int Main()
    {
        try
        {
            Snapshot baseline = State();
            var tracker = new Tracker(baseline);
            Check(tracker.Export(State()).Changes.Count == 0, "unchanged");
            Snapshot modified = State();
            modified.Records[0].Cells[0].Value.Text = "11";
            Packet packet = tracker.Export(modified);
            Check(packet.Changes.Count == 1 && packet.Changes[0].Before.Text == "10" &&
                packet.Changes[0].After.Text == "11" && packet.Changes[0].Layer == "active", "exact stored override values");
            Check(packet.Context.Records.Count == 0, "packet does not leak entire loaded database");
            Check(packet.Context.LoadOrder.SequenceEqual(baseline.LoadOrder), "load order retained");
            Check(tracker.Export(modified).BatchId == packet.BatchId, "identical export batch identity");
            baseline.Records[0].Cells[0].Value.Text = "999";
            Check(tracker.Export(modified).Changes[0].Before.Text == "10", "immutable baseline");
            modified.Records[0].Name = "Renamed";
            Check(tracker.Export(modified).Changes.Any(x => x.Path == "/name" && x.Operation == "renamed"), "rename");
            Snapshot added = State();
            added.Records.Add(new Record { Id = "stable-2", Type = "ITEM", Name = "New",
                Cells = new List<Cell> { Cell("/fields/zero", "active", "Int32", "0") } });
            Check(tracker.Export(added).Changes.Any(x => x.Path == "/record" && x.Operation == "added"), "record addition");
            Check(tracker.Export(added).Changes.Any(x => x.Before.Kind == "missing" && x.After.Text == "0"), "zero distinct from missing");
            Snapshot deleted = State();
            deleted.Records.Clear();
            Packet deletion = tracker.Export(deleted);
            Check(deletion.Changes.Any(x => x.Operation == "deleted"), "record deletion");
            Check(deletion.ResponseTemplate.Entries[0].TargetRecordId == "", "deleted record requires explicit surviving note target");
            Snapshot removed = State();
            removed.Records[0].Cells.RemoveAt(2);
            Check(tracker.Export(removed).Changes.Any(x => x.Before.Kind == "boolean" &&
                x.Before.Text == "false" && x.After.Kind == "missing"), "false distinct from missing");
            removed = State();
            removed.Records[0].Cells.RemoveAt(3);
            Check(tracker.Export(removed).Changes.Any(x => x.Before.Kind == "string" &&
                x.Before.Text == "" && x.After.Kind == "missing"), "empty distinct from missing");
            Check(!Value.Of("null", null).Same(Value.Missing()), "null distinct from missing");
            Check(!Value.Of("removed-reference", null).Same(Value.Of("null", null)), "removed reference distinct from null");
            Snapshot reference = State();
            var refCell = Cell("/references/items/stable-1", "active", "triple", "[1,2,3]");
            refCell.ReferencedId = "stable-1";
            reference.Records[0].Cells.Add(refCell);
            Packet refs = tracker.Export(reference);
            Check(refs.Changes.Single().ReferencedName == "Sword", "reference name resolved");
            Snapshot changedRef = Clone(reference);
            changedRef.Records[0].Cells.Last().Value.Text = "[0,2,3]";
            Packet changedRefs = new Tracker(reference).Export(changedRef);
            Check(changedRefs.Changes.Single().Before.Text == "[1,2,3]" &&
                changedRefs.Changes.Single().After.Text == "[0,2,3]", "reference quantity modification");
            Check(new Tracker(reference).Export(State()).Changes.Single().Operation == "removed", "reference removal");
            Snapshot reordered = Clone(reference);
            reordered.Records.Reverse();
            reordered.Records[0].Cells.Reverse();
            Check(reference.Fingerprint() == reordered.Fingerprint(), "ordering-independent fingerprint");
            Check(Json.Write(tracker.Export(reference).Changes) == Json.Write(tracker.Export(reordered).Changes), "deterministic diff ordering");
            Snapshot inherited = State();
            inherited.Records[0].Cells.Add(Cell("/fields/inherited", "base", "Int32", "8"));
            Check(new Tracker(inherited).Export(Clone(inherited)).Changes.Count == 0, "inherited data not a new edit");
            inherited.Records[0].Cells.Last().Value.Text = "9";
            Reject(delegate { tracker.Export(inherited); }, "changed dependency data");
            Snapshot context = State();
            context.LoadOrder.Reverse();
            Reject(delegate { tracker.Export(context); }, "changed load order");
            context = State();
            context.ModId = "other";
            Reject(delegate { tracker.Export(context); }, "changed active mod");
            context = State();
            context.Dependencies.Add("save-normalized.mod");
            Check(tracker.Export(context).Changes.Count == 0, "save-normalized dependency declarations do not reset baseline");
            Check(tracker.Export(context).BatchId == tracker.Export(State()).BatchId,
                "save-normalized metadata does not change documentation batch identity");
            Snapshot instance = State();
            instance.Records[0].Cells.Add(Cell("/instances/child/present", "effective", "boolean", "true"));
            instance.Records[0].Cells.Add(Cell("/instances/child/name", "base", "string", "Child"));
            Check(new Tracker(instance).Export(State()).Changes.Any(x => x.Path == "/instances/child/present" &&
                x.After.Kind == "missing"), "owned instance disappearance is not an inherited-data edit");
            Check(tracker.Export(instance).Changes.Any(x => x.Path == "/instances/child/present" &&
                x.After.Text == "true"), "instance creation includes presence without attributing base defaults as edits");
            Snapshot duplicate = State();
            duplicate.Records.Add(duplicate.Records[0]);
            Reject(delegate { new Tracker(duplicate); }, "duplicate stable IDs");
            duplicate = State();
            duplicate.Records[0].Cells.Add(duplicate.Records[0].Cells[0]);
            Reject(delegate { new Tracker(duplicate); }, "duplicate field paths");

            modified = State();
            modified.Records[0].Cells[0].Value.Text = "11";
            packet = tracker.Export(modified);
            NoteDraft draft = Draft(packet);
            DraftValidation.Check(packet, draft, modified, delegate { return "Existing notes"; });
            passed++;
            Reject(delegate { DraftValidation.Check(packet, draft, State(), delegate { return ""; }); }, "stale draft");
            Reject(delegate { DraftValidation.Check(packet, draft, modified, delegate {
                return "Existing notes " + DraftValidation.Marker(packet.BatchId);
            }); }, "duplicate batch");
            draft.BatchId = "wrong";
            Reject(delegate { DraftValidation.Check(packet, draft, modified, delegate { return ""; }); }, "wrong batch");
            draft = Draft(packet);
            draft.Entries[0].ChangeIds.Add("invented-change");
            Reject(delegate { DraftValidation.Check(packet, draft, modified, delegate { return ""; }); }, "unknown evidence");
            draft = Draft(packet);
            draft.Entries[0].Text = " ";
            Reject(delegate { DraftValidation.Check(packet, draft, modified, delegate { return ""; }); }, "empty draft");
            draft = Draft(packet);
            draft.Entries[0].TargetRecordId = "deleted";
            Reject(delegate { DraftValidation.Check(packet, draft, modified, delegate { return ""; }); }, "nonexistent target");
            draft = Draft(packet);
            draft.Entries.Add(draft.Entries[0]);
            Reject(delegate { DraftValidation.Check(packet, draft, modified, delegate { return ""; }); }, "duplicate note target");
            string original = "{\"Text\":\"quotes: \\\"hi\\\", slash: \\\\, newline: \\n, Unicode: \\u2603\"}";
            Check(Json.Read<NoteEntry>(Json.Pretty(original)).Text == Json.Read<NoteEntry>(original).Text, "JSON formatting preserves data");
            Packet roundtrip = Json.Read<Packet>(Json.Pretty(Json.Write(packet)));
            Check(roundtrip.Changes[0].Before.Text == "10" && roundtrip.SchemaVersion == 2, "versioned packet roundtrip");
            Check(packet.Instructions.Contains("UNTRUSTED DATA") && packet.Instructions.Contains("do not claim completeness"), "AI evidence safeguards");
            var oldPacket = Json.Read<Packet>("{\"SchemaVersion\":1,\"Context\":{\"ModId\":\"legacy\",\"ModName\":\"legacy.mod\"}}");
            Check(oldPacket.SchemaVersion == 1 && oldPacket.Context.Workshop == null, "schema 1 packets remain readable without Workshop context");
            var metadata = new WorkshopMetadata {
                ModName = "Workshop title", Author = "Author", Version = 12, Description = "Untrusted \"description\"",
                Dependencies = new List<string> { "base.mod" }, ReferencedMods = new List<string> { "reference.mod" },
                Tags = new List<string> { "Buildings" }, ChangeNotes = "Staged notes",
                SteamId = "18446744073709551614", Visibility = "Private", VisibilityCode = 2,
                LastUpdate = "2026-11-03T12:00:00.0000000Z",
                Preview = new PreviewImage { Status = "missing" }, Source = "test", Warnings = new List<string> { "cached" }
            };
            Snapshot withMetadata = State();
            withMetadata.Workshop = metadata;
            Packet metadataOnly = tracker.Export(withMetadata);
            Check(metadataOnly.Changes.Count == 0 && metadataOnly.BatchId == tracker.Export(State()).BatchId,
                "Workshop context is not invented gameplay evidence or a new documentation batch");
            WorkshopMetadata copied = Json.Read<WorkshopMetadata>(Json.Write(metadataOnly.Context.Workshop));
            Check(Json.Write(copied) == Json.Write(metadata) && copied.SteamId == "18446744073709551614",
                "all Workshop fields round-trip with lossless Steam ID strings");
            metadata.ModName = "Changed after export";
            metadata.Tags.Add("Other");
            Check(metadataOnly.Context.Workshop.ModName == "Workshop title" && metadataOnly.Context.Workshop.Tags.Count == 1,
                "exported Workshop context is independent of later mutations");
            Check(withMetadata.Fingerprint() != metadataOnly.SourceFingerprint, "Workshop changes affect stale-context fingerprints");
            withMetadata.Records[0].Cells[0].Value.Text = "11";
            Packet withContext = tracker.Export(withMetadata);
            NoteDraft contextDraft = Draft(withContext);
            withMetadata.Workshop.ChangeNotes = "New staged notes";
            Reject(delegate { DraftValidation.Check(withContext, contextDraft, withMetadata, delegate { return ""; }); },
                "Workshop context changed after export blocks stale drafts");
            Console.WriteLine("PASS: " + passed + " core checks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
