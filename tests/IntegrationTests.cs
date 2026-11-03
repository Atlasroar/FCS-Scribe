using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
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
    public static int Main(string[] args)
    {
        int result = 1;
        using (var host = new Form { ShowInTaskbar = false, Opacity = 0 })
        {
            host.Shown += delegate {
                host.BeginInvoke(new Action(delegate {
                    try { result = Run(args.Contains("--settings-recovery")); }
                    finally { host.Close(); }
                }));
            };
            Application.Run(host);
        }
        return result;
    }

    private static int Run(bool settingsOnly)
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
            File.Copy(Path.Combine(directory, "fixture.mod"), output, true);
            Check((bool)FcsAdapter.Call(data, "load", output, Enum.Parse(mode, "ACTIVE"), false, false),
                "load disposable active mod");
            FcsAdapter.Call(data, "resolveAllReferences");
            Set(FcsAdapter.Field(editor, "modInfo"), "Data", data);
            string originalModBytes = Convert.ToBase64String(File.ReadAllBytes(output));
            object todo = FcsAdapter.Field(editor, "todoList");
            FcsAdapter.Call(todo, "LoadAll");
            var adapter = new FcsAdapter(assembly, editor);
            if (settingsOnly)
            {
                CheckSettingsWindow(adapter, editor);
                Console.WriteLine("PASS: " + passed + " settings recovery integration checks");
                return 0;
            }
            CheckWorkshop(adapter, editor, data, nav, assembly);
            CheckScribeWindow(adapter, editor);
            CheckInitializationWindow(adapter, editor);
            CheckDraftWorkspaceWindow(adapter, editor);
            CheckTrackingWindow(adapter, editor);
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
            string secondId = initial.Records.First(x => x.Id != id).Id;
            object secondItem = records[secondId];
            Set(secondItem, "Name", (string)FcsAdapter.Property(secondItem, "Name") + " Scribe coverage");
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
            using (var project = JournalProject.Create(Path.Combine(directory, "notes-range.json"), initial))
            {
                project.BeginSession(); project.Record(adapter.Capture());
                var recordings = project.ExportRecordings(adapter.Capture());
                packet = RangeExport.Create(project.ProjectId, recordings, 0, recordings.Count - 1, adapter.Capture());
                Check(packet.SchemaVersion == 3 && packet.Changes.All(x => x.Id.Contains(":change-")),
                    "the actual notes workflow uses qualified recording-range evidence");
            }
            var draft = new NoteDraft {
                BatchId = packet.BatchId, SourceFingerprint = packet.SourceFingerprint,
                Entries = packet.Changes.GroupBy(x => x.RecordId).Select(group => new NoteEntry {
                    TargetRecordId = group.Key,
                    Text = group.Key == id ? "Approved test documentation." : "Second approved documentation.",
                    ChangeIds = group.Select(x => x.Id).ToList()
                }).ToList()
            };
            Snapshot beforeSave = adapter.Capture();
            string gameplay = beforeSave.Fingerprint();
            var beforeNotes = draft.Entries.ToDictionary(x => x.TargetRecordId, x => adapter.Notes(x.TargetRecordId));
            NoteDraft incomplete = Json.Read<NoteDraft>(Json.Write(draft));
            incomplete.Entries.RemoveAt(1);
            Reject<InvalidDataException>(() => adapter.Apply(packet, incomplete),
                "incomplete record coverage is rejected before actual FCS notes mutation");
            Check(beforeNotes.All(x => adapter.Notes(x.Key) == x.Value) && !(bool)FcsAdapter.Property(nav, "HasChanges"),
                "rejected coverage leaves notes and dirty state unchanged");
            NoteDraft duplicated = Json.Read<NoteDraft>(Json.Write(draft));
            duplicated.Entries[1].ChangeIds.Add(duplicated.Entries[0].ChangeIds[0]);
            Reject<InvalidDataException>(() => adapter.Apply(packet, duplicated),
                "duplicate evidence across targets is rejected by the actual notes workflow");
            NoteEntry completed = draft.Entries.Single(x => x.TargetRecordId == id);
            FcsAdapter.Call(todo, "AddItem", item, DraftValidation.NoteText(packet.BatchId, completed),
                Enum.Parse(noteType, "NOTE"), null, true, false);
            string completedNotes = adapter.Notes(id);
            adapter.Apply(packet, draft);
            Check(adapter.Notes(id) == completedNotes && adapter.Notes(secondId).Contains("Second approved documentation."),
                "retry preserves a completed FCS note byte-for-byte and appends only the pending target");
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
            Check(adapter.Notes(secondId).Contains("Second approved documentation."),
                "the pending note appended on retry survives normal save and reload");
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
        finally
        {
            if (editor != null) editor.Dispose();
        }
    }

    private static void CheckScribeWindow(FcsAdapter adapter, Form editor)
    {
        using (var window = new ScribeWindow(adapter, editor, Path.Combine(Directory.GetCurrentDirectory(), "plugin-settings.json")))
        {
            Check(window.IsMdiChild && window.MdiParent == editor, "Scribe uses FCS's actual MDI container");
            var actions = window.Controls.OfType<MenuStrip>().Single();
            var help = (Label)FcsAdapter.Field(window, "help");
            var status = (Label)FcsAdapter.Field(window, "status");
            string originalStatus = status.Text;
            var menus = actions.Items.Cast<ToolStripMenuItem>().ToList();
            Check(menus.Select(x => x.Text.Replace("&", "")).SequenceEqual(
                new[] { "Core", "Workshop", "Draft", "Apply approved notes", "Settings" }),
                "five top-level menus/actions replace the flat button row");
            Check(window.MainMenuStrip == actions && !actions.AllowMerge && actions.TabStop,
                "menus stay inside Scribe and support keyboard access without merging into FCS");
            string[][] groups = {
                new[] { "Start tracking", "Review changes", "Compile journal", "Export AI packet" },
                new[] { "Export Workshop info", "Archive preview image" },
                new[] { "Import draft", "Paste draft", "Save workspace", "Open workspace" }
            };
            for (int i = 0; i < groups.Length; i++)
                Check(menus[i].DropDownItems.Cast<ToolStripMenuItem>().Select(x => x.Text.Replace("&", "")).SequenceEqual(groups[i]),
                    menus[i].Text + " contains the requested actions in order");
            var commands = menus.SelectMany(x => x.HasDropDownItems ?
                x.DropDownItems.Cast<ToolStripMenuItem>() : new[] { x }).ToList();
            Check(commands.Count == 12, "all ten original actions plus two workspace actions remain available");
            foreach (ToolStripMenuItem item in menus.Take(3).Concat(commands))
            {
                typeof(ToolStripItem).GetMethod("HandleMouseEnter", Flags).Invoke(item, new object[] { EventArgs.Empty });
                Check(!String.IsNullOrEmpty(item.AccessibleDescription) && help.Text == item.AccessibleDescription &&
                    status.Text == originalStatus, item.Text + " hover explains action without erasing operation status");
                typeof(ToolStripItem).GetMethod("HandleMouseLeave", Flags).Invoke(item, new object[] { EventArgs.Empty });
                Check(help.Text != item.AccessibleDescription, item.Text + " hover help clears on exit");
            }
            var completion = new TaskCompletionSource<int>();
            Task operation = (Task)FcsAdapter.Call(window, "RunBusy", new Func<Task>(() => completion.Task));
            try { Check(!actions.Enabled, "the entire menu is disabled while an operation is pending"); }
            finally { completion.SetResult(0); AwaitUi(operation); }
            Check(actions.Enabled, "the menu is re-enabled after an operation completes");
            using (var host = new Form { IsMdiContainer = true, ShowInTaskbar = false, Opacity = 0,
                Width = 1200, Height = 900 })
            {
                window.MdiParent = host;
                host.Show(); window.Show();
                foreach (ToolStripMenuItem menu in menus)
                {
                    menu.Select(); actions.Refresh();
                    Check(help.Text == menu.AccessibleDescription, menu.Text + " keyboard selection explains the menu/action");
                    if (!menu.HasDropDownItems) continue;
                    menu.ShowDropDown();
                    foreach (ToolStripMenuItem item in menu.DropDownItems)
                    {
                        item.Select(); menu.DropDown.Refresh();
                        Check(help.Text == item.AccessibleDescription && status.Text == originalStatus,
                            item.Text + " keyboard selection explains the action without erasing operation status");
                    }
                    menu.HideDropDown();
                }
                window.Width = 320; window.PerformLayout(); actions.PerformLayout();
                Check(actions.OverflowButton.Visible && menus.Last().Placement == ToolStripItemPlacement.Overflow,
                    "narrow windows retain access to actions through the overflow menu");
                actions.OverflowButton.ShowDropDown();
                menus.Last().Select(); actions.OverflowButton.DropDown.Refresh();
                Check(help.Text == menus.Last().AccessibleDescription,
                    "keyboard selection in the overflow menu retains contextual help");
                actions.OverflowButton.HideDropDown();
                window.Hide(); window.MdiParent = editor;
            }

            Check(window.Controls.OfType<TabControl>().Single().TabPages.Cast<TabPage>().Any(x => x.Text == "Workshop info"),
                "Workshop context has a read-only review tab");
            window.PerformLayout();
            Check(help.Dock == DockStyle.Bottom && help.Bottom <= status.Top, "hover help is at the window bottom above persistent status");
        }
    }

    private static void AwaitUi(Task task)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && timeout.Elapsed < TimeSpan.FromMinutes(3))
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        if (!task.IsCompleted) throw new TimeoutException("Background tracking operation did not complete.");
        task.GetAwaiter().GetResult();
    }

    private static void CheckDraftWorkspaceWindow(FcsAdapter adapter, Form editor)
    {
        string directory = Directory.GetCurrentDirectory();
        string settings = Path.Combine(directory, "draft-settings.json");
        string path = Path.Combine(directory, "mods", "draft-workspace.json");
        Snapshot before = adapter.Capture();
        object nav = FcsAdapter.Field(editor, "nav");
        IDictionary records = (IDictionary)FcsAdapter.Field(FcsAdapter.Field(FcsAdapter.Field(nav, "ou"), "gameData"), "items");
        object item = records.Values.Cast<object>().First();
        string name = (string)FcsAdapter.Property(item, "Name");
        try
        {
            Set(item, "Name", name + " Workspace fixture");
            Snapshot current = adapter.Capture();
            Packet packet = new Tracker(before).Export(current);
            NoteDraft response = Json.Read<NoteDraft>(Json.Write(packet.ResponseTemplate));
            foreach (NoteEntry entry in response.Entries) entry.Text = "Workspace fixture documentation.";
            string text = " \r\n" + Json.Pretty(Json.Write(response)) + "\r\n";
            using (var window = new ScribeWindow(adapter, editor, settings))
            {
                window.CreateControl();
                var draft = (TextBox)FcsAdapter.Field(window, "draft");
                FcsAdapter.Call(window, "AdoptWorkspace", packet, text, null, null);
                Check((bool)FcsAdapter.Property(window, "DraftNeedsSave") && window.Text.Contains("* unsaved"),
                    "new draft/packet association is visibly unsaved");
                RunWindow(window, "SaveWorkspacePath", path);
                Check(!(bool)FcsAdapter.Property(window, "DraftNeedsSave") && !window.Text.Contains("* unsaved"),
                    "saving the complete workspace clears the unsaved indicator");
                draft.Text += " ";
                Check((bool)FcsAdapter.Property(window, "DraftNeedsSave"),
                    "editing a saved draft marks it unsaved");
                Put(window, "draftDecision", new Func<string, DialogResult>(operation => DialogResult.Cancel));
                RunWindow(window, "StartPath", Path.Combine(directory, "mods", "cancelled-draft-tracking.json"));
                Check(draft.Text == text + " " && Object.ReferenceEquals(FcsAdapter.Field(window, "exported"), packet) &&
                    !File.Exists(Path.Combine(directory, "mods", "cancelled-draft-tracking.json")),
                    "cancelled Start retains exact draft, original association and filesystem");
                RunWindow(window, "OpenWorkspacePath", path);
                Check(draft.Text == text + " ", "cancelled Open retains unsaved edits");
                RunWindow(window, "ResumePath", Path.Combine(directory, "mods", "missing-cancelled-resume.json"));
                Check(draft.Text == text + " " && Object.ReferenceEquals(FcsAdapter.Field(window, "exported"), packet),
                    "cancelled Resume does not read a missing project or lose draft associations");
                Put(window, "draftDecision", new Func<string, DialogResult>(operation => DialogResult.No));
                RunWindow(window, "OpenWorkspacePath", path);
                Check(draft.Text == text && !(bool)FcsAdapter.Property(window, "DraftNeedsSave"),
                    "explicit discard permits reopening a saved workspace");
                Packet originalPacket = (Packet)FcsAdapter.Field(window, "exported");
                RunWindow(window, "SaveWorkspacePath", path);
                draft.Text = "{ unfinished editor text";
                Task saving = (Task)FcsAdapter.Call(window, "SaveWorkspacePath", path);
                draft.Text += " typed during background save";
                AwaitUi(saving);
                Check(draft.Text.EndsWith("typed during background save") &&
                    (bool)FcsAdapter.Property(window, "DraftNeedsSave") &&
                    DraftWorkspace.Load(path).DraftText == "{ unfinished editor text",
                    "background saving retains late edits and leaves them visibly unsaved");
                Task opening = (Task)FcsAdapter.Call(window, "OpenWorkspacePath", path);
                draft.Text += " typed during background load";
                AwaitUi(opening);
                Check(draft.Text.EndsWith("typed during background load") &&
                    Object.ReferenceEquals(FcsAdapter.Field(window, "exported"), originalPacket),
                    "background loading cancels replacement if new edits arrive");
                string lateText = draft.Text;
                var candidate = JournalProject.Create(Path.Combine(directory, "mods", "draft-race-tracking.json"), current);
                long revision = (long)FcsAdapter.Field(window, "draftRevision");
                Task beginning = (Task)FcsAdapter.Call(window, "Begin", candidate, null, revision);
                draft.Text += " typed during background session start";
                AwaitUi(beginning);
                Check(draft.Text.StartsWith(lateText) && FcsAdapter.Field(window, "project") == null,
                    "late edits prevent session adoption from clearing the draft");
                var closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
                Put(window, "busy", true);
                FcsAdapter.Call(window, "OnFormClosing", closing);
                Put(window, "busy", false);
                Check(closing.Cancel && !(bool)FcsAdapter.Field(window, "closing"),
                    "closing during background work is cancelled without disposing state");
                Put(window, "draftDecision", new Func<string, DialogResult>(operation => DialogResult.Cancel));
                closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
                FcsAdapter.Call(window, "OnFormClosing", closing);
                Check(closing.Cancel && !(bool)FcsAdapter.Field(window, "closing") && draft.Text.Contains("session start"),
                    "cancelled close keeps the latest draft and window alive");
                string beforeFailedOpen = draft.Text;
                string loadedHash = TrackingStorage.HashFile(path);
                Put(window, "draftDecision", new Func<string, DialogResult>(operation => {
                    File.AppendAllText(path, " ");
                    return DialogResult.No;
                }));
                bool changedDuringOpen = false;
                try { RunWindow(window, "OpenWorkspacePath", path); }
                catch (IOException) { changedDuringOpen = true; }
                Check(changedDuringOpen && draft.Text == beforeFailedOpen &&
                    TrackingStorage.HashFile(path) != loadedHash,
                    "workspace changes during the replacement prompt cancel adoption of old bytes");
                string external = File.ReadAllText(path) + " ";
                File.WriteAllText(path, external);
                bool rejected = false;
                try { RunWindow(window, "SaveWorkspacePath", path); }
                catch (IOException) { rejected = true; }
                Check(rejected && File.ReadAllText(path) == external && (bool)FcsAdapter.Property(window, "DraftNeedsSave"),
                    "external workspace changes block saving without losing edits");
                string ordinary = Path.Combine(directory, "mods", "not-a-workspace.json");
                File.WriteAllText(ordinary, Json.Write(packet));
                rejected = false;
                try { RunWindow(window, "SaveWorkspacePath", ordinary); }
                catch (InvalidDataException) { rejected = true; }
                Check(rejected && File.ReadAllText(ordinary) == Json.Write(packet),
                    "workspace Save cannot overwrite an ordinary AI packet");
            }
            using (var reopened = new ScribeWindow(adapter, editor, settings))
            {
                RunWindow(reopened, "OpenWorkspacePath", path);
                Check(((TextBox)FcsAdapter.Field(reopened, "draft")).Text == "{ unfinished editor text" &&
                    ((Label)FcsAdapter.Field(reopened, "status")).Text.Contains("invalid JSON"),
                    "new window restores unfinished work and explicitly reports validation failure");
                DraftWorkspace saved = DraftWorkspace.Create(packet, text);
                string complete = Path.Combine(directory, "mods", "complete-workspace.json");
                TrackingStorage.WriteAtomic(complete, saved, null);
                RunWindow(reopened, "OpenWorkspacePath", complete);
                Check(((Label)FcsAdapter.Field(reopened, "status")).Text.Contains("pass evidence/current-state checks") &&
                    FcsAdapter.Field(reopened, "tracker") == null,
                    "restored complete workspace needs no tracking session and is not automatically applied");
                Set(item, "Name", name);
                RunWindow(reopened, "OpenWorkspacePath", complete);
                Check(((Label)FcsAdapter.Field(reopened, "status")).Text.Contains("stale"),
                    "reopening a stale workspace preserves text for review without permitting application");
                Reject<InvalidOperationException>(() => adapter.Apply(saved.Packet, response),
                    "restored stale packet cannot bypass adapter application checks");
            }
        }
        finally { Set(item, "Name", name); }
    }

    private static void CheckInitializationWindow(FcsAdapter adapter, Form editor)
    {
        string directory = Directory.GetCurrentDirectory();
        string path = Path.Combine(directory, "mods", "failed-initialization.json");
        Snapshot current = adapter.Capture();
        Reject<IOException>(() => JournalProject.Create(path, current, step => {
            if (step == "baseline-flush") throw new IOException("Injected baseline write failure.");
        }), "window fixture leaves a recognized pending marker without a baseline");
        Check(!File.Exists(path) && File.Exists(Path.Combine(path + ".scribe", "project.json")),
            "interrupted initialization is distinguishable from a completed tracking project");
        using (var window = new ScribeWindow(adapter, editor, Path.Combine(directory, "initialization-settings.json")))
        {
            RunWindow(window, "StartPath", path);
            var project = (JournalProject)FcsAdapter.Field(window, "project");
            Check(project.Fingerprint == current.Fingerprint() && project.EntryCount == 0 &&
                File.Exists(project.SessionPath), "actual Start retries an uncommitted attempt and opens its first session");
        }
        string committed = Path.Combine(directory, "mods", "committed-initialization.json");
        Reject<IOException>(() => JournalProject.Create(committed, current, step => {
            if (step == "final-marker-flush") throw new IOException("Injected final marker failure.");
        }), "window fixture interrupts finalization after a complete baseline commit");
        string hash = TrackingStorage.HashFile(committed);
        using (var window = new ScribeWindow(adapter, editor, Path.Combine(directory, "initialization-settings.json")))
        {
            RunWindow(window, "ResumePath", committed);
            var project = (JournalProject)FcsAdapter.Field(window, "project");
            Check(project.Fingerprint == current.Fingerprint() && File.Exists(project.SessionPath) &&
                TrackingStorage.HashFile(committed) == hash,
                "actual background Resume finalizes initialization without rewriting the captured baseline");
            Check(!File.ReadAllText(Path.Combine(project.DirectoryName, "project.json")).Contains("\"Initialization\""),
                "resumed window uses the unchanged normal durable-marker format");
        }
    }

    private static void CheckSettingsWindow(FcsAdapter adapter, Form editor)
    {
        string directory = Directory.GetCurrentDirectory();
        string path = Path.Combine(directory, "recovery-settings.json");
        File.WriteAllText(path, "{ broken settings");
        string original = TrackingStorage.HashFile(path);
        object header = FcsAdapter.Field(FcsAdapter.Field(FcsAdapter.Field(
            FcsAdapter.Field(editor, "nav"), "ou"), "gameData"), "header");
        string author = (string)FcsAdapter.Field(header, "Author");
        using (var host = new Form { IsMdiContainer = true, ShowInTaskbar = false, Opacity = 0 })
        using (var window = new ScribeWindow(adapter, editor, path))
        {
            window.MdiParent = host; host.Show(); window.Show();
            var notice = (Label)FcsAdapter.Field(window, "settingsNotice");
            var timer = (System.Windows.Forms.Timer)FcsAdapter.Field(window, "timer");
            var draft = (TextBox)FcsAdapter.Field(window, "draft");
            Check(FcsAdapter.Field(window, "settingsError") != null && notice.Visible &&
                notice.Text.Contains(path) && notice.Text.Contains("SETTINGS RECOVERY") &&
                notice.Text.Contains("automatic recording disabled") && TrackingStorage.HashFile(path) == original,
                "corrupt settings open the actual window with a persistent recovery warning and untouched source");
            Check(window.MainMenuStrip.Items.Cast<ToolStripMenuItem>().Last().Enabled &&
                (string)FcsAdapter.Call(window, "ExportDirectory") == adapter.ActiveModDirectory(),
                "Settings stays reachable and safe manual mode uses the active mod export folder");
            string anchorPath = Path.Combine(directory, "mods", "settings-recovery-tracking.json");
            RunWindow(window, "StartPath", anchorPath);
            var project = (JournalProject)FcsAdapter.Field(window, "project");
            Packet packet = project.ResumeTracker().Export(project.State);
            Put(window, "exported", packet); draft.Text = "{ unfinished draft retained\r\n";
            try
            {
                var settings = (PluginSettings)FcsAdapter.Field(window, "settings");
                settings.ReviewSeconds = 30;
                ((JournalSchedule)FcsAdapter.Field(window, "schedule")).Reset(-JournalSchedule.CompilationMilliseconds, 30);
                FcsAdapter.Call(window, "ConfigureTimer");
                Put(header, "Author", "Settings recovery manual recording fixture");
                FcsAdapter.Call(timer, "OnTick", EventArgs.Empty);
                RunWindow(window, "AutomaticTick");
                Check(!timer.Enabled && project.EntryCount == 0,
                    "recovery explicitly gates timer dispatch and automatic work even with a nonzero interval");
                RunWindow(window, "Review");
                Check(project.EntryCount == 1 && project.PendingCount == 1 && notice.Visible,
                    "manual gameplay recording works in recovery without clearing its persistent warning");
                RunWindow(window, "ReloadSettings");
                Check(FcsAdapter.Field(window, "settingsError") != null && !timer.Enabled &&
                    TrackingStorage.HashFile(path) == original,
                    "failed reload keeps safe manual mode and never rewrites corrupt settings");
                File.WriteAllText(path, Json.Write(new PluginSettings()));
                string external = TrackingStorage.HashFile(path);
                bool stale = false;
                try { RunWindow(window, "SaveSettings", new PluginSettings { ReviewSeconds = 60 }); }
                catch (IOException) { stale = true; }
                Check(stale && TrackingStorage.HashFile(path) == external && notice.Visible,
                    "a stale recovery window cannot overwrite another instance's repaired settings");
                RunWindow(window, "ReloadSettings");
                Check(FcsAdapter.Field(window, "settingsError") == null && !notice.Visible && !timer.Enabled,
                    "explicit validated reload exits recovery without enabling manual-only settings");
                File.WriteAllText(path, "another malformed original");
                RunWindow(window, "ReloadSettings");
                RunWindow(window, "SaveSettings", new PluginSettings { ReviewSeconds = 30 });
                string backup = Directory.GetFiles(directory, "recovery-settings.json.recovery-*.bak").Single();
                Check(File.ReadAllText(backup) == "another malformed original" &&
                    PluginSettings.Load(path).ReviewSeconds == 30 && !notice.Visible && timer.Enabled,
                    "explicit repaired Save preserves original bytes and adopts validated recording preferences");
                string repairedHash = TrackingStorage.HashFile(path);
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    using (var blockedWindow = new ScribeWindow(adapter, editor, path))
                        Check(FcsAdapter.Field(blockedWindow, "settingsError") is IOException &&
                            FcsAdapter.Field(blockedWindow, "settingsHash") == null &&
                            ((PluginSettings)FcsAdapter.Field(blockedWindow, "settings")).ReviewSeconds == 0 &&
                            blockedWindow.MainMenuStrip.Items.Cast<ToolStripMenuItem>().Last().Enabled,
                            "actual sharing denial during construction still opens a usable manual recovery window");
                    RunWindow(window, "ReloadSettings");
                    bool unreadable = false;
                    try { RunWindow(window, "SaveSettings", new PluginSettings()); }
                    catch (IOException) { unreadable = true; }
                    Check(unreadable && FcsAdapter.Field(window, "settingsHash") == null && notice.Visible && !timer.Enabled,
                        "sharing-denied reload enters recovery and an unknown original hash cannot authorize repair");
                    using (var dialog = new SettingsDialog((PluginSettings)FcsAdapter.Field(window, "settings"), path,
                        (Exception)FcsAdapter.Field(window, "settingsError")))
                    {
                        Check(((Button)dialog.AcceptButton).Text == "Save repaired settings" &&
                            dialog.Controls[0].Controls.OfType<FlowLayoutPanel>().Last().Controls.OfType<Button>()
                                .Any(x => x.DialogResult == DialogResult.Retry),
                            "recovery Settings exposes explicit repair and discard-and-reload actions");
                        dialog.Shown += delegate { dialog.BeginInvoke(new Action(() => ((Button)dialog.CancelButton).PerformClick())); };
                        Check(dialog.ShowDialog(window) == DialogResult.Cancel, "recovery Settings can be cancelled normally");
                    }
                }
                Check(TrackingStorage.HashFile(path) == repairedHash &&
                    Directory.GetFiles(directory, "recovery-settings.json.recovery-*.bak").Length == 1,
                    "failed unreadable repair and dialog cancellation leave settings and recovery backups untouched");
                RunWindow(window, "ReloadSettings");
                Check(!notice.Visible && timer.Enabled && Object.ReferenceEquals(project, FcsAdapter.Field(window, "project")) &&
                    Object.ReferenceEquals(packet, FcsAdapter.Field(window, "exported")) && draft.Text == "{ unfinished draft retained\r\n",
                    "reload after access recovery retains the current project, original packet and exact unfinished draft");
            }
            finally { Put(header, "Author", author); }
        }
    }

    private static void CheckTrackingWindow(FcsAdapter adapter, Form editor)
    {
        string directory = Directory.GetCurrentDirectory();
        string path = Path.Combine(directory, "mods", "tracking.json");
        using (var window = new ScribeWindow(adapter, editor, Path.Combine(directory, "automation-settings.json")))
        {
            Check(adapter.ActiveModDirectory() == Path.Combine(directory, "mods"),
                "default project output is the active mod file's actual directory");
            Check((string)FcsAdapter.Call(window, "ExportDirectory") == adapter.ActiveModDirectory(),
                "all JSON choosers use the active mod default");
            var settings = (PluginSettings)FcsAdapter.Field(window, "settings");
            var timer = (System.Windows.Forms.Timer)FcsAdapter.Field(window, "timer");
            Check(settings.ReviewSeconds == 0 && !timer.Enabled, "automatic review is opt-in and waits for tracking");
            RunWindow(window, "StartPath", path);
            JournalProject first = (JournalProject)FcsAdapter.Field(window, "project");
            Check(first.EntryCount == 0 && File.Exists(path) && File.Exists(first.SessionPath),
                "Start saves one full baseline and opens an empty timestamped session");
            string originalSession = first.SessionPath, anchorHash = TrackingStorage.HashFile(path);
            var schedule = (JournalSchedule)FcsAdapter.Field(window, "schedule");
            var clock = (System.Diagnostics.Stopwatch)FcsAdapter.Field(window, "clock");
            foreach (int seconds in PluginSettings.Intervals.Where(x => x > 0))
            {
                settings.ReviewSeconds = seconds;
                long now = clock.ElapsedMilliseconds;
                schedule.Reset(now, seconds);
                FcsAdapter.Call(window, "ConfigureTimer");
                Check(timer.Enabled && timer.Interval == 1000 && schedule.NextReview == now + seconds * 1000L,
                    "automatic deadline is exactly " + seconds + " seconds with independent UI polling");
            }
            settings.ReviewSeconds = 0;
            FcsAdapter.Call(window, "ConfigureTimer");
            Check(!timer.Enabled, "manual review disables scheduling");
            ToolStripMenuItem reviewAction = window.MainMenuStrip.Items.Cast<ToolStripMenuItem>().First()
                .DropDownItems.Cast<ToolStripMenuItem>().Single(x => x.Text.Replace("&", "") == "Review changes");
            reviewAction.PerformClick();
            var reviewTimeout = System.Diagnostics.Stopwatch.StartNew();
            while ((bool)FcsAdapter.Field(window, "busy") && reviewTimeout.Elapsed < TimeSpan.FromMinutes(3))
            {
                Application.DoEvents(); Thread.Sleep(10);
            }
            Check(!(bool)FcsAdapter.Field(window, "busy") && !((bool)FcsAdapter.Field(window, "automaticPaused")),
                "the Review changes menu dispatches through the serialized asynchronous operation gate");
            Check(first.EntryCount == 0, "unchanged reviews do not append duplicate checkpoints");
            RunWindow(window, "ResumePath", path);
            JournalProject resumed = (JournalProject)FcsAdapter.Field(window, "project");
            Check(resumed.Fingerprint == first.Fingerprint && resumed.SessionPath != originalSession,
                "background resume retains validated state and starts a distinct session");
            bool protectedFile = false;
            try { FcsAdapter.Call(window, "ProtectTrackingFile", path); }
            catch (TargetInvocationException error) { protectedFile = error.InnerException is InvalidOperationException; }
            Check(protectedFile, "manual packet/Workshop exports cannot overwrite the active tracking JSON");
            object nav = FcsAdapter.Field(editor, "nav");
            object header = FcsAdapter.Field(FcsAdapter.Field(FcsAdapter.Field(nav, "ou"), "gameData"), "header");
            string originalAuthor = (string)FcsAdapter.Field(header, "Author");
            try
            {
                using (var host = new Form { IsMdiContainer = true, ShowInTaskbar = false, Opacity = 0 })
                {
                    window.MdiParent = host;
                    host.Show(); window.Show();
                    settings.ReviewSeconds = 30;
                    schedule.Reset(clock.ElapsedMilliseconds - 30000, 30);
                    Put(window, "automaticPaused", false);
                    FcsAdapter.Call(window, "ConfigureTimer");
                    Put(header, "Author", "Automatic checkpoint fixture");
                    var coreMenu = (ToolStripMenuItem)window.MainMenuStrip.Items[0];
                    coreMenu.ShowDropDown();
                    FcsAdapter.Call(timer, "OnTick", EventArgs.Empty);
                    Check(!(bool)FcsAdapter.Field(window, "busy") &&
                        ((JournalProject)FcsAdapter.Field(window, "project")).EntryCount == 0,
                        "automatic work defers while the user is browsing a menu");
                    coreMenu.HideDropDown();
                    FcsAdapter.Call(timer, "OnTick", EventArgs.Empty);
                    var timeout = System.Diagnostics.Stopwatch.StartNew();
                    while ((bool)FcsAdapter.Field(window, "busy") && timeout.Elapsed < TimeSpan.FromMinutes(3))
                    {
                        Application.DoEvents(); Thread.Sleep(10);
                    }
                    JournalProject saved = (JournalProject)FcsAdapter.Field(window, "project");
                    Check(!(bool)FcsAdapter.Field(window, "busy") && saved.EntryCount == 1 && saved.PendingCount == 1 &&
                        File.ReadAllLines(saved.SessionPath).Length == 2 && !File.Exists(saved.JournalPath),
                        "the actual automatic tick flushes a changed session delta before compilation (entries=" +
                        saved.EntryCount + ", visible=" + window.Visible + ", state=" + window.WindowState +
                        ", parent=" + host.WindowState + ", closing=" + FcsAdapter.Field(window, "closing") +
                        ", due=" + schedule.NextReview + ", now=" + clock.ElapsedMilliseconds + ", modal=" +
                        String.Join(",", Application.OpenForms.Cast<Form>().Where(x => x.Modal).Select(x => x.Name)) + ")");
                    Check(saved.ResumeTracker().Export(adapter.Capture()).Changes.Count == 0 &&
                        saved.Fingerprint == adapter.Capture().Fingerprint() && TrackingStorage.HashFile(path) == anchorHash,
                        "automatic review advances only to the successfully saved current state");
                    CheckRecordingRangeDialog(saved, adapter);
                    long compilationDeadline = schedule.NextCompilation;
                    RunWindow(window, "Review");
                    Check(schedule.NextCompilation == compilationDeadline, "recording does not postpone compilation");
                    schedule.Reset(clock.ElapsedMilliseconds - JournalSchedule.CompilationMilliseconds, 30);
                    RunWindow(window, "AutomaticTick");
                    Check(saved.PendingCount == 0 && File.ReadAllLines(saved.JournalPath).Length == 1 &&
                        File.Exists(saved.SessionPath), "thirty-minute automatic compilation retains the original session");
                    string compiledHash = TrackingStorage.HashFile(saved.JournalPath);
                    RunWindow(window, "Compile");
                    Check(TrackingStorage.HashFile(saved.JournalPath) == compiledHash, "explicit compilation is available and idempotent");
                    Put(window, "busy", true);
                    FcsAdapter.Call(timer, "OnTick", EventArgs.Empty);
                    Check(saved.EntryCount == 1,
                        "automatic ticks do not overlap an active operation");
                    Put(window, "busy", false);
                    Put(window, "automaticPaused", true);
                    FcsAdapter.Call(window, "ConfigureTimer");
                    Check(!timer.Enabled, "automatic scheduling remains suspended after an error (thread=" +
                        Thread.CurrentThread.ManagedThreadId + ", paused=" + FcsAdapter.Field(window, "automaticPaused") +
                        ", context=" + (SynchronizationContext.Current == null ? "null" : SynchronizationContext.Current.GetType().Name) + ")");
                    saved = CheckMultipleRecordedTicks(window, adapter, path);
                    string preserved = File.ReadAllText(saved.SessionPath);
                    string fingerprint = saved.Fingerprint;
                    File.WriteAllText(saved.SessionPath, "external edit");
                    Put(header, "Author", "A later unsaved fixture edit");
                    bool conflict = false;
                    try { RunWindow(window, "Review"); }
                    catch (IOException) { conflict = true; }
                    Check(conflict && saved.Fingerprint == fingerprint && File.ReadAllText(saved.SessionPath) == "external edit",
                        "failed recordings preserve both the external session content and the previous baseline");
                    File.WriteAllText(saved.SessionPath, preserved);
                    window.Hide(); window.MdiParent = editor;
                }
            }
            finally { Put(header, "Author", originalAuthor); }
            string custom = Path.Combine(directory, "custom-json");
            Directory.CreateDirectory(custom);
            settings = (PluginSettings)FcsAdapter.Field(window, "settings");
            settings.ExportDirectory = custom;
            Check((string)FcsAdapter.Call(window, "ExportDirectory") == custom, "custom JSON default is configurable without moving the project");
            settings.ReviewSeconds = 30;
            FcsAdapter.Call(window, "ConfigureTimer");
            window.Dispose();
            Check(!timer.Enabled, "disposing the window stops automatic scheduling");
        }
    }

    private static void RunWindow(ScribeWindow window, string method, params object[] args)
    {
        AwaitUi((Task)FcsAdapter.Call(window, "RunBusy", new Func<Task>(() =>
            (Task)FcsAdapter.Call(window, method, args))));
    }

    private static void CheckRecordingRangeDialog(JournalProject project, FcsAdapter adapter)
    {
        Snapshot pending = adapter.Capture();
        pending.Records[0].Name += " Unrecorded preview fixture";
        string head = project.HeadId, hash = TrackingStorage.HashFile(project.SessionPath);
        var choices = project.ExportRecordings(pending);
        Check(choices.Count == 2 && choices[0].Packet.Changes.Count == 0 && choices[1].Kind == "pending",
            "actual recorded context and pending gameplay are distinct export choices");
        using (var dialog = new RecordingRangeDialog(choices))
        {
            var first = (ComboBox)FcsAdapter.Field(dialog, "first");
            var last = (ComboBox)FcsAdapter.Field(dialog, "last");
            var export = (Button)FcsAdapter.Field(dialog, "export");
            var preview = (TextBox)FcsAdapter.Field(dialog, "preview");
            Check(dialog.First == 0 && dialog.Last == 1 && export.Enabled &&
                first.Items[1].ToString().Contains("UNRECORDED") && preview.Text.Contains(choices[1].EntryId + ":change-"),
                "range dialog defaults to all entries with explicit pending labels and qualified evidence preview");
            last.SelectedIndex = 0;
            Check(!export.Enabled && !preview.Text.Contains(":change-") &&
                ((Label)FcsAdapter.Field(dialog, "summary")).Text.Contains("Context-only"),
                "context-only selections cannot export record documentation");
            first.SelectedIndex = 1;
            Check(!export.Enabled && preview.Text.IndexOf(":change-", StringComparison.Ordinal) < 0,
                "reversed dialog ranges are visibly invalid and cannot export");
            last.SelectedIndex = 1;
            Check(export.Enabled && dialog.First == dialog.Last && preview.Text.Contains("Unrecorded preview fixture"),
                "an inclusive single pending recording can be selected and inspected");
            using (var cancel = new System.Windows.Forms.Timer { Interval = 50 })
            {
                cancel.Tick += delegate { cancel.Stop(); ((Button)dialog.CancelButton).PerformClick(); };
                cancel.Start();
                Check(dialog.ShowDialog() == DialogResult.Cancel, "cancelling the modal recording selector does not approve an export");
            }
        }
        Check(project.HeadId == head && TrackingStorage.HashFile(project.SessionPath) == hash,
            "dialog selection and cancellation never record, compile or alter tracking history");
        Packet packet = RangeExport.Create(project.ProjectId, choices, 1, 1, pending);
        NoteDraft draft = Json.Read<NoteDraft>(Json.Write(packet.ResponseTemplate));
        foreach (NoteEntry entry in draft.Entries) entry.Text = "Range fixture.";
        Reject<InvalidOperationException>(() => adapter.Apply(packet, draft),
            "actual FCS application rejects range evidence not matching the current editor state");
    }

    private static JournalProject CheckMultipleRecordedTicks(ScribeWindow window, FcsAdapter adapter, string path)
    {
        var timer = (System.Windows.Forms.Timer)FcsAdapter.Field(window, "timer");
        var schedule = (JournalSchedule)FcsAdapter.Field(window, "schedule");
        var clock = (System.Diagnostics.Stopwatch)FcsAdapter.Field(window, "clock");
        object item = ((IDictionary)FcsAdapter.Field(adapter, "items"))[adapter.Capture().Records[0].Id];
        string name = (string)FcsAdapter.Property(item, "Name");
        try
        {
            for (int i = 0; i < 2; i++)
            {
                Set(item, "Name", name + " Recorded tick " + i);
                schedule.Reset(clock.ElapsedMilliseconds - 30000, 30);
                Put(window, "automaticPaused", false);
                FcsAdapter.Call(window, "ConfigureTimer");
                FcsAdapter.Call(timer, "OnTick", EventArgs.Empty);
                var timeout = System.Diagnostics.Stopwatch.StartNew();
                while ((bool)FcsAdapter.Field(window, "busy") && timeout.Elapsed < TimeSpan.FromMinutes(3))
                { Application.DoEvents(); Thread.Sleep(10); }
                Check(!(bool)FcsAdapter.Field(window, "busy") && !(bool)FcsAdapter.Field(window, "automaticPaused") &&
                    ((JournalProject)FcsAdapter.Field(window, "project")).EntryCount == i + 2,
                    "successive automatic gameplay tick is durable: " + i);
            }
            RunWindow(window, "ResumePath", path);
            var project = (JournalProject)FcsAdapter.Field(window, "project");
            var choices = project.ExportRecordings(adapter.Capture());
            Packet packet = RangeExport.Create(project.ProjectId, choices, 0, choices.Count - 1, adapter.Capture());
            Check(choices.Count == 3 && packet.Changes.Count == 4 &&
                choices.Skip(1).All(x => x.Packet.Changes.Any(change => change.Layer == "active") &&
                    x.Packet.Changes.Any(change => change.Layer == "effective")) && project.EntryCount == 3,
                "multiple actual automatic ticks remain accumulated after starting a new session");
            Put(window, "automaticPaused", true);
            FcsAdapter.Call(window, "ConfigureTimer");
            return project;
        }
        finally { Set(item, "Name", name); }
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
        string projectPath = Path.Combine(Directory.GetCurrentDirectory(), "workshop-failures.json");
        JournalProject project = null;
        try
        {
            WorkshopMetadata missing = adapter.CaptureWorkshop();
            Check(missing.SteamId == null && missing.ModName == null && missing.VisibilityCode == null &&
                missing.ChangeNotes == null && missing.Warnings.Count > 0, "missing Workshop cache is explicitly unknown, not fabricated defaults");
            Check(missing.Preview.Status == "missing" && missing.Preview.Sha256 == null, "missing preview is explicit");
            Check(missing.CaptureStatus == null && missing.Issues == null && adapter.Capture().Records.Count > 0,
                "absent optional data stays explicitly missing without blocking ordinary model capture");
            project = JournalProject.Create(projectPath, adapter.Capture());
            project.BeginSession();
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
            CheckPartialWorkshop(adapter, project, "dialog", "upload-in-progress");
            Reject<InvalidOperationException>(() => adapter.ArchivePreview(archive, null), "archiving still waits for an active upload");
            Check(adapter.CaptureWorkshop().ModName == null && adapter.CaptureWorkshop().Preview.Status == "unavailable",
                "busy staged values and preview are unknown, not stale dialog values");
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
                CheckPartialWorkshop(adapter, project, "dialog", "refresh-in-progress");
                Reject<InvalidOperationException>(() => adapter.ArchivePreview(archive, null), "archiving still waits for asynchronous refresh");
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
            CheckPartialWorkshop(adapter, project, "preview", "unsupported");
            WorkshopMetadata invalidPreview = adapter.CaptureWorkshop();
            Check(invalidPreview.ModName == live.ModName && invalidPreview.Tags.SequenceEqual(live.Tags) &&
                invalidPreview.Preview.Status == "unavailable" && !String.IsNullOrWhiteSpace(invalidPreview.Preview.UnavailableReason),
                "invalid previews do not erase readable Workshop metadata and expose their reason");
            Reject<NotSupportedException>(() => adapter.ArchivePreview(archive + ".bmp", null),
                "unsupported previews still cannot be archived");
            var boundaryImage = new byte[1024 * 1024];
            Array.Copy(source, boundaryImage, source.Length);
            File.WriteAllBytes(preview, boundaryImage);
            Check(adapter.CaptureWorkshop().Preview.ByteLength == 1024 * 1024, "FCS's exact 1 MB preview limit is accepted");
            using (var oversized = new FileStream(preview, FileMode.Create)) oversized.SetLength(1024 * 1024 + 1);
            CheckPartialWorkshop(adapter, project, "preview", "unsupported");
            Reject<NotSupportedException>(() => adapter.ArchivePreview(archive, null),
                "oversized previews remain strict archive failures");
            File.WriteAllText(preview, "not an encoded image");
            CheckPartialWorkshop(adapter, project, "preview", "invalid-data");
            Reject<InvalidDataException>(() => adapter.ArchivePreview(archive, null),
                "malformed image decode errors are explicit archive failures");
            using (var locked = new FileStream(preview, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                CheckPartialWorkshop(adapter, project, "preview", "read-failed");
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
            CheckPartialWorkshop(adapter, project, "cache", "invalid-data");
            using (var image = new Bitmap(2, 2)) image.Save(preview, ImageFormat.Png);
            WorkshopMetadata badCache = adapter.CaptureWorkshop();
            Check(badCache.ModName == null && badCache.Preview.Status == "available" &&
                badCache.Preview.Sha256 != null, "cache failure does not discard an independently readable preview");
            string independentArchive = archive + ".cache.png";
            try
            {
                adapter.ArchivePreview(independentArchive, badCache.Preview.Sha256);
                Check(File.ReadAllBytes(independentArchive).SequenceEqual(File.ReadAllBytes(preview)),
                    "a valid preview can still be archived while its optional metadata cache is invalid");
            }
            finally { if (File.Exists(independentArchive)) File.Delete(independentArchive); }
            File.WriteAllText(preview, "invalid image while cache is also corrupt");
            CheckPartialWorkshop(adapter, project, "cache", "invalid-data");
            Check(adapter.CaptureWorkshop().Issues.Count == 2, "independent cache and preview failures are both reported");
            File.Delete(preview);
            File.WriteAllText(cache, "<!DOCTYPE ModData [<!ENTITY x SYSTEM 'file:///not-read'>]><ModData><title>&x;</title></ModData>");
            CheckPartialWorkshop(adapter, project, "cache", "invalid-data");
            File.WriteAllText(cache, "<WrongRoot />");
            CheckPartialWorkshop(adapter, project, "cache", "invalid-data");
            File.WriteAllText(cache, "<ModData><id>invalid Steam ID</id></ModData>");
            CheckPartialWorkshop(adapter, project, "cache", "invalid-data");
            File.WriteAllText(cache, "<ModData><mod>different-mod</mod></ModData>");
            CheckPartialWorkshop(adapter, project, "cache", "invalid-data");
            using (var locked = new FileStream(cache, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                CheckPartialWorkshop(adapter, project, "cache", "read-failed");
            string savedFingerprint = project.Fingerprint;
            int recordedCount = project.EntryCount;
            Check(project.Compile() == recordedCount, "all recordings made during optional Workshop failures compile successfully");
            project.Dispose(); project = null;
            using (var reopened = JournalProject.Open(projectPath))
            {
                var choices = reopened.ExportRecordings(reopened.State);
                Packet packet = RangeExport.Create(reopened.ProjectId, choices, 0, choices.Count - 1, reopened.State);
                Check(reopened.Fingerprint == savedFingerprint && reopened.State.Workshop.CaptureStatus == "partial" &&
                    packet.Recordings.Count == recordedCount && packet.Recordings.All(x => x.Packet.Context.Workshop.Issues.Count > 0),
                    "partial availability, reasons and gameplay evidence replay and export across restart");
            }
            File.WriteAllText(cache, "<ModData>");
            CheckWorkshopFailureWindow(adapter, editor, cache, cacheBytes);
            bool originalMergeMode = (bool)FcsAdapter.Field(editor, "mergeModsMode");
            try
            {
                Put(editor, "mergeModsMode", true);
                Reject<NotSupportedException>(() => adapter.Capture(), "optional recovery does not bypass unsupported editor modes");
            }
            finally { Put(editor, "mergeModsMode", originalMergeMode); }
        }
        finally
        {
            if (project != null) project.Dispose();
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

    private static void CheckPartialWorkshop(FcsAdapter adapter, JournalProject project, string component, string code)
    {
        IDictionary items = (IDictionary)FcsAdapter.Field(adapter, "items");
        object item = items.Values.Cast<object>().First();
        string name = (string)FcsAdapter.Property(item, "Name");
        string id = (string)FcsAdapter.Property(item, "stringID");
        object header = FcsAdapter.Field(FcsAdapter.Field(adapter, "gameData"), "header");
        try
        {
            Set(item, "Name", name + " During " + component + " failure " + project.EntryCount);
            Snapshot captured = adapter.Capture();
            WorkshopMetadata metadata = captured.Workshop;
            Check(metadata.CaptureStatus == "partial" && metadata.Issues.Any(x => x.Component == component && x.Code == code) &&
                captured.Warnings.Any(x => x.Contains("[" + code + "]") && x.Contains("unavailable")),
                component + "/" + code + " is explicit in metadata and gameplay-capture warnings");
            Check(metadata.Author == (string)FcsAdapter.Field(header, "Author") &&
                metadata.Version == (int)FcsAdapter.Field(header, "Version") &&
                metadata.Description == (string)FcsAdapter.Field(header, "Description") &&
                captured.Records.Any(x => x.Id == id && x.Name != name),
                component + "/" + code + " retains known header fields and unsaved gameplay");
            Packet packet = project.Record(captured);
            Check(packet.Changes.Any(x => x.RecordId == id && x.Path == "/name") && project.Fingerprint == captured.Fingerprint(),
                component + "/" + code + " does not block durable gameplay recording");
            int count = project.EntryCount;
            project.Record(adapter.Capture());
            Check(project.EntryCount == count, component + "/" + code + " repeats with a stable availability fingerprint");
            NoteDraft draft = Json.Read<NoteDraft>(Json.Write(packet.ResponseTemplate));
            foreach (NoteEntry entry in draft.Entries) entry.Text = "Recorded fixture name changes with explicit Workshop warnings.";
            DraftValidation.Check(packet, draft, captured, target => "");
            Check(true, component + "/" + code + " still permits validation against the known captured state");
        }
        finally { Set(item, "Name", name); }
    }

    private static void CheckWorkshopFailureWindow(FcsAdapter adapter, Form editor, string cache, byte[] healthyCache)
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "window-partial-workshop.json");
        object item = ((IDictionary)FcsAdapter.Field(adapter, "items")).Values.Cast<object>().First();
        string name = (string)FcsAdapter.Property(item, "Name");
        using (var host = new Form { IsMdiContainer = true, ShowInTaskbar = false, Opacity = 0 })
        using (var window = new ScribeWindow(adapter, editor, Path.Combine(Directory.GetCurrentDirectory(), "partial-workshop-settings.json")))
        {
            try
            {
                window.MdiParent = host; host.Show(); window.Show();
                RunWindow(window, "StartPath", path);
                var project = (JournalProject)FcsAdapter.Field(window, "project");
                Check(project.State.Workshop.CaptureStatus == "partial" && !(bool)FcsAdapter.Field(window, "automaticPaused") &&
                    ((Label)FcsAdapter.Field(window, "status")).Text.Contains("PARTIAL Workshop") &&
                    ((TextBox)FcsAdapter.Field(window, "review")).Text.Contains("cache unavailable"),
                    "new tracking starts with visible availability warnings rather than aborting");
                Set(item, "Name", name + " Automatic partial Workshop fixture");
                var settings = (PluginSettings)FcsAdapter.Field(window, "settings");
                var schedule = (JournalSchedule)FcsAdapter.Field(window, "schedule");
                var clock = (System.Diagnostics.Stopwatch)FcsAdapter.Field(window, "clock");
                var timer = (System.Windows.Forms.Timer)FcsAdapter.Field(window, "timer");
                settings.ReviewSeconds = 30;
                schedule.Reset(clock.ElapsedMilliseconds - 30000, 30);
                FcsAdapter.Call(window, "ConfigureTimer");
                FcsAdapter.Call(timer, "OnTick", EventArgs.Empty);
                var timeout = System.Diagnostics.Stopwatch.StartNew();
                while ((bool)FcsAdapter.Field(window, "busy") && timeout.Elapsed < TimeSpan.FromMinutes(3))
                { Application.DoEvents(); Thread.Sleep(10); }
                Check(!(bool)FcsAdapter.Field(window, "busy") && !(bool)FcsAdapter.Field(window, "automaticPaused") &&
                    timer.Enabled && project.EntryCount == 1 && project.State.Workshop.CaptureStatus == "partial",
                    "an actual automatic tick durably records gameplay without pausing for corrupt optional cache data");
                Snapshot partial = adapter.Capture();
                var choices = project.ExportRecordings(partial);
                Packet packet = RangeExport.Create(project.ProjectId, choices, 0, choices.Count - 1, partial);
                NoteDraft draft = Json.Read<NoteDraft>(Json.Write(packet.ResponseTemplate));
                foreach (NoteEntry entry in draft.Entries) entry.Text = "Partial Workshop fixture.";
                File.WriteAllBytes(cache, healthyCache);
                Snapshot recovered = adapter.Capture();
                Check(recovered.Workshop.CaptureStatus == null && recovered.Workshop.Issues == null &&
                    recovered.Workshop.ModName == "Cached Workshop title", "readable metadata recovers automatically without restarting tracking");
                Reject<InvalidOperationException>(() => DraftValidation.Check(packet, draft, recovered, adapter.Notes),
                    "recovered Workshop availability stales the actual partial-capture range draft");
                RunWindow(window, "Review");
                Check(project.EntryCount == 2 && project.State.Workshop.CaptureStatus == null &&
                    !(bool)FcsAdapter.Field(window, "automaticPaused") &&
                    !((Label)FcsAdapter.Field(window, "status")).Text.Contains("PARTIAL Workshop"),
                    "availability recovery is recorded as context without fabricating gameplay or retaining an obsolete failure status");
                Check(File.ReadAllBytes(cache).SequenceEqual(healthyCache),
                    "tracking does not rewrite a repaired Workshop cache");
                string fingerprint = project.Fingerprint;
                FcsAdapter.Call(window, "SetActionFailure", false);
                Check(!(bool)FcsAdapter.Field(window, "automaticPaused") && timer.Enabled &&
                    project.Fingerprint == fingerprint && ((Label)FcsAdapter.Field(window, "status")).Text.Contains("Workshop action failed"),
                    "Workshop-only action failures retain active scheduling and report failure without changing tracking");
                FcsAdapter.Call(window, "SetActionFailure", true);
                Check((bool)FcsAdapter.Field(window, "automaticPaused") && !timer.Enabled,
                    "ordinary model/journal action failures still pause automatic recording");
                FcsAdapter.Call(window, "SetActionFailure", false);
                Check((bool)FcsAdapter.Field(window, "automaticPaused") && !timer.Enabled,
                    "a later Workshop-only failure does not clear an existing automatic pause");
            }
            finally { Set(item, "Name", name); }
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
