using System;
using System.IO;
using System.Linq;
using System.Text;
using FcsScribe;

internal static class ModWorkTests
{
    private static int passed;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception("FAILED: " + message); passed++; }
    private static void Reject<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T) { passed++; return; }
        throw new Exception("FAILED to reject: " + message);
    }

    internal static int Run()
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), "build", "mod-work-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var off = new ModWork(root, "example.mod", false, false);
        off.Notes = "disabled"; off.Save();
        Check(!File.Exists(off.NotesPath) && !File.Exists(off.TodoPath), "disabled features create no text files");
        var work = new ModWork(root, "example.mod", true, true);
        Check(!File.Exists(work.NotesPath), "opening enabled work does not prematurely create files");
        work.Notes = "Plain notes.\r\nNo formatting.\nTabs\tand Unicode \u03a9.";
        work.Todos.Add(new ModTodo { RecordId = "id/1", RecordName = "Name\twith\nlines", Text = "Task\\text\r\nnext", Done = true });
        work.Save();
        Check(File.ReadAllText(work.NotesPath) == work.Notes, "notes are literal plain text, not JSON");
        Check(File.ReadAllText(work.TodoPath).Contains("[x]\t") && File.ReadAllText(work.TodoPath).Contains("Task"),
            "todo.txt is separately readable with completion and record linkage");
        var reopened = new ModWork(root, "example.mod", true, true);
        Check(reopened.Notes == work.Notes && reopened.Todos.Single().RecordName == work.Todos[0].RecordName &&
            reopened.Todos[0].Text == work.Todos[0].Text && reopened.Todos[0].Done, "text escaping and task state round-trip");
        string noteHash = TrackingStorage.HashFile(work.NotesPath), todoHash = TrackingStorage.HashFile(work.TodoPath);
        reopened.Save();
        Check(TrackingStorage.HashFile(work.NotesPath) == noteHash && TrackingStorage.HashFile(work.TodoPath) == todoHash &&
            Directory.GetFiles(root, "*.backup-*").Length == 0, "unchanged saves neither normalize nor rotate files");
        File.WriteAllText(work.NotesPath, "external edit");
        reopened.Notes = "local unsaved";
        Reject<IOException>(() => reopened.Save(), "external notes edits reject stale saves");
        Check(File.ReadAllText(work.NotesPath) == "external edit" && reopened.Notes == "local unsaved",
            "both external and unsaved local intent survive conflict");
        reopened = new ModWork(root, "example.mod", true, true);
        reopened.Notes = "new notes";
        File.WriteAllText(work.TodoPath, "unrelated TODO file");
        Reject<IOException>(() => reopened.Save(), "a later task conflict is surfaced after independently saved notes");
        Check(File.ReadAllText(work.NotesPath) == "new notes" && File.ReadAllText(work.TodoPath) == "unrelated TODO file",
            "partial multi-file save never overwrites conflicting task bytes");
        Check(Directory.GetFiles(root, "notes.txt.backup-*").Any(x => File.ReadAllText(x) == "external edit"),
            "replacement preserves unique previous notes bytes");
        Reject<InvalidDataException>(() => new ModWork(root, "example.mod", true, true), "unrecognized task content fails closed");
        File.WriteAllText(work.TodoPath, "FCS-Scribe Mod To Do 1\nMod: other.mod\n");
        Reject<InvalidDataException>(() => new ModWork(root, "example.mod", false, true), "other-mod task content is not adopted");
        File.Delete(work.TodoPath);
        var fresh = new ModWork(root, "example.mod", true, true); fresh.Save();
        var competing = new ModWork(root, "example.mod", true, true);
        fresh.Notes = "first writer"; fresh.Save(); competing.Notes = "second writer";
        Reject<IOException>(() => competing.Save(), "separate Scribe windows cannot overwrite newer work");
        var locked = new ModWork(root, "example.mod", true, true);
        locked.Notes = "lease contender";
        using (var lease = new FileStream(locked.NotesPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            Reject<IOException>(() => locked.Save(), "cooperating text saves honor the file lease");
        var invalid = new ModWork(root, "example.mod", false, true);
        invalid.Todos.Add(new ModTodo { RecordId = "id", RecordName = "record", Text = "task", Id = "not-a-guid" });
        Reject<InvalidDataException>(() => invalid.Save(), "invalid task identity does not serialize");
        var location = ProjectLocation.Open(root, "mod-id");
        Check(location.Directory == root && !File.Exists(Path.Combine(root, "FCS-Scribe.location.json")), "project defaults are mod-local and read-only on open");
        string external = Path.Combine(root, "external"); Directory.CreateDirectory(external);
        location.Switch(external);
        Check(ProjectLocation.Open(root, "mod-id").Directory == external && location.SettingsPath == Path.Combine(external, "FCS-Scribe.settings.json"),
            "external storage is discoverable through the small mod-local locator");
        Reject<InvalidDataException>(() => ProjectLocation.Open(root, "other-mod"), "locator identity prevents another mod adopting external work");
        Reject<InvalidDataException>(() => ProjectLocation.RequireDirectory("relative"), "relative project folders reject");
        string reserved = Path.Combine(root, "tracking.json.scribe"); Directory.CreateDirectory(reserved);
        Reject<InvalidOperationException>(() => ProjectLocation.RequireDirectory(reserved), "project work cannot write into tracking storage");
        string settingsAnchor = Path.Combine(external, "FCS-Scribe.settings.json");
        Directory.CreateDirectory(settingsAnchor + ".scribe");
        Reject<InvalidOperationException>(() => SettingsStorage.Save(settingsAnchor, new PluginSettings(), null, false),
            "mod-local settings cannot claim an existing tracking anchor");
        Check(!File.Exists(settingsAnchor), "settings collision leaves the tracking anchor uncreated");
        File.WriteAllBytes(Path.Combine(external, "notes.txt"), new byte[] { 0xff, 0xff });
        Reject<DecoderFallbackException>(() => new ModWork(external, "example.mod", true, false), "invalid UTF-8 is not silently replaced");
        File.WriteAllText(Path.Combine(external, "todo.txt"), "FCS-Scribe Mod To Do 1\nMod: example.mod\n[ ]\tbad\n");
        Reject<InvalidDataException>(() => new ModWork(external, "example.mod", false, true), "truncated TODO rows reject");
        var ignored = new ModWork(external, "example.mod", false, false); ignored.Save();
        Check(File.ReadAllBytes(ignored.NotesPath).SequenceEqual(new byte[] { 0xff, 0xff }) &&
            File.ReadAllText(ignored.TodoPath).EndsWith("[ ]\tbad\n"),
            "disabled features neither parse nor modify existing non-Scribe text");
        string id = Guid.NewGuid().ToString("N");
        File.WriteAllText(ignored.TodoPath, "FCS-Scribe Mod To Do 1\nMod: example.mod\n[ ]\t" + id +
            "\tid\tname\ttext\n[ ]\t" + id.ToUpperInvariant() + "\tid\tname\ttext\n");
        Reject<InvalidDataException>(() => new ModWork(external, "example.mod", false, true), "GUID casing cannot hide duplicate tasks");
        File.WriteAllText(ignored.TodoPath, "FCS-Scribe Mod To Do 1\nMod: example.mod\n[ ]\t" + id + "\tid\tname\tbad\\z\n");
        Reject<InvalidDataException>(() => new ModWork(external, "example.mod", false, true), "unknown text escapes reject");
        File.WriteAllText(ignored.NotesPath, "BOM and line endings\r\n", new UTF8Encoding(true));
        string bomHash = TrackingStorage.HashFile(ignored.NotesPath);
        new ModWork(external, "example.mod", true, false).Save();
        Check(TrackingStorage.HashFile(ignored.NotesPath) == bomHash, "unchanged BOM and line endings are byte-preserved");
        Check(Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories).Length == 0, "atomic saves leave no temporary files");
        return passed;
    }
}
