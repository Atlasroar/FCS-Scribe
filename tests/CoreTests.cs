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

    private static void CheckCompleteCoverage()
    {
        Snapshot before = State();
        before.Records.Add(new Record { Id = "stable-2", Type = "ITEM", Name = "Shield" });
        Snapshot after = Clone(before);
        after.Records[0].Cells[0].Value.Text = "11";
        after.Records[0].Cells[1].Value.Text = "11";
        after.Records[1].Name = "Renamed shield";
        Packet packet = new Tracker(before).Export(after);
        NoteDraft complete = Json.Read<NoteDraft>(Json.Write(packet.ResponseTemplate));
        foreach (NoteEntry entry in complete.Entries) entry.Text = "Reviewed documentation for " + entry.TargetRecordId + ".";
        Func<NoteDraft> copy = () => Json.Read<NoteDraft>(Json.Write(complete));
        Check(DraftValidation.Pending(packet, complete, after, delegate { return ""; }).Count == 2,
            "a complete multi-record draft covers stored/effective changes exactly once");
        NoteDraft invalid = copy();
        invalid.Entries[0].ChangeIds.RemoveAt(0);
        Reject(delegate { DraftValidation.Check(packet, invalid, after, delegate {
            throw new Exception("Incomplete coverage reached notes lookup.");
        }); }, "omitted field evidence rejected before notes lookup");
        invalid = copy(); invalid.Entries.RemoveAt(1);
        Reject(delegate { DraftValidation.Check(packet, invalid, after, delegate { return ""; }); },
            "omitted changed record rejected");
        invalid = copy(); invalid.Entries[0].ChangeIds.Add(invalid.Entries[0].ChangeIds[0]);
        Reject(delegate { DraftValidation.Check(packet, invalid, after, delegate { return ""; }); },
            "duplicate citation inside a note rejected");
        invalid = copy(); invalid.Entries[1].ChangeIds.Add(invalid.Entries[0].ChangeIds[0]);
        Reject(delegate { DraftValidation.Check(packet, invalid, after, delegate { return ""; }); },
            "duplicate citation across distinct targets rejected");
        invalid = copy(); invalid.Entries[0].ChangeIds.Add(null);
        Reject(delegate { DraftValidation.Check(packet, invalid, after, delegate { return ""; }); },
            "null evidence rejected");
        NoteDraft reordered = copy(); reordered.Entries[0].ChangeIds.Reverse();
        Check(DraftValidation.Pending(packet, reordered, after, delegate { return ""; }).Count == 2,
            "initial complete coverage does not require citation ordering");

        NoteEntry first = complete.Entries[0], second = complete.Entries[1];
        string applied = DraftValidation.NoteText(packet.BatchId, first);
        Check(applied == "\r\n\r\n" + DraftValidation.Marker(packet.BatchId) + "\r\n" + first.Text +
            "\r\nChanges: " + String.Join(", ", first.ChangeIds) + "\r\n",
            "canonical note text preserves the existing append format");
        Func<string, string> partiallyApplied = id => id == first.TargetRecordId ? "Previous notes." + applied : "";
        List<NoteEntry> pending = DraftValidation.Pending(packet, complete, after, partiallyApplied);
        Check(pending.Count == 1 && pending[0] == second && complete.Entries.Count == 2,
            "retry skips a matching completed note without removing full draft coverage");
        Check(DraftValidation.Pending(packet, complete, after, id =>
            partiallyApplied(id).Replace("\r\n", "\n")).Count == 1,
            "completed notes tolerate normalized line endings");
        invalid = copy(); invalid.Entries[0].Text += " Changed after partial application.";
        Reject(delegate { DraftValidation.Check(packet, invalid, after, partiallyApplied); },
            "changed completed prose blocks unsafe retry");
        invalid = copy(); invalid.Entries[0].ChangeIds.Reverse();
        Reject(delegate { DraftValidation.Check(packet, invalid, after, partiallyApplied); },
            "changed completed citation block requires inspection before retry");
        Reject(delegate { DraftValidation.Check(packet, complete, after, id =>
            id == first.TargetRecordId ? applied + applied : ""); },
            "multiple same-batch markers block ambiguous retry");
        Reject(delegate { DraftValidation.Check(packet, complete, after, id =>
            id == first.TargetRecordId ? DraftValidation.Marker(packet.BatchId) : ""); },
            "marker without a complete matching note cannot be skipped");
        Reject(delegate { DraftValidation.Check(packet, complete, after, id =>
            DraftValidation.NoteText(packet.BatchId, complete.Entries.Single(x => x.TargetRecordId == id))); },
            "fully applied drafts still reject repeat application");
        invalid = copy(); invalid.Entries.RemoveAt(0);
        Reject(delegate { DraftValidation.Check(packet, invalid, after, partiallyApplied); },
            "retry must retain already-applied entries for full evidence coverage");
        Snapshot stale = Clone(after); stale.Records[0].Name = "Changed after export";
        Reject(delegate { DraftValidation.Check(packet, complete, stale, partiallyApplied); },
            "partial retry still rejects stale editor state");

        Snapshot deleted = Clone(after); deleted.Records.RemoveAt(1);
        Packet deletion = new Tracker(before).Export(deleted);
        var combined = new NoteDraft { BatchId = deletion.BatchId, SourceFingerprint = deletion.SourceFingerprint,
            Entries = new List<NoteEntry> { new NoteEntry {
                TargetRecordId = "stable-1", Text = "Reviewed edits and deleted shield.",
                ChangeIds = deletion.Changes.Select(x => x.Id).ToList()
            }} };
        Check(DraftValidation.Pending(deletion, combined, deleted, delegate { return ""; }).Count == 1,
            "deleted-record evidence can be combined on a surviving target without dropping changes");
        Check(packet.Instructions.Contains("EVERY exported change exactly once"),
            "new AI instructions require full nonduplicate evidence coverage");
    }

    private static void CheckWorkshopAvailability()
    {
        Snapshot baseline = State();
        baseline.Workshop = new WorkshopMetadata { Author = "Known author", Version = 3,
            Source = "Active mod header + saved Workshop .info cache", Preview = new PreviewImage { Status = "missing" } };
        Check(!Json.Write(baseline.Workshop).Contains("CaptureStatus") && !Json.Write(baseline.Workshop).Contains("Issues") &&
            !Json.Write(baseline.Workshop.Preview).Contains("UnavailableReason"),
            "optional availability fields do not change healthy legacy metadata serialization");
        Snapshot healthy = Clone(baseline); healthy.Records[0].Cells[0].Value.Text = "11";
        var tracker = new Tracker(baseline);
        Packet available = tracker.Export(healthy);
        Snapshot partial = Clone(healthy);
        partial.Workshop.CaptureStatus = "partial";
        partial.Workshop.Source = "Active mod header; Workshop cache unavailable";
        partial.Workshop.Issues = new List<WorkshopIssue> { new WorkshopIssue {
            Component = "cache", Code = "invalid-data", Message = "Workshop cache XML is invalid." } };
        partial.Workshop.Preview.Status = "unavailable";
        partial.Workshop.Preview.UnavailableReason = "Preview cannot be decoded.";
        Packet unavailable = tracker.Export(partial);
        Check(unavailable.BatchId == available.BatchId && unavailable.Changes.Count == available.Changes.Count,
            "optional Workshop availability changes do not invent gameplay evidence or batch identity");
        Reject(() => DraftValidation.Check(available, Draft(available), partial, id => ""),
            "available-to-unavailable metadata stales existing drafts");
        DraftValidation.Check(unavailable, Draft(unavailable), partial, id => "");
        passed++;
        Reject(() => DraftValidation.Check(unavailable, Draft(unavailable), healthy, id => ""),
            "recovering Workshop availability stales a partial-capture draft");
        Snapshot copy = Clone(partial);
        Check(copy.Fingerprint() == partial.Fingerprint() && copy.Workshop.Issues[0].Code == "invalid-data" &&
            copy.Workshop.Preview.UnavailableReason == "Preview cannot be decoded.",
            "availability and explicit reasons round-trip with stable fingerprints");
        copy.Workshop.Author = "Changed known author";
        Reject(() => DraftValidation.Check(unavailable, Draft(unavailable), copy, id => ""),
            "known header fields still stale drafts while optional data is unavailable");
        copy = Clone(partial); copy.Workshop.Issues[0].Code = "read-failed";
        Reject(() => DraftValidation.Check(unavailable, Draft(unavailable), copy, id => ""),
            "availability reason changes affect stale-state fingerprints");
        copy = Clone(partial); copy.Workshop.Preview.UnavailableReason = "Preview is too large.";
        Reject(() => DraftValidation.Check(unavailable, Draft(unavailable), copy, id => ""),
            "preview availability reasons participate in fingerprints");
        copy = Clone(partial); copy.Records[0].Cells[0].Value.Text = "12";
        Reject(() => DraftValidation.Check(unavailable, Draft(unavailable), copy, id => ""),
            "gameplay changes still stale partial-capture drafts");
        var recordings = new List<RecordingEvidence> { new RecordingEvidence {
            EntryId = Guid.NewGuid().ToString("N"), SessionId = Guid.NewGuid().ToString("N"),
            RecordedUtc = unavailable.ExportedUtc, Kind = "recorded", Packet = unavailable } };
        Packet range = RangeExport.Create(Guid.NewGuid().ToString("N"), recordings, 0, 0, partial);
        Check(range.Context.Workshop.CaptureStatus == "partial" && range.Recordings[0].Packet.Context.Workshop.Issues.Count == 1,
            "range exports retain current and historical availability reasons");
        Reject(() => DraftValidation.Check(range, Draft(range), healthy, id => ""),
            "historical range drafts still stale when current Workshop availability recovers");
        Check(unavailable.Instructions.Contains("unavailable metadata/preview values are unknown"),
            "AI instructions forbid interpreting unavailable values as verified defaults");
    }

    private static void CheckDraftWorkspace()
    {
        Snapshot before = State(), current = State();
        current.Records[0].Name = "Workspace edit";
        Packet packet = new Tracker(before).Export(current);
        string original = Json.Write(packet);
        string text = " \r\n" + Json.Pretty(Json.Write(Draft(packet))) + "\r\n ";
        DraftWorkspace workspace = DraftWorkspace.Create(packet, text);
        DraftWorkspace restored = Json.Read<DraftWorkspace>(Json.Write(workspace));
        restored.Validate();
        Check(restored.DraftText == text && Json.Write(restored.Packet) == original,
            "workspace round-trip retains exact text and original packet");
        Check(restored.Review(current, id => "").Contains("pass evidence/current-state checks"),
            "restored workspace validates its complete draft against original evidence");
        Check(restored.Review(before, id => "").Contains("stale"),
            "restored draft cannot bypass current-state validation");
        restored.DraftText = "{ unfinished";
        Check(restored.Review(current, id => "").Contains("invalid JSON"),
            "unfinished JSON is preserved but cannot be applied");
        restored.Validate();
        restored.DraftText = "";
        restored.Validate();
        Check(restored.DraftText == "", "empty draft text remains recoverable");
        restored.DraftText = Json.Write(new NoteDraft { BatchId = "other", SourceFingerprint = packet.SourceFingerprint });
        Check(restored.Review(current, id => "").Contains("does not match"),
            "mismatched restored response is reported without rebinding evidence");
        restored.DraftText = text;
        restored.Packet.Instructions += " changed";
        Reject(restored.Validate, "changed original packet rejected by workspace integrity hash");
        restored = Json.Read<DraftWorkspace>(Json.Write(workspace));
        restored.Packet = null;
        Reject(restored.Validate, "missing associated packet rejected");
        restored = Json.Read<DraftWorkspace>(Json.Write(workspace));
        restored.PacketHash = null;
        Reject(restored.Validate, "missing original-packet hash rejected");
        restored = Json.Read<DraftWorkspace>(Json.Write(workspace));
        restored.SchemaVersion++;
        Reject(restored.Validate, "unknown workspace version rejected");
        Reject(() => DraftWorkspace.Create(packet, null), "missing raw draft text rejected");
        restored = DraftWorkspace.Create(null, "unassociated unfinished text");
        restored.Validate();
        Check(restored.Review(current, id => "").Contains("no associated AI packet"),
            "text-only recovery never invents evidence");
        Packet range = RangeExport.Create(Guid.NewGuid().ToString("N"),
            new[] { new RecordingEvidence { EntryId = "checkpoint-test", Kind = "checkpoint",
                RecordedUtc = packet.ExportedUtc, Packet = packet } }, 0, 0, current);
        DraftWorkspace ranged = DraftWorkspace.Create(range, Json.Write(range.ResponseTemplate));
        DraftWorkspace rangedCopy = Json.Read<DraftWorkspace>(Json.Write(ranged));
        rangedCopy.Validate();
        Check(Json.Write(rangedCopy.Packet) == Json.Write(range) &&
            Json.Write(rangedCopy.Packet.Recordings[0].Packet) == original,
            "schema-3 workspace preserves qualified and original archival evidence");
        rangedCopy.Packet.Recordings[0].Packet.Changes[0].Id += "-tampered";
        rangedCopy.PacketHash = Json.StreamHash(rangedCopy.Packet);
        Reject(rangedCopy.Validate, "rehashed range with inconsistent original evidence rejected");
        Packet legacy = Json.Read<Packet>(original);
        legacy.SchemaVersion = 1;
        DraftWorkspace.Create(legacy, text).Validate();
        Check(Json.Write(legacy).Contains("\"SchemaVersion\":1"), "schema-1 packets remain supported in workspaces");
        string directory = Path.Combine(Path.GetTempPath(), "scribe-workspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "workspace.json");
        try
        {
            string hash = TrackingStorage.WriteAtomic(path, workspace, null);
            restored = DraftWorkspace.Load(path);
            Check(restored.FileHash == hash && restored.DraftText == text && Json.Write(restored.Packet) == original,
                "saved workspace reopens independently of the original packet file");
            restored.DraftText = "later unfinished work";
            string nextHash = TrackingStorage.WriteAtomic(path, restored, restored.FileHash);
            Check(nextHash != hash && Json.Read<DraftWorkspace>(File.ReadAllText(path + ".bak")).DraftText == text,
                "workspace replacement retains the previous file as a backup");
            File.AppendAllText(path, " ");
            bool conflict = false;
            try { TrackingStorage.WriteAtomic(path, workspace, nextHash); }
            catch (IOException) { conflict = true; }
            Check(conflict && File.ReadAllText(path).EndsWith(" "),
                "observed external workspace edits are not overwritten");
            string packetPath = Path.Combine(directory, "packet.json");
            File.WriteAllText(packetPath, original);
            Reject(() => DraftWorkspace.Load(packetPath), "ordinary packet cannot masquerade as a workspace");
            File.WriteAllText(packetPath, "null");
            Reject(() => DraftWorkspace.Load(packetPath), "null workspace rejected");
        }
        finally { Directory.Delete(directory, true); }
    }

    public static int Main()
    {
        try
        {
            CheckDraftWorkspace();
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
            CheckCompleteCoverage();
            CheckWorkshopAvailability();
            passed += TrackingTests.Run();
            passed += JournalTests.Run();
            Console.WriteLine("PASS: " + passed + " core checks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
