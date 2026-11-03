using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using FcsScribe;

internal static class IntegrationTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static int passed;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAILED: " + message);
        passed++;
    }

    private static void Set(object target, string property, object value)
    {
        target.GetType().GetProperty(property, Flags).SetValue(target, value, null);
    }

    private static void Put(object target, string field, object value)
    {
        target.GetType().GetField(field, Flags).SetValue(target, value);
    }

    private static void Reject<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (Exception error) { if (error is T) { passed++; return; } throw; }
        throw new Exception("FAILED to reject: " + message);
    }

    [STAThread]
    public static int Main()
    {
        Form editor = null;
        try
        {
            string directory = Directory.GetCurrentDirectory();
            Assembly assembly = Assembly.LoadFrom(Path.Combine(directory, "forgotten construction set.exe"));
            FcsAdapter.CheckVersion(assembly);
            Type program = assembly.GetType("forgotten_construction_set.Program", true);
            program.GetField("logger", Flags).SetValue(null, Activator.CreateInstance(
                assembly.GetType("forgotten_construction_set.Logger", true), new object[] { Path.Combine(directory, "fixture.log") }));
            program.GetProperty("args", Flags).SetValue(null, new string[0], null);
            editor = (Form)Activator.CreateInstance(assembly.GetType("forgotten_construction_set.baseForm", true));
            object nav = FcsAdapter.Field(editor, "nav");
            object data = FcsAdapter.Field(FcsAdapter.Field(nav, "ou"), "gameData");
            Type mode = assembly.GetType("forgotten_construction_set.GameData+ModMode", true);
            Type fileMode = assembly.GetType("forgotten_construction_set.navigation+ModFileMode", true);
            string output = Path.Combine(directory, "mods", "scribe-test.mod");
            FcsAdapter.Call(nav, "setActiveFilename", output, Enum.Parse(fileMode, "USER"));
            ((IList)FcsAdapter.Field(editor, "activeMods")).Add("fixture.mod");
            ((IList)FcsAdapter.Field(editor, "activeMods")).Add("scribe-test.mod");
            Check((bool)FcsAdapter.Call(data, "load", Path.Combine(directory, "fixture.mod"),
                Enum.Parse(mode, "BASE"), false, false), "load representative mod copy as dependency");
            File.Copy(Path.Combine(directory, "fixture.mod"), output);
            Check((bool)FcsAdapter.Call(data, "load", output, Enum.Parse(mode, "ACTIVE"), false, false),
                "load disposable active mod");
            FcsAdapter.Call(data, "resolveAllReferences");
            Set(FcsAdapter.Field(editor, "modInfo"), "Data", data);
            string originalModBytes = Convert.ToBase64String(File.ReadAllBytes(output));
            object todo = FcsAdapter.Field(editor, "todoList");
            FcsAdapter.Call(todo, "LoadAll");
            var adapter = new FcsAdapter(assembly, editor);
            CheckWorkshop(adapter, editor, data, nav, assembly);
            CheckScribeWindow(adapter, editor);
            Snapshot initial = adapter.Capture();
            Check(initial.Records.Count > 0, "capture real loaded records");
            var tracker = new Tracker(initial);
            Check(tracker.Export(adapter.Capture()).Changes.Count == 0, "unchanged loaded records");
            IDictionary records = (IDictionary)FcsAdapter.Field(data, "items");
            object item = records.Values.Cast<object>().First(x =>
                ((IDictionary)FcsAdapter.Field(x, "data")).Values.Cast<object>().Any(v => v is float || v is int));
            string id = (string)FcsAdapter.Property(item, "stringID");
            IDictionary originalFields = (IDictionary)FcsAdapter.Field(item, "data");
            string originalKey = originalFields.Keys.Cast<string>().First(x => originalFields[x] is float || originalFields[x] is int);
            object originalValue = originalFields[originalKey];
            object changed = originalValue is float ? (object)((float)originalValue + 1.0f) : (object)((int)originalValue + 1);
            item.GetType().GetProperty("Bah", Flags).SetValue(item, changed, new object[] { originalKey });
            Check(tracker.Export(adapter.Capture()).Changes.Any(x => x.RecordId == id &&
                x.Layer == "active"), "capture unsaved active override");
            Type noteType = assembly.GetType("forgotten_construction_set.ToDo+NotesItem+Type", true);
            FcsAdapter.Call(todo, "AddItem", item, "Inherited note.", Enum.Parse(noteType, "TODO"), null, false, false);
            string activeNotesFile = (string)FcsAdapter.Field(todo, "activeFile");
            todo.GetType().GetField("activeFile", Flags).SetValue(todo, Path.Combine(directory, "data", "__TODO__fixture-other.txt"));
            FcsAdapter.Call(todo, "SaveData");
            todo.GetType().GetField("activeFile", Flags).SetValue(todo, activeNotesFile);
            FcsAdapter.Call(todo, "LoadAll");
            FcsAdapter.Call(todo, "AddItem", item, "Existing note.", Enum.Parse(noteType, "TODO"), null, true, false);
            Set(nav, "HasChanges", false);
            Packet packet = tracker.Export(adapter.Capture());
            Check(Json.Write(packet.Changes) == Json.Write(tracker.Export(adapter.Capture()).Changes), "notes excluded from gameplay diff");
            var draft = new NoteDraft {
                BatchId = packet.BatchId, SourceFingerprint = packet.SourceFingerprint,
                Entries = new List<NoteEntry> { new NoteEntry {
                    TargetRecordId = id, Text = "Approved test documentation.",
                    ChangeIds = packet.Changes.Where(x => x.RecordId == id).Select(x => x.Id).ToList()
                }}
            };
            Snapshot beforeSave = adapter.Capture();
            string gameplay = beforeSave.Fingerprint();
            adapter.Apply(packet, draft);
            Check(adapter.Notes(id).Contains("Existing note.") && adapter.Notes(id).Contains("Approved test documentation."),
                "append preserves existing notes");
            Check(adapter.Notes(id).Contains("Inherited note."), "append preserves inherited notes");
            Check(FcsAdapter.Field(((IDictionary)FcsAdapter.Field(todo, "items"))[id], "type").ToString() == "TODO",
                "append preserves existing TODO category");
            Check((bool)FcsAdapter.Property(nav, "HasChanges"), "existing-note append marks editor dirty");
            Check(Convert.ToBase64String(File.ReadAllBytes(output)) == originalModBytes, "apply does not save mod");
            Check(adapter.Capture().Fingerprint() == gameplay, "notes do not change gameplay snapshot");
            bool duplicateRejected = false;
            try { adapter.Apply(packet, draft); }
            catch (InvalidOperationException) { duplicateRejected = true; }
            Check(duplicateRejected, "duplicate batch rejected");
            // Exercise the real normal-save handler, never a custom mod writer.
            Check((bool)FcsAdapter.Call(editor, "saveFile"), "normal FCS save handler");
            Check(File.Exists(output), "normal save persisted disposable mod");
            Check(!(bool)FcsAdapter.Property(nav, "HasChanges"), "normal save clears dirty state");
            FcsAdapter.Call(todo, "LoadAll");
            Check(adapter.Notes(id).Contains("Existing note.") && adapter.Notes(id).Contains(DraftValidation.Marker(packet.BatchId)),
                "notes survive FCS reload");
            Check(adapter.Notes(id).Contains("Inherited note."), "inherited notes survive normal save and reload");
            Snapshot afterSave = adapter.Capture();
            Check(Json.Write(afterSave.Records) == Json.Write(beforeSave.Records),
                "normal save does not alter captured gameplay records");
            Check(tracker.Export(afterSave).Changes.Count == packet.Changes.Count,
                "normal save preserves tracking baseline despite dependency normalization");
            // Reopen the generated active mod over a fresh dependency model.
            object reopened = Activator.CreateInstance(data.GetType());
            Check((bool)FcsAdapter.Call(reopened, "load", Path.Combine(directory, "fixture.mod"),
                Enum.Parse(mode, "BASE"), false, false), "reopen dependency");
            Check((bool)FcsAdapter.Call(reopened, "load", output, Enum.Parse(mode, "ACTIVE"), false, false), "reopen active mod");
            FcsAdapter.Call(reopened, "resolveAllReferences");
            object loaded = ((IDictionary)FcsAdapter.Field(reopened, "items"))[id];
            object value = loaded.GetType().GetProperty("Bah", Flags).GetValue(loaded, new object[] { originalKey });
            Check(value.Equals(changed), "unsaved override survives normal save and reopen");
            FcsAdapter.Field(nav, "ou").GetType().GetField("gameData", Flags).SetValue(FcsAdapter.Field(nav, "ou"), reopened);
            var reopenedAdapter = new FcsAdapter(assembly, editor);
            Check(Json.Write(reopenedAdapter.Capture().Records) == Json.Write(afterSave.Records),
                "all captured gameplay records survive full dependency/active-mod reopen");
            object renamed = ((IDictionary)FcsAdapter.Field(reopened, "items"))[id];
            var renameTracker = new Tracker(reopenedAdapter.Capture());
            Set(renamed, "Name", (string)FcsAdapter.Property(renamed, "Name") + " Scribe-test");
            Check(renameTracker.Export(reopenedAdapter.Capture()).Changes.Any(x => x.Operation == "renamed"), "live record rename");
            var addTracker = new Tracker(reopenedAdapter.Capture());
            object created = FcsAdapter.Call(reopened, "createItem", FcsAdapter.Property(renamed, "type"));
            string createdId = (string)FcsAdapter.Property(created, "stringID");
            Check(addTracker.Export(reopenedAdapter.Capture()).Changes.Any(x => x.RecordId == createdId &&
                x.Operation == "added" && x.Path == "/record"), "live record creation");
            var deleteTracker = new Tracker(reopenedAdapter.Capture());
            FcsAdapter.Call(created, "flagDeleted");
            Check(deleteTracker.Export(reopenedAdapter.Capture()).Changes.Any(x => x.RecordId == createdId &&
                x.Operation == "deleted"), "live record tombstone");
            CheckReferences(reopened, reopenedAdapter, assembly);
            CheckInstances(reopened, reopenedAdapter);
            Console.WriteLine("PASS: " + passed + " disposable FCS integration checks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { if (editor != null) editor.Dispose(); }
    }

    private static void CheckScribeWindow(FcsAdapter adapter, Form editor)
    {
        using (var window = new ScribeWindow(adapter, editor))
        {
            Check(window.IsMdiChild && window.MdiParent == editor, "Scribe uses FCS's actual MDI container");
            var actions = window.Controls.OfType<FlowLayoutPanel>().Single();
            var help = (Label)FcsAdapter.Field(window, "help");
            var status = (Label)FcsAdapter.Field(window, "status");
            string originalStatus = status.Text;
            Check(actions.Controls.OfType<Button>().Count() == 8, "all eight actions are available");
            foreach (Button button in actions.Controls.OfType<Button>())
            {
                typeof(Control).GetMethod("OnMouseEnter", Flags).Invoke(button, new object[] { EventArgs.Empty });
                Check(!String.IsNullOrEmpty(button.AccessibleDescription) && help.Text == button.AccessibleDescription &&
                    status.Text == originalStatus, button.Text + " hover explains action without erasing operation status");
                typeof(Control).GetMethod("OnMouseLeave", Flags).Invoke(button, new object[] { EventArgs.Empty });
                Check(help.Text != button.AccessibleDescription, button.Text + " hover help clears on exit");
                typeof(Control).GetMethod("OnEnter", Flags).Invoke(button, new object[] { EventArgs.Empty });
                Check(help.Text == button.AccessibleDescription, button.Text + " keyboard focus explains action");
                typeof(Control).GetMethod("OnLeave", Flags).Invoke(button, new object[] { EventArgs.Empty });
            }
            Check(window.Controls.OfType<TabControl>().Single().TabPages.Cast<TabPage>().Any(x => x.Text == "Workshop info"),
                "Workshop context has a read-only review tab");
            window.PerformLayout();
            Check(help.Dock == DockStyle.Bottom && help.Bottom <= status.Top, "hover help is at the window bottom above persistent status");
        }
    }

    private static void CheckWorkshop(FcsAdapter adapter, Form editor, object data, object nav, Assembly assembly)
    {
        object header = FcsAdapter.Field(data, "header");
        object dialog = FcsAdapter.Field(editor, "modInfo");
        object originalInfo = FcsAdapter.Field(dialog, "info");
        string author = (string)FcsAdapter.Field(header, "Author");
        string description = (string)FcsAdapter.Field(header, "Description");
        int version = (int)FcsAdapter.Field(header, "Version");
        string dependencies = Json.Write((List<string>)FcsAdapter.Field(header, "Dependencies"));
        string references = Json.Write((List<string>)FcsAdapter.Field(header, "Referenced"));
        bool dirty = (bool)FcsAdapter.Property(nav, "HasChanges");
        object info = Activator.CreateInstance(assembly.GetType("forgotten_construction_set.ModData", true),
            new[] { header, FcsAdapter.Property(nav, "ModPath"), "scribe-test" });
        string cache = (string)FcsAdapter.Call(info, "GetFileName");
        string preview = (string)FcsAdapter.Call(info, "GetAbsoluteImagePath");
        string archive = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", Guid.NewGuid().ToString("N") + ".png"));
        try
        {
            WorkshopMetadata missing = adapter.CaptureWorkshop();
            Check(missing.SteamId == null && missing.ModName == null && missing.VisibilityCode == null &&
                missing.ChangeNotes == null && missing.Warnings.Count > 0, "missing Workshop cache is explicitly unknown, not fabricated defaults");
            Check(missing.Preview.Status == "missing" && missing.Preview.Sha256 == null, "missing preview is explicit");
            Reject<InvalidOperationException>(delegate { adapter.ArchivePreview(archive, null); }, "missing preview cannot be archived");
            Put(header, "Author", "Fixture author");
            Put(header, "Description", "Fixture description \"quoted\"\r\nsecond line");
            Put(header, "Version", 321);
            Put(info, "title", "Cached Workshop title");
            Put(info, "id", UInt64.MaxValue - 1);
            Put(info, "tags", new List<string> { "Buildings" });
            Put(info, "visibility", 2);
            var updated = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);
            Put(info, "lastUpdate", (DateTime?)updated);
            FcsAdapter.Call(info, "Save");
            byte[] cacheBytes = File.ReadAllBytes(cache);
            WorkshopMetadata cached = adapter.CaptureWorkshop();
            Check(cached.ModName == "Cached Workshop title" && cached.SteamId == (UInt64.MaxValue - 1).ToString() &&
                cached.VisibilityCode == 2 && cached.Visibility == ((ComboBox)FcsAdapter.Field(dialog, "cbVisiblity")).Items[2].ToString(),
                "saved Workshop cache supplies title, lossless Steam ID and actual visibility label");
            Check(cached.Tags.SequenceEqual(new[] { "Buildings" }) && cached.LastUpdate == updated.ToString("o") &&
                cached.ChangeNotes == null, "cache supplies tags and timestamp but does not invent staged change notes");
            Check(cached.Author == "Fixture author" && cached.Version == 321 &&
                cached.Description == "Fixture description \"quoted\"\r\nsecond line", "Workshop captures active header author, version and description");
            Check(cached.Dependencies.SequenceEqual(((IEnumerable)FcsAdapter.Call(data, "getRequiredMods")).Cast<string>()) &&
                cached.ReferencedMods.SequenceEqual(((IEnumerable)FcsAdapter.Call(data, "getReferencedMods")).Cast<string>()),
                "Workshop dependency lists match FCS's required and parenthesized referenced mods");
            Check(File.ReadAllBytes(cache).SequenceEqual(cacheBytes) && (bool)FcsAdapter.Property(nav, "HasChanges") == dirty &&
                Json.Write((List<string>)FcsAdapter.Field(header, "Dependencies")) == dependencies &&
                Json.Write((List<string>)FcsAdapter.Field(header, "Referenced")) == references, "capture does not save cache, dirty the editor or refresh header dependency declarations");
            Put(dialog, "info", info);
            ((TextBox)FcsAdapter.Field(dialog, "txtTitle")).Text = "Unsaved Workshop title";
            ((TextBox)FcsAdapter.Field(dialog, "txtAuthor")).Text = "Unsaved author";
            ((TextBox)FcsAdapter.Field(dialog, "txtDescription")).Text = "Unsaved description";
            ((TextBox)FcsAdapter.Field(dialog, "txtVersion")).Text = "322";
            ((ComboBox)FcsAdapter.Field(dialog, "cbVisiblity")).SelectedIndex = 1;
            ((TextBox)FcsAdapter.Field(dialog, "txtChangeNotes")).Text = "Staged upload notes, not Notes/TODO.";
            ((List<string>)FcsAdapter.Field(info, "tags")).Add("Unsaved tag");
            WorkshopMetadata live = adapter.CaptureWorkshop();
            Check(live.ModName == "Unsaved Workshop title" && live.Author == "Unsaved author" &&
                live.Version == 322 && live.Description == "Unsaved description" && live.VisibilityCode == 1 &&
                live.Tags.SequenceEqual(new[] { "Buildings", "Unsaved tag" }),
                "current Workshop dialog and header override the older cache without saving");
            Check(live.ChangeNotes == "Staged upload notes, not Notes/TODO." &&
                (string)FcsAdapter.Field(info, "changeNotes") == "", "staged change notes come from the control, not stale ModData.changeNotes");
            WorkshopExport export = Json.Read<WorkshopExport>(Json.Write(adapter.CaptureWorkshopExport()));
            Check(export.SchemaVersion == 1 && export.ModFileName == "scribe-test.mod" && export.Workshop.ModName == live.ModName &&
                export.ExportedUtc.EndsWith("Z", StringComparison.Ordinal), "standalone Workshop JSON requires no tracking baseline or record changes");
            Put(dialog, "uploading", true);
            Reject<InvalidOperationException>(delegate { adapter.CaptureWorkshop(); }, "capture during upload is blocked");
            Put(dialog, "uploading", false);
            object steam = assembly.GetType("forgotten_construction_set.SteamManager", true).GetField("instance", Flags).GetValue(null);
            bool connected = (bool)FcsAdapter.Property(steam, "Enabled");
            var refresh = (Button)FcsAdapter.Field(dialog, "btnRefreshFromSteam");
            var upload = (Button)FcsAdapter.Field(dialog, "btnUpload");
            bool refreshEnabled = refresh.Enabled, uploadEnabled = upload.Enabled;
            try
            {
                Set(steam, "Enabled", true);
                refresh.Enabled = upload.Enabled = false;
                Reject<InvalidOperationException>(delegate { adapter.CaptureWorkshop(); }, "capture during asynchronous Steam refresh is blocked");
                Set(steam, "Enabled", false);
                Check(adapter.CaptureWorkshop().ModName == live.ModName, "disabled offline Workshop controls do not imply an in-progress refresh");
            }
            finally
            {
                Set(steam, "Enabled", connected);
                refresh.Enabled = refreshEnabled;
                upload.Enabled = uploadEnabled;
            }
            Put(info, "header", Activator.CreateInstance(header.GetType()));
            WorkshopMetadata staleDialog = adapter.CaptureWorkshop();
            Check(staleDialog.ModName == "Cached Workshop title" && staleDialog.ChangeNotes == null,
                "a previous mod's dialog is never reused as current metadata");
            Put(info, "header", header);
            using (var image = new Bitmap(2, 2)) image.Save(preview, ImageFormat.Png);
            byte[] source = File.ReadAllBytes(preview);
            PreviewImage imageInfo = adapter.CaptureWorkshop().Preview;
            Check(imageInfo.Status == "available" && imageInfo.Format == "png" && imageInfo.ByteLength == source.Length &&
                imageInfo.Sha256.Length == 64 && imageInfo.SourceFileName.EndsWith(".img", StringComparison.Ordinal),
                "preview is identified from encoded bytes despite FCS's .img extension");
            adapter.ArchivePreview(archive, imageInfo.Sha256);
            Check(File.ReadAllBytes(archive).SequenceEqual(source) && File.ReadAllBytes(preview).SequenceEqual(source),
                "archiving is byte-for-byte lossless and preserves the FCS original");
            Reject<IOException>(delegate { adapter.ArchivePreview(archive, imageInfo.Sha256); }, "existing archives are not overwritten");
            Reject<InvalidOperationException>(delegate {
                adapter.ArchivePreview(Path.Combine(Directory.GetCurrentDirectory(), "preview.png"), imageInfo.Sha256);
            }, "archiving inside the FCS installation is blocked");
            Reject<InvalidOperationException>(delegate {
                adapter.ArchivePreview(Path.ChangeExtension(archive, ".mod"), imageInfo.Sha256);
            }, "a mod cannot be an image archive target");
            using (var image = new Bitmap(3, 3)) image.Save(preview, ImageFormat.Png);
            Reject<InvalidOperationException>(delegate { adapter.ArchivePreview(archive, imageInfo.Sha256); },
                "preview changes during destination selection are rejected");
            foreach (ImageFormat format in new[] { ImageFormat.Jpeg, ImageFormat.Gif })
            {
                using (var image = new Bitmap(2, 2)) image.Save(preview, format);
                PreviewImage other = adapter.CaptureWorkshop().Preview;
                string destination = Path.ChangeExtension(archive, "." + other.Format);
                try
                {
                    adapter.ArchivePreview(destination, other.Sha256);
                    Check(File.ReadAllBytes(destination).SequenceEqual(File.ReadAllBytes(preview)),
                        other.Format + " previews archive without re-encoding");
                }
                finally { if (File.Exists(destination)) File.Delete(destination); }
            }
            using (var image = new Bitmap(2, 2)) image.Save(preview, ImageFormat.Bmp);
            Reject<NotSupportedException>(delegate { adapter.CaptureWorkshop(); }, "unsupported preview encoding is explicit");
            var boundaryImage = new byte[1024 * 1024];
            Array.Copy(source, boundaryImage, source.Length);
            File.WriteAllBytes(preview, boundaryImage);
            Check(adapter.CaptureWorkshop().Preview.ByteLength == 1024 * 1024, "FCS's exact 1 MB preview limit is accepted");
            using (var oversized = new FileStream(preview, FileMode.Create)) oversized.SetLength(1024 * 1024 + 1);
            Reject<NotSupportedException>(delegate { adapter.CaptureWorkshop(); }, "preview one byte over FCS's limit is rejected");
            File.Delete(preview);
            Put(dialog, "info", null);
            File.WriteAllText(cache, "<ModData><visibility>2</visibility></ModData>");
            WorkshopMetadata partial = adapter.CaptureWorkshop();
            Check(partial.ModName == null && partial.SteamId == null && partial.LastUpdate == null &&
                partial.Tags == null && partial.VisibilityCode == 2, "partial cache preserves missing fields as unknown");
            File.WriteAllText(cache, "<ModData><id>0</id><visibility>99</visibility><tags /></ModData>");
            WorkshopMetadata unknownVisibility = adapter.CaptureWorkshop();
            Check(unknownVisibility.SteamId == "0" && unknownVisibility.VisibilityCode == 99 &&
                unknownVisibility.Visibility == null && unknownVisibility.Tags.Count == 0 &&
                unknownVisibility.Warnings.Any(x => x.Contains("Unrecognized Workshop visibility")) &&
                unknownVisibility.Warnings.Any(x => x.Contains("unpublished/default")),
                "unknown visibility and explicit unpublished ID remain distinct from missing data");
            File.WriteAllText(cache, "<ModData>");
            Reject<InvalidOperationException>(delegate { adapter.CaptureWorkshop(); }, "malformed cache errors are visible");
            File.WriteAllText(cache, "<!DOCTYPE ModData [<!ENTITY x SYSTEM 'file:///not-read'>]><ModData><title>&x;</title></ModData>");
            Reject<InvalidOperationException>(delegate { adapter.CaptureWorkshop(); }, "cache DTDs and external entities are rejected");
        }
        finally
        {
            Put(dialog, "info", originalInfo);
            Put(dialog, "uploading", false);
            ((TextBox)FcsAdapter.Field(dialog, "txtChangeNotes")).Clear();
            Put(header, "Author", author);
            Put(header, "Description", description);
            Put(header, "Version", version);
            Set(nav, "HasChanges", dirty);
            if (File.Exists(cache)) File.Delete(cache);
            if (File.Exists(preview)) File.Delete(preview);
            if (File.Exists(archive)) File.Delete(archive);
        }
    }

    private static void CheckReferences(object data, FcsAdapter adapter, Assembly assembly)
    {
        foreach (object record in ((IDictionary)FcsAdapter.Field(data, "items")).Values)
        foreach (DictionaryEntry section in (IDictionary)FcsAdapter.Field(record, "references"))
        foreach (object reference in (IEnumerable)section.Value)
        {
            object original = FcsAdapter.Field(reference, "original");
            if (original == null) continue;
            string target = (string)FcsAdapter.Property(reference, "itemID");
            var tracker = new Tracker(adapter.Capture());
            Type triple = assembly.GetType("forgotten_construction_set.GameData+TripleInt", true);
            object values = Activator.CreateInstance(triple, new object[] {
                (int)FcsAdapter.Field(original, "v0") + 1,
                (int)FcsAdapter.Field(original, "v1"),
                (int)FcsAdapter.Field(original, "v2") });
            FcsAdapter.Call(record, "setReferenceValue", section.Key, target, values);
            Check(tracker.Export(adapter.Capture()).Changes.Any(x => x.ReferencedId == target &&
                x.Layer == "active"), "live reference quantity changes");
            tracker = new Tracker(adapter.Capture());
            FcsAdapter.Call(record, "removeReference", section.Key, target);
            Check(tracker.Export(adapter.Capture()).Changes.Any(x => x.ReferencedId == target &&
                x.After.Kind == "removed-reference"), "live reference removal tombstone");
            return;
        }
        Console.WriteLine("SKIP: fixture has no inherited references to exercise");
    }

    private static void CheckInstances(object data, FcsAdapter adapter)
    {
        foreach (object record in ((IDictionary)FcsAdapter.Field(data, "items")).Values)
        {
            IDictionary instances = (IDictionary)FcsAdapter.Field(record, "instances");
            if (instances.Count == 0) continue;
            string id = instances.Keys.Cast<string>().First();
            var tracker = new Tracker(adapter.Capture());
            FcsAdapter.Call(instances[id], "flagDeleted");
            Check(tracker.Export(adapter.Capture()).Changes.Any(x => x.Path.Contains("/instances/")),
                "live instance removal");
            return;
        }
        Console.WriteLine("SKIP: fixture has no instances to exercise");
    }
}
