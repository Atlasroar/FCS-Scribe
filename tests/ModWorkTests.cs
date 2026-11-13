using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
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
        CheckTaskMetadata(Path.Combine(root, "metadata"));
        CheckTaskQueries();
        return passed;
    }

    private static void CheckTaskMetadata(string root)
    {
        Directory.CreateDirectory(root);
        string id = Guid.NewGuid().ToString("N"), path = Path.Combine(root, "todo.txt");
        string legacy = "FCS-Scribe Mod To Do 1\r\nMod: example.mod\r\n[x]\t" + id +
            "\tstable-1\tLegacy\\tname\tKeep\\nall text\r\n";
        File.WriteAllText(path, legacy, new UTF8Encoding(true));
        byte[] original = File.ReadAllBytes(path);
        var work = new ModWork(root, "example.mod", false, true);
        ModTodo old = work.Todos.Single();
        Check(old.Id == id && old.Done && old.RecordName == "Legacy\tname" && old.Text == "Keep\nall text",
            "legacy tasks retain exact identity, completion and escaped text");
        Check(old.RecordType == null && old.Priority == null && old.CreatedUtc == null && !old.CreatedDateUtc.HasValue,
            "legacy tasks do not invent type, priority or creation dates");
        new ModTodoQuery { Search = "legacy", Sort = ModTodoColumn.CreatedDate }.Apply(work.Todos);
        work.Save();
        Check(File.ReadAllBytes(path).SequenceEqual(original) && Directory.GetFiles(root, "*.backup-*").Length == 0,
            "legacy loading, queries and unchanged saves preserve raw BOM/CRLF bytes without migration");
        old.Priority = "High"; work.Save();
        Check(File.ReadAllText(path).StartsWith("FCS-Scribe Mod To Do 2\n") &&
            Directory.GetFiles(root, "todo.txt.backup-*").Any(file => File.ReadAllBytes(file).SequenceEqual(original)),
            "a real legacy task edit writes format 2 and preserves an exact unique original backup");
        old = new ModWork(root, "example.mod", false, true).Todos.Single();
        Check(old.Id == id && old.Done && old.Priority == "High" && old.RecordType == null && old.CreatedUtc == null &&
            old.Text == "Keep\nall text", "legacy migration preserves unknown metadata and original task content");
        DateTime created = new DateTime(2026, 11, 5, 12, 34, 56, DateTimeKind.Utc);
        var target = new ModTodo { RecordId = "stable-2", RecordName = "Name \u03a9", RecordType = "ITEM" };
        ModTodo task = ModTodo.Create(target, "Description\twith\r\nlines\\", created);
        Check(task.Id != target.Id && !task.Done && task.Priority == "Normal" && task.RecordType == "ITEM" &&
            task.CreatedDateUtc == created && task.CreatedUtc == created.ToString("o", CultureInfo.InvariantCulture),
            "new tasks receive fresh identity, captured type, Normal priority and the exact supplied UTC date");
        work = new ModWork(root, "example.mod", false, true); work.Todos.Add(task); work.Save();
        var reopened = new ModWork(root, "example.mod", false, true);
        ModTodo copy = reopened.Todos.Single(item => item.Id == task.Id);
        Check(copy.RecordId == task.RecordId && copy.RecordName == task.RecordName && copy.RecordType == task.RecordType &&
            copy.Text == task.Text && copy.Priority == task.Priority && copy.CreatedUtc == task.CreatedUtc,
            "format 2 round-trips all metadata and literal description text");
        string hash = TrackingStorage.HashFile(path); reopened.Save();
        Check(TrackingStorage.HashFile(path) == hash, "unchanged format 2 saves retain exact bytes");
        Reject<InvalidDataException>(() => ModTodo.Create(target, "text", DateTime.SpecifyKind(created, DateTimeKind.Local)),
            "local creation times cannot silently become UTC");
        Reject<InvalidDataException>(() => ModTodo.Create(new ModTodo { RecordId = "id" }, "text", created),
            "new tasks require a captured type");
        string valid = File.ReadAllText(path);
        foreach (string invalidPriority in new[] { "Urgent", "low", " High " })
        {
            File.WriteAllText(path, valid.Replace("\tNormal\t", "\t" + invalidPriority + "\t"));
            Reject<InvalidDataException>(() => new ModWork(root, "example.mod", false, true),
                "invalid priority metadata fails closed: " + invalidPriority);
        }
        File.WriteAllText(path, valid.Replace(task.CreatedUtc, "2026-11-05"));
        Reject<InvalidDataException>(() => new ModWork(root, "example.mod", false, true), "non-UTC/non-round-trip dates fail closed");
        File.WriteAllText(path, valid.Replace(task.CreatedUtc, "2026-11-05T12:34:56.0000000+01:00"));
        Reject<InvalidDataException>(() => new ModWork(root, "example.mod", false, true), "offset dates are not accepted as recorded UTC");
        File.WriteAllText(path, valid.Replace("\tITEM\t", "\t \t"));
        Reject<InvalidDataException>(() => new ModWork(root, "example.mod", false, true), "whitespace record type is not invented or normalized");
        File.WriteAllText(path, valid.Replace("\tITEM\tNormal\t", "\tITEM\t"));
        Reject<InvalidDataException>(() => new ModWork(root, "example.mod", false, true), "truncated format 2 metadata fails closed");
        File.WriteAllText(path, valid);
        reopened = new ModWork(root, "example.mod", false, true);
        reopened.Todos[0].Priority = "Low"; File.WriteAllText(path, valid + "\n");
        Reject<IOException>(() => reopened.Save(), "metadata edits still honor original-file conflict guards");
        Check(File.ReadAllText(path) == valid + "\n" && reopened.Todos[0].Priority == "Low",
            "a metadata conflict preserves external bytes and local intent");
    }

    private static void CheckTaskQueries()
    {
        DateTime created = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        ModTodo a = ModTodo.Create(new ModTodo { RecordId = "string-1", RecordName = "Zulu sword", RecordType = "ITEM" },
            "Review damage", created);
        ModTodo b = ModTodo.Create(new ModTodo { RecordId = "string-2", RecordName = "alpha research", RecordType = "RESEARCH" },
            "Review cost", created.AddMonths(-1));
        a.Priority = "High"; b.Priority = "Low"; b.Done = true;
        var legacy = new ModTodo { RecordId = "string-3", RecordName = "Legacy task", Text = "Unknown metadata", Priority = null };
        var tasks = new List<ModTodo> { a, b, legacy };
        Check(new ModTodoQuery().Apply(tasks).SequenceEqual(tasks), "default query retains original task order and references");
        foreach (var test in new[] {
            new { Field = ModTodoField.ItemName, Search = "SWORD" },
            new { Field = ModTodoField.StringId, Search = "STRING-1" },
            new { Field = ModTodoField.ItemType, Search = "item" },
            new { Field = ModTodoField.Priority, Search = "HIGH" },
            new { Field = ModTodoField.CreatedDate, Search = "2026-11-05" },
            new { Field = ModTodoField.Description, Search = "DAMAGE" } })
            Check(new ModTodoQuery { Field = test.Field, Search = test.Search }.Apply(tasks).SequenceEqual(new[] { a }),
                "field-specific case-insensitive search: " + test.Field);
        Check(new ModTodoQuery { Search = "  ReSeArCh  " }.Apply(tasks).SequenceEqual(new[] { b }),
            "all-field search trims its query and finds captured types/names");
        Check(new ModTodoQuery { Field = ModTodoField.Description, Search = "sword" }.Apply(tasks).Count == 0,
            "a scoped search does not leak matches from another field");
        Check(new ModTodoQuery { State = ModTodoState.Open }.Apply(tasks).SequenceEqual(new[] { a, legacy }) &&
            new ModTodoQuery { State = ModTodoState.Completed }.Apply(tasks).SequenceEqual(new[] { b }),
            "completion filters retain open versus completed task identities");
        Check(new ModTodoQuery { ItemType = "ITEM" }.Apply(tasks).SequenceEqual(new[] { a }) &&
            new ModTodoQuery { Priority = "Unspecified", ItemType = "Unknown" }.Apply(tasks).SequenceEqual(new[] { legacy }),
            "captured and explicit unknown metadata can be filtered without inference");
        DateTime day = created.ToLocalTime().Date;
        Check(new ModTodoQuery { CreatedFrom = day, CreatedTo = day }.Apply(tasks).SequenceEqual(new[] { a }),
            "inclusive local-date filters include the boundary day and exclude unknown dates");
        Check(new ModTodoQuery { CreatedTo = day }.Apply(tasks).SequenceEqual(new[] { a, b }) &&
            new ModTodoQuery { CreatedFrom = day }.Apply(tasks).SequenceEqual(new[] { a }),
            "one-sided date filters work independently");
        Check(new ModTodoQuery { Search = "cost", State = ModTodoState.Completed, ItemType = "RESEARCH", Priority = "Low",
            CreatedFrom = b.CreatedDateUtc.Value.ToLocalTime().Date, CreatedTo = b.CreatedDateUtc.Value.ToLocalTime().Date }
            .Apply(tasks).SequenceEqual(new[] { b }), "all filters combine rather than replace each other");
        Check(new ModTodoQuery { Sort = ModTodoColumn.ItemName }.Apply(tasks).SequenceEqual(new[] { b, legacy, a }),
            "name sorting is case-insensitive");
        Check(new ModTodoQuery { Sort = ModTodoColumn.StringId, Descending = true }.Apply(tasks).SequenceEqual(new[] { legacy, b, a }) &&
            new ModTodoQuery { Sort = ModTodoColumn.ItemType }.Apply(tasks).SequenceEqual(new[] { a, b, legacy }),
            "string IDs and captured item types sort as literal text");
        Check(new ModTodoQuery { Sort = ModTodoColumn.Description }.Apply(tasks).SequenceEqual(new[] { b, a, legacy }),
            "description sorting uses task text rather than record names");
        Check(new ModTodoQuery { Sort = ModTodoColumn.Priority }.Apply(tasks).SequenceEqual(new[] { legacy, b, a }) &&
            new ModTodoQuery { Sort = ModTodoColumn.Priority, Descending = true }.Apply(tasks).SequenceEqual(new[] { a, b, legacy }),
            "priority sorting uses priority rank, not alphabetical labels");
        Check(new ModTodoQuery { Sort = ModTodoColumn.CreatedDate, Descending = true }.Apply(tasks).SequenceEqual(new[] { a, b, legacy }),
            "creation sorting uses UTC timestamps with explicit unknown entries");
        Check(new ModTodoQuery { Sort = ModTodoColumn.Done }.Apply(tasks).SequenceEqual(new[] { a, legacy, b }) &&
            tasks.SequenceEqual(new[] { a, b, legacy }), "stable completion sorting does not mutate the source list");
        Reject<InvalidOperationException>(() => new ModTodoQuery { CreatedFrom = day, CreatedTo = day.AddDays(-1) }.Apply(tasks),
            "reversed date ranges are explicit errors");
        Reject<InvalidOperationException>(() => new ModTodoQuery { Priority = "invalid" }.Apply(tasks), "unknown priority filters reject");
        Reject<InvalidOperationException>(() => new ModTodoQuery { Field = (ModTodoField)100 }.Apply(tasks), "unknown search fields reject");
        Reject<InvalidOperationException>(() => new ModTodoQuery { Sort = (ModTodoColumn)100 }.Apply(tasks), "unknown sort columns reject");
        Check(ModTodoQuery.CreationLabel(legacy) == "Unknown" && ModTodoQuery.PriorityLabel(legacy) == "Unspecified" &&
            ModTodoQuery.TypeLabel(legacy) == "Unknown", "missing metadata remains explicitly unknown in presentation");
    }
}
