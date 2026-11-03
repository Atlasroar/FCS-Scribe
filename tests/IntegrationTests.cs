using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
