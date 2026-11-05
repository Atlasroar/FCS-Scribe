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
                    try { result = Run(args.Contains("--settings-recovery"), args.Contains("--schedule"),
                        args.Contains("--exports"), args.Contains("--tracking-recovery"), args.Contains("--sampling"),
                        args.Contains("--performance"), args.Contains("--notes-safety") || args.Contains("--notes-reopen"),
                        args.Contains("--notes-reopen"), args.Contains("--compatibility"), args.Contains("--workshop-provenance"),
                        args.Contains("--mod-work")); }
                    finally { host.Close(); }
                }));
            };
            Application.Run(host);
        }
        return result;
    }

    private static int Run(bool settingsOnly, bool scheduleOnly, bool exportsOnly, bool recoveryOnly, bool samplingOnly,
        bool performanceOnly, bool notesOnly, bool notesReopen, bool compatibilityOnly, bool workshopOnly, bool modWorkOnly)
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
            bool stacked = (performanceOnly || compatibilityOnly) && Directory.Exists(Path.Combine(directory, "dependencies"));
            ((IList)FcsAdapter.Field(editor, "activeMods")).Add(stacked ? "gamedata.base" : "fixture.mod");
            ((IList)FcsAdapter.Field(editor, "activeMods")).Add("scribe-test.mod");
            Check((bool)FcsAdapter.Call(data, "load", stacked ? Path.Combine(directory, "dependencies", "gamedata.base") :
                Path.Combine(directory, "fixture.mod"),
                Enum.Parse(mode, "BASE"), false, false), "load representative mod copy as dependency");
            if (stacked)
                foreach (string name in new[] { "Newwworld.mod", "Dialogue.mod", "rebirth.mod" })
                {
                    Check((bool)FcsAdapter.Call(data, "load", Path.Combine(directory, "dependencies", name),
                        Enum.Parse(mode, "BASE"), false, false), "load additional copied vanilla dependency " + name);
                    ((IList)FcsAdapter.Field(editor, "activeMods")).Insert(
                        ((IList)FcsAdapter.Field(editor, "activeMods")).Count - 1, name);
                }
            if (!notesReopen) File.Copy(Path.Combine(directory, "fixture.mod"), output, true);
            Check((bool)FcsAdapter.Call(data, "load", output, Enum.Parse(mode, "ACTIVE"), false, false),
                "load disposable active mod");
            FcsAdapter.Call(data, "resolveAllReferences");
            Set(FcsAdapter.Field(editor, "modInfo"), "Data", data);
            string originalModBytes = Convert.ToBase64String(File.ReadAllBytes(output));
            object todo = FcsAdapter.Field(editor, "todoList");
            FcsAdapter.Call(todo, "LoadAll");
            var adapter = new FcsAdapter(assembly, editor);
            if (modWorkOnly)
            {
                CheckScribeWindow(adapter, editor);
                CheckModWork(adapter, editor, data, assembly, output);
                Console.WriteLine("PASS: " + passed + " mod work/menu integration checks");
                return 0;
            }
            if (compatibilityOnly)
            {
                CheckCompatibility(adapter, editor, data, nav, assembly, output);
                Console.WriteLine("PASS: " + passed + " compatibility/provenance integration checks");
                return 0;
            }
            if (workshopOnly)
            {
                CheckWorkshop(adapter, editor, data, nav, assembly);
                Console.WriteLine("PASS: " + passed + " Workshop provenance/isolation integration checks");
                return 0;
            }
            if (notesOnly)
            {
                if (notesReopen) CheckNotesReopened(adapter, output);
                else CheckNotesSafetyWindow(adapter, editor, assembly, output);
                Console.WriteLine("PASS: " + passed + (notesReopen ? " cold-process notes reopen checks" : " notes safety integration checks"));
                return 0;
            }
            if (performanceOnly)
            {
                PerformanceTests.Native(adapter.Capture, directory, Environment.GetEnvironmentVariable("FCS_SCRIBE_PROFILE_SOURCE"),
                    Int32.Parse(Environment.GetEnvironmentVariable("FCS_SCRIBE_PROFILE_ITERATIONS"),
                        System.Globalization.CultureInfo.InvariantCulture));
                return 0;
            }
            if (samplingOnly)
            {
                CheckSamplingWindow(adapter, editor, assembly, output);
                Console.WriteLine("PASS: " + passed + " sampling boundary integration checks");
                return 0;
            }
            if (recoveryOnly)
            {
                CheckScribeWindow(adapter, editor);
                CheckTrackingRecoveryWindow(adapter, editor, assembly, output);
                CheckPortabilityWindow(new FcsAdapter(assembly, editor), editor, assembly, output);
                Console.WriteLine("PASS: " + passed + " tracking recovery boundary integration checks");
                return 0;
            }
            if (exportsOnly)
            {
                CheckExportWindow(adapter, editor);
                Console.WriteLine("PASS: " + passed + " export protection integration checks");
                return 0;
            }
            if (scheduleOnly)
            {
                CheckSchedulingWindow(adapter, editor);
                Console.WriteLine("PASS: " + passed + " compilation scheduling integration checks");
                return 0;
            }
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
            var help = (TextBox)FcsAdapter.Field(window, "help");
            var status = (Label)FcsAdapter.Field(window, "status");
            string originalStatus = status.Text;
            string originalInformation = help.Text;
            string trackingNotice = TrackingChoiceDialog.BoundaryText;
            Check(help.Text.Contains(trackingNotice) &&
                trackingNotice.Contains("UNSAVED") && trackingNotice.Contains("NOT verified"),
                "default bottom information distinguishes captured editor state from saved mod/notes state");
            using (var choice = new TrackingChoiceDialog())
                Check(choice.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Label>()
                    .Any(x => x.Text == trackingNotice), "the Start/Resume chooser shares the boundary guidance");
            string storageNotice = TrackingChoiceDialog.StorageText;
            Check(help.Text.Contains(storageNotice) &&
                storageNotice.Contains("project.json") && storageNotice.Contains("journal.jsonl") &&
                storageNotice.Contains("sessions") && storageNotice.Contains("full loaded/dependency data") &&
                storageNotice.Contains("does not inspect or block"),
                "default bottom storage guidance retains the complete private-set warning");
            Check(window.Controls.OfType<Label>().SequenceEqual(new[] { status }) &&
                help.Multiline && help.ReadOnly && help.WordWrap && help.ScrollBars == ScrollBars.Vertical,
                "one scrollable read-only information box replaces the separate top warning labels");
            using (var choice = new TrackingChoiceDialog())
            {
                Check(choice.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Label>()
                    .Any(x => x.Text == storageNotice), "the chooser shares the same storage/distribution warning");
                choice.ShowInTaskbar = false; choice.Opacity = 0; choice.Show(); choice.PerformLayout();
                var layout = choice.Controls.OfType<FlowLayoutPanel>().Single();
                Check(layout.Controls.OfType<FlowLayoutPanel>().Single().Bottom <= layout.ClientSize.Height - layout.Padding.Bottom,
                    "the tracking chooser keeps its buttons visible below both notices at its default size");
            }
            var menus = actions.Items.Cast<ToolStripMenuItem>().ToList();
            Check(menus.Select(x => x.Text.Replace("&", "")).SequenceEqual(
                new[] { "Core", "Workshop", "Draft", "Workspace", "Apply approved notes", "Settings" }),
                "Workspace has its own top-level dropdown without losing native annotation approval");
            Check(window.MainMenuStrip == actions && !actions.AllowMerge && actions.TabStop,
                "menus stay inside Scribe and support keyboard access without merging into FCS");
            string[][] groups = {
                new[] { "Start tracking", "Save Changes", "Compile journal", "Browse history", "Export AI packet", "Export Markdown" },
                new[] { "Export Workshop info", "Archive preview image" },
                new[] { "Import draft", "Paste draft", "Export reviewed Markdown" },
                new[] { "Save workspace", "Open workspace" }
            };
            for (int i = 0; i < groups.Length; i++)
                Check(menus[i].DropDownItems.Cast<ToolStripMenuItem>().Select(x => x.Text.Replace("&", "")).SequenceEqual(groups[i]),
                    menus[i].Text + " contains the requested actions in order");
            var commands = menus.SelectMany(x => x.HasDropDownItems ?
                x.DropDownItems.Cast<ToolStripMenuItem>() : new[] { x }).ToList();
            Check(commands.Count == 15, "all original/workspace/Markdown actions plus read-only history remain available");
            foreach (ToolStripMenuItem item in menus.Take(4).Concat(commands))
            {
                typeof(ToolStripItem).GetMethod("HandleMouseEnter", Flags).Invoke(item, new object[] { EventArgs.Empty });
                Check(!String.IsNullOrEmpty(item.AccessibleDescription) && help.Text == item.AccessibleDescription &&
                    status.Text == originalStatus, item.Text + " hover explains action without erasing operation status");
                typeof(ToolStripItem).GetMethod("HandleMouseLeave", Flags).Invoke(item, new object[] { EventArgs.Empty });
                Check(help.Text == originalInformation, item.Text + " hover exit restores all default warnings");
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
                    FcsAdapter.Call(actions, "OnKeyDown", new KeyEventArgs(Keys.Right));
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
                    Check(help.Text == originalInformation, "closing " + menu.Text + " restores default information");
                }
                FcsAdapter.Call(actions, "OnMenuDeactivate", EventArgs.Empty);
                window.Width = 320; window.PerformLayout(); actions.PerformLayout();
                Check(help.Width <= window.ClientSize.Width && help.Text == originalInformation &&
                    help.Height == 100 && help.ScrollBars == ScrollBars.Vertical,
                    "narrow windows retain all warnings with scrolling instead of growing the header");
                Check(actions.OverflowButton.Visible && menus.Last().Placement == ToolStripItemPlacement.Overflow,
                    "narrow windows retain access to actions through the overflow menu");
                actions.OverflowButton.ShowDropDown();
                menus.Last().Select(); actions.OverflowButton.DropDown.Refresh();
                Check(help.Text == menus.Last().AccessibleDescription,
                    "keyboard selection in the overflow menu retains contextual help");
                actions.OverflowButton.HideDropDown();
                actions.Refresh();
                Check(help.Text == originalInformation, "overflow close and later selected-item paints do not leave stale help");
                typeof(ToolStripItem).GetMethod("HandleMouseEnter", Flags).Invoke(menus[0], new object[] { EventArgs.Empty });
                FcsAdapter.Call(actions, "OnMenuDeactivate", EventArgs.Empty);
                actions.Refresh();
                Check(help.Text == originalInformation && status.Text == originalStatus,
                    "menu deactivation restores warnings without erasing operation status");
                window.Hide(); window.MdiParent = editor;
            }

            Check(window.Controls.OfType<TabControl>().Single().TabPages.Cast<TabPage>().Any(x => x.Text == "Workshop info"),
                "Workshop context has a read-only review tab");
            window.PerformLayout();
            Check(help.Dock == DockStyle.Bottom && help.Bottom <= status.Top,
                "shared warnings/help remain at the bottom above distinct operation status");
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

    private static void CheckExportWindow(FcsAdapter adapter, Form editor)
    {
        string directory = Path.Combine(Directory.GetCurrentDirectory(), "mods", "export-protection");
        Directory.CreateDirectory(directory);
        Snapshot current = adapter.Capture(), tiny = current.Context();
        tiny.Records.Add(current.Records[0]);
        string inactive = Path.Combine(directory, "inactive.json");
        using (var project = JournalProject.Create(inactive, tiny)) { project.BeginSession(); }
        string standalone = Path.Combine(directory, "standalone.json");
        TrackingStorage.Save(standalone, TrackingDocument.Create(tiny, null), null);
        string[] targets = { inactive, standalone, Path.Combine(inactive + ".scribe", "project.json"),
            Path.Combine(inactive + ".scribe", "project.json.bak"),
            Path.Combine(inactive + ".scribe", "sessions", "new.json"),
            Path.Combine(inactive + ".scribe", "recovery", "new.json") };
        var originals = Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(x => x, TrackingStorage.HashFile);
        using (var window = new ScribeWindow(adapter, editor, Path.Combine(directory, "settings.json")))
        {
            var workshop = new WorkshopExport { Workshop = current.Workshop };
            foreach (string target in targets)
            {
                Reject<InvalidOperationException>(() => CallWindowAction(window, "SaveWorkshopPath", target, workshop),
                    "Workshop export rejects inactive tracking storage with no open project");
                bool rejected = false;
                try { RunWindow(window, "SaveWorkspacePath", target); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "draft workspace save uses the same inactive-storage guard");
            }
            Packet packet = new Tracker(current).Export(current);
            Reject<InvalidOperationException>(() => CallWindowAction(window, "SavePacketPath", inactive, packet),
                "AI export write path rejects another project's anchor");
            bool markdownRejected = false;
            try { RunWindow(window, "SaveMarkdownPath", Path.Combine(inactive + ".scribe", "sessions", "new.md"), packet, null, 0L); }
            catch (InvalidOperationException) { markdownRejected = true; }
            Check(markdownRejected, "Markdown window export rejects inactive reserved storage");
            Check(originals.All(x => TrackingStorage.HashFile(x.Key) == x.Value) &&
                Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length == originals.Count,
                "rejected actual window writes preserve the complete inactive project");
            string imagePath = Path.Combine(directory, "preview.png");
            using (var image = new Bitmap(1, 1)) image.Save(imagePath, ImageFormat.Png);
            WorkshopPreview preview = WorkshopPreview.Read(imagePath);
            string imageTarget = Path.Combine(inactive + ".scribe", "sessions", "preview.png");
            Reject<InvalidOperationException>(() => preview.Save(imageTarget, directory),
                "new preview archives cannot create files in another project's storage");
            Check(!File.Exists(imageTarget), "rejected preview archive creates no reserved file");
            string valid = Path.Combine(directory, "ordinary.json");
            CallWindowAction(window, "SaveWorkshopPath", valid, workshop);
            CallWindowAction(window, "SavePacketPath", valid, packet);
            Check(TrackingStorage.Load(valid).Legacy.BatchId == packet.BatchId,
                "a valid existing Workshop export can be replaced by an AI packet");
            CallWindowAction(window, "SaveWorkshopPath", valid, workshop);
            Check(Json.Read<WorkshopExport>(File.ReadAllText(valid)).Workshop.Source == workshop.Workshop.Source,
                "a valid existing AI packet can be replaced by Workshop JSON");
            File.WriteAllText(valid, "{ damaged JSON");
            Reject<InvalidDataException>(() => CallWindowAction(window, "SaveWorkshopPath", valid, workshop),
                "actual Workshop write path refuses damaged JSON");
            Check(File.ReadAllText(valid) == "{ damaged JSON", "damaged destination remains untouched");
            string markdown = Path.Combine(directory, "ordinary.md");
            RunWindow(window, "SaveMarkdownPath", markdown, packet, null, 0L);
            Check(File.ReadAllText(markdown).Contains("No record-change evidence") &&
                FcsAdapter.Field(window, "exported") == null && FcsAdapter.Field(window, "project") == null,
                "factual Markdown exports without adopting a draft packet or starting tracking");
            File.AppendAllText(markdown, "\nmanual edit");
            string markdownHash = TrackingStorage.HashFile(markdown);
            markdownRejected = false;
            try { RunWindow(window, "SaveMarkdownPath", markdown, packet, null, 0L); }
            catch (InvalidDataException) { markdownRejected = true; }
            Check(markdownRejected && TrackingStorage.HashFile(markdown) == markdownHash,
                "actual Markdown window path preserves edited generated files");
            CheckReviewedMarkdownWindow(adapter, editor, directory, window);
            string workspace = Path.Combine(directory, "workspace.json");
            RunWindow(window, "SaveWorkspacePath", workspace);
            File.Copy(standalone, workspace + ".bak");
            string workspaceHash = TrackingStorage.HashFile(workspace), backupHash = TrackingStorage.HashFile(workspace + ".bak");
            bool backupRejected = false;
            try { RunWindow(window, "SaveWorkspacePath", workspace); }
            catch (InvalidOperationException) { backupRejected = true; }
            Check(backupRejected && TrackingStorage.HashFile(workspace) == workspaceHash &&
                TrackingStorage.HashFile(workspace + ".bak") == backupHash,
                "workspace rotation cannot overwrite tracking data stored at its backup path");
            RunWindow(window, "StartPath", Path.Combine(directory, "active.json"));
            string active = ((JournalProject)FcsAdapter.Field(window, "project")).PathName;
            Reject<InvalidOperationException>(() => CallWindowAction(window, "SaveWorkshopPath", active, workshop),
                "current project remains protected after Start");
            var openProject = (JournalProject)FcsAdapter.Field(window, "project");
            string head = openProject.HeadId, sessionHash = TrackingStorage.HashFile(openProject.SessionPath);
            RunWindow(window, "SaveMarkdownPath", Path.Combine(directory, "tracked.md"), new Tracker(adapter.Capture()).Export(adapter.Capture()), null, 0L);
            Check(openProject.HeadId == head && TrackingStorage.HashFile(openProject.SessionPath) == sessionHash &&
                !File.Exists(Path.Combine(active + ".scribe", "journal.jsonl")),
                "Markdown export with tracking open neither records nor compiles history");
            CheckHistoryWindow(adapter, editor, directory);
        }
    }

    private static void CheckHistoryWindow(FcsAdapter adapter, Form editor, string directory)
    {
        // Construct the modal owner in its shown MDI host, rather than reparenting a model-only window.
        using (var host = new Form { IsMdiContainer = true, ShowInTaskbar = false, Opacity = 0,
            Width = 1200, Height = 900 })
        using (var window = new ScribeWindow(adapter, host, Path.Combine(directory, "history-settings.json")))
        {
            host.Show(); window.Show();
            Check(window.IsHandleCreated && window.Visible && window.IsMdiChild && window.MdiParent == host,
                "history uses a shown MDI owner initialized in that host");
            RunWindow(window, "StartPath", Path.Combine(directory, "history.json"));
            CheckHistoryContents(adapter, editor, directory, window);
        }
    }

    private static void CheckHistoryContents(FcsAdapter adapter, Form editor, string directory, ScribeWindow window)
    {
        var project = (JournalProject)FcsAdapter.Field(window, "project");
        Snapshot live = adapter.Capture();
        project.Record(live);
        using (var empty = new RecordingHistoryDialog(project.ExportRecordings(live)))
        {
            Check(((ListBox)FcsAdapter.Field(empty, "entries")).Items.Count == 0 &&
                !((Button)FcsAdapter.Field(empty, "ai")).Enabled &&
                !((Button)FcsAdapter.Field(empty, "markdown")).Enabled &&
                ((TextBox)FcsAdapter.Field(empty, "preview")).Text.Contains("No recordings yet"),
                "history opens an explicit read-only empty state without inventing evidence");
        }
        Snapshot changed = live.Copy(); changed.Records[0].Name += " History browsing fixture";
        project.Record(changed);
        string firstSession = project.SessionId;
        Snapshot context = changed.Copy(); context.Warnings.Add("Historical browser warning");
        context.Workshop.Warnings.Add("Historical Workshop-only warning");
        context.Workshop.Issues = new List<WorkshopIssue> {
            new WorkshopIssue { Component = "History fixture", Code = "unavailable", Message = "Historical availability reason" }
        };
        project.Record(context);
        project.Compile();
        project.BeginSession();
        project.Record(live);
        Put(window, "tracker", project.ResumeTracker());
        var read = (Task<List<RecordingEvidence>>)FcsAdapter.Call(window, "ReadRecordingHistory", live);
        AwaitUi(read);
        List<RecordingEvidence> choices = read.Result;
        Check(choices.Count == 3 && choices.Count(x => x.SessionId == firstSession) == 2 &&
            choices.Last().SessionId != firstSession && choices[1].Packet.Changes.Count == 0,
            "window reads compiled/uncompiled multi-session history once, including context-only observations");
        Dictionary<string, string> files = Directory.GetFiles(project.DirectoryName, "*", SearchOption.AllDirectories)
            .Where(x => Path.GetFileName(x) != "writer.lock").Concat(new[] { project.PathName }).ToDictionary(x => x, TrackingStorage.HashFile);
        string original = Json.Write(choices), head = project.HeadId, fingerprint = live.Fingerprint();
        int pendingCount = project.PendingCount;
        Packet existing = new Tracker(live).Export(live);
        FcsAdapter.Call(window, "AdoptWorkspace", existing, "Unfinished history draft", null, null);
        long revision = (long)FcsAdapter.Field(window, "draftRevision");
        var nav = FcsAdapter.Field(editor, "nav");
        bool dirty = (bool)FcsAdapter.Property(nav, "HasChanges");
        string modHash = TrackingStorage.HashFile(adapter.ActiveModDirectory() + "\\scribe-test.mod");
        string nativeNotes = adapter.Notes(live.Records[0].Id);
        Put(window, "draftDecision", new Func<string, DialogResult>(operation => DialogResult.Cancel));
        using (var dialog = new RecordingHistoryDialog(choices))
        {
            var list = (ListBox)FcsAdapter.Field(dialog, "entries");
            var filter = (ComboBox)FcsAdapter.Field(dialog, "session");
            var search = (TextBox)FcsAdapter.Field(dialog, "search");
            var preview = (TextBox)FcsAdapter.Field(dialog, "preview");
            Check(list.Items.Count == 3 && list.SelectionMode == SelectionMode.MultiExtended && preview.ReadOnly &&
                preview.Text.Contains(choices[0].EntryId + ":change-") && preview.Text.Contains("Original source fingerprint:"),
                "history displays original identities, qualified citations and exact read-only evidence");
            Check(preview.Text.Contains("History browsing fixture") && preview.Text.Contains("Source labels:") &&
                preview.Text.Contains(Tracker.SamplingNotice), "history shows names, source labels and sampling boundaries");
            filter.SelectedItem = firstSession;
            Check(list.Items.Count == 2, "session filter retains all and only that session's observations");
            search.Text = "historical browser WARNING";
            Check(list.Items.Count == 1 && preview.Text.Contains("Context-only observation") &&
                preview.Text.Contains("Historical Workshop-only warning") && preview.Text.Contains("Historical availability reason"),
                "case-insensitive history search can inspect context-only warnings and optional availability");
            search.Text = "Historical availability reason";
            Check(list.Items.Count == 1, "search includes optional Workshop issue messages");
            filter.SelectedIndex = 0;
            search.Text = choices[0].Packet.Changes[0].Path;
            bool fieldMatches = list.Items.Count == 2;
            search.Text = choices[0].Packet.Changes[0].After.Text.ToUpperInvariant();
            Check(fieldMatches && list.Items.Count == 2,
                "search matches exact field paths and typed name payloads in both directions, without including context-only entries");
            filter.SelectedIndex = 0; search.Text = choices[2].EntryId;
            Check(list.Items.Count == 1 && preview.Text.Contains(choices[2].EntryId), "search finds a recording by stable identity");
            search.Text = "not a matching recording";
            Check(list.Items.Count == 0 && !((Button)FcsAdapter.Field(dialog, "ai")).Enabled &&
                preview.Text.Contains("No entries selected"), "empty filters disable export and clear stale preview evidence");
            search.Text = "";
            list.ClearSelected(); list.SetSelected(0, true); list.SetSelected(2, true);
            Check(preview.Text.Contains("\r\n" + choices[0].EntryId + " | ") &&
                preview.Text.Contains("\r\n" + choices[2].EntryId + " | ") &&
                !preview.Text.Contains("\r\n" + choices[1].EntryId + " | "),
                "multi-selection previews exactly the selected original recording blocks, not their baseline references");
            CheckHistoryExportSelection(dialog, choices, true);
        }
        using (var dialog = new RecordingHistoryDialog(choices))
            CheckHistoryExportSelection(dialog, choices, false);
        using (var dialog = new RecordingHistoryDialog(choices))
        using (var cancel = new System.Windows.Forms.Timer { Interval = 20 })
        {
            bool clicked = false, cancelledRange = false;
            cancel.Tick += delegate {
                var range = Application.OpenForms.OfType<RecordingRangeDialog>().FirstOrDefault();
                if (range != null)
                {
                    cancelledRange = true; ((Button)range.CancelButton).PerformClick();
                }
                else if (!clicked)
                {
                    clicked = true; ((Button)FcsAdapter.Field(dialog, "ai")).PerformClick();
                }
                else if (cancelledRange)
                {
                    cancel.Stop(); ((Button)dialog.CancelButton).PerformClick();
                }
            };
            cancel.Start();
            Check(dialog.ShowDialog(window) == DialogResult.Cancel && cancelledRange && dialog.First == -1 && dialog.Last == -1,
                "cancelling the nested export selector returns to browsing without accepting a range");
        }
        using (var pending = new RecordingHistoryDialog(project.ExportRecordings(changed)))
        {
            ((TextBox)FcsAdapter.Field(pending, "search")).Text = "unrecorded";
            Check(((ListBox)FcsAdapter.Field(pending, "entries")).Items.Count == 1 &&
                ((TextBox)FcsAdapter.Field(pending, "preview")).Text.Contains("pending"),
                "unrecorded current state has an explicit separately searchable group, not a durable session entry");
        }
        var legacy = Json.Read<List<RecordingEvidence>>(Json.Write(choices));
        legacy[0].Kind = "checkpoint"; legacy[0].SessionId = null;
        using (var dialog = new RecordingHistoryDialog(legacy))
        {
            ((ComboBox)FcsAdapter.Field(dialog, "session")).SelectedIndex = 1;
            Check(((ListBox)FcsAdapter.Field(dialog, "entries")).Items.Count == 1 &&
                ((TextBox)FcsAdapter.Field(dialog, "preview")).Text.Contains("(legacy)"),
                "legacy evidence remains browseable without inventing a session ID");
        }
        var historyAction = window.MainMenuStrip.Items.Cast<ToolStripMenuItem>().First().DropDownItems
            .Cast<ToolStripMenuItem>().Single(x => x.Text.Replace("&", "") == "Browse history");
        RunWindow(window, "SaveSettings", new PluginSettings { ModNotesEnabled = true });
        var work = (ModWorkView)FcsAdapter.Field(window, "modWork");
        work.Notes.Text = "Unsaved text while reading history";
        string notesHash = TrackingStorage.HashFile(work.Work.NotesPath);
        Check(window.IsMdiChild && window.IsHandleCreated && window.Visible,
            "history retains its initialized MDI owner after nested selectors");
        using (var cancel = new System.Windows.Forms.Timer { Interval = 20 })
        {
            cancel.Tick += delegate {
                var opened = Application.OpenForms.OfType<RecordingHistoryDialog>().FirstOrDefault();
                if (opened == null) return;
                cancel.Stop(); ((Button)opened.CancelButton).PerformClick();
            };
            cancel.Start(); historyAction.PerformClick();
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while ((bool)FcsAdapter.Field(window, "busy") && timeout.Elapsed < TimeSpan.FromMinutes(2))
            { Application.DoEvents(); Thread.Sleep(10); }
            Check(!(bool)FcsAdapter.Field(window, "busy") &&
                ((Label)FcsAdapter.Field(window, "status")).Text.StartsWith("History browser closed", StringComparison.Ordinal),
                "actual Core history action opens and closes through the normal busy/modal guard");
        }
        Check(work.Notes.Text == "Unsaved text while reading history" &&
            TrackingStorage.HashFile(work.Work.NotesPath) == notesHash,
            "opening/cancelling history does not save even enabled unsaved Scribe text");
        Check(Json.Write(choices) == original && project.HeadId == head && project.PendingCount == pendingCount &&
            files.All(x => TrackingStorage.HashFile(x.Key) == x.Value),
            "history filtering/range previews/cancellation do not mutate evidence or tracking files/head/debt");
        Check(Object.ReferenceEquals(FcsAdapter.Field(window, "exported"), existing) &&
            ((TextBox)FcsAdapter.Field(window, "draft")).Text == "Unfinished history draft" &&
            (long)FcsAdapter.Field(window, "draftRevision") == revision,
            "read-only history retains unfinished draft and original packet association");
        Packet selected = RangeExport.Create(project.ProjectId, choices, 0, 2, live);
        string markdown = Path.Combine(directory, "history-range.md");
        RunWindow(window, "SaveMarkdownPath", markdown, selected, null, revision);
        Check(File.ReadAllText(markdown).Contains("Historical browser warning") &&
            Object.ReferenceEquals(FcsAdapter.Field(window, "exported"), existing),
            "history-selected Markdown uses the existing guarded renderer without replacing the draft");
        string aiPath = Path.Combine(directory, "history-range.json");
        CallWindowAction(window, "SavePacketPath", aiPath, selected);
        Check(Json.Read<Packet>(File.ReadAllText(aiPath)).Recordings.Select(x => x.EntryId)
            .SequenceEqual(choices.Select(x => x.EntryId)), "history-selected AI output retains complete original range order and identities");
        Check(adapter.Capture().Fingerprint() == fingerprint && adapter.Notes(live.Records[0].Id) == nativeNotes &&
            (bool)FcsAdapter.Property(nav, "HasChanges") == dirty &&
            TrackingStorage.HashFile(adapter.ActiveModDirectory() + "\\scribe-test.mod") == modHash &&
            project.HeadId == head && files.All(x => TrackingStorage.HashFile(x.Key) == x.Value),
            "history and exports leave native model/dirty state/mod bytes and journal storage untouched");
        string sessionBytes = File.ReadAllText(project.SessionPath);
        File.AppendAllText(project.SessionPath, "\n");
        bool rejected = false;
        try
        {
            var invalid = (Task<List<RecordingEvidence>>)FcsAdapter.Call(window, "ReadRecordingHistory", live);
            AwaitUi(invalid);
        }
        catch (IOException) { rejected = true; }
        finally { File.WriteAllText(project.SessionPath, sessionBytes); }
        Check(rejected, "history refuses changed source bytes before exposing a catalogue");
        FcsAdapter.Call(window, "AdoptWorkspace", null, "", null, null);
    }

    private static void CheckHistoryExportSelection(RecordingHistoryDialog history, IList<RecordingEvidence> choices, bool markdown)
    {
        var list = (ListBox)FcsAdapter.Field(history, "entries");
        list.ClearSelected(); list.SetSelected(0, true); list.SetSelected(2, true);
        bool inspected = false, clicked = false;
        using (var choose = new System.Windows.Forms.Timer { Interval = 20 })
        {
            choose.Tick += delegate {
                var range = Application.OpenForms.OfType<RecordingRangeDialog>().FirstOrDefault();
                if (range == null)
                {
                    if (!clicked)
                    {
                        clicked = true;
                        ((Button)FcsAdapter.Field(history, markdown ? "markdown" : "ai")).PerformClick();
                    }
                    return;
                }
                choose.Stop();
                Check(range.First == 0 && range.Last == 2 &&
                    ((TextBox)FcsAdapter.Field(range, "preview")).Text.Contains("\r\n" + choices[1].EntryId + " | "),
                    "history export previews the entire inclusive range, including the unselected middle entry");
                inspected = true; ((Button)FcsAdapter.Field(range, "export")).PerformClick();
            };
            choose.Start();
            Check(history.ShowDialog() == DialogResult.OK, "history export requires an explicit successful modal selection");
        }
        Check(inspected && history.DialogResult == DialogResult.OK && history.First == 0 && history.Last == 2 &&
            history.Markdown == markdown, "history returns explicit inclusive bounds and the selected export format");
    }

    private static void CheckReviewedMarkdownWindow(FcsAdapter adapter, Form editor, string directory, ScribeWindow window)
    {
        object nav = FcsAdapter.Field(editor, "nav");
        IDictionary records = (IDictionary)FcsAdapter.Field(FcsAdapter.Field(FcsAdapter.Field(nav, "ou"), "gameData"), "items");
        object item = records.Values.Cast<object>().First();
        string name = (string)FcsAdapter.Property(item, "Name");
        bool dirty = (bool)FcsAdapter.Property(nav, "HasChanges");
        Snapshot before = adapter.Capture();
        string modHash = TrackingStorage.HashFile(adapter.ActiveModDirectory() + "\\scribe-test.mod");
        try
        {
            Set(item, "Name", name + " Markdown fixture");
            Snapshot current = adapter.Capture();
            Packet packet = new Tracker(before).Export(current);
            NoteDraft notes = Json.Read<NoteDraft>(Json.Write(packet.ResponseTemplate));
            foreach (NoteEntry entry in notes.Entries) entry.Text = "Reviewed Markdown fixture.";
            string text = Json.Pretty(Json.Write(notes));
            FcsAdapter.Call(window, "AdoptWorkspace", packet, text, null, null);
            string workspace = Path.Combine(directory, "reviewed-workspace.json");
            RunWindow(window, "SaveWorkspacePath", workspace);
            string workspaceHash = TrackingStorage.HashFile(workspace), fingerprint = adapter.Capture().Fingerprint();
            string nativeNotes = adapter.Notes(notes.Entries[0].TargetRecordId);
            bool exportDirty = (bool)FcsAdapter.Property(nav, "HasChanges");
            long revision = (long)FcsAdapter.Field(window, "draftRevision");
            string destination = Path.Combine(directory, "reviewed.md");
            RunWindow(window, "SaveMarkdownPath", destination, packet, notes, revision);
            Check(File.ReadAllText(destination).Contains("Reviewed Markdown fixture") &&
                File.ReadAllText(destination).Contains("](#evidence-000001)") && FcsAdapter.Field(window, "project") == null,
                "reviewed Markdown exports a validated draft without an open tracking project");
            Check(((TextBox)FcsAdapter.Field(window, "draft")).Text == text &&
                Object.ReferenceEquals(FcsAdapter.Field(window, "exported"), packet) &&
                !(bool)FcsAdapter.Property(window, "DraftNeedsSave") &&
                TrackingStorage.HashFile(workspace) == workspaceHash,
                "reviewed Markdown retains exact draft text, original packet and saved workspace association");
            using (var restored = new ScribeWindow(adapter, editor, Path.Combine(directory, "restored-markdown-settings.json")))
            {
                RunWindow(restored, "OpenWorkspacePath", workspace);
                Packet restoredPacket = (Packet)FcsAdapter.Field(restored, "exported");
                NoteDraft restoredDraft = Json.Read<NoteDraft>(((TextBox)FcsAdapter.Field(restored, "draft")).Text);
                string restoredPath = Path.Combine(directory, "restored.md");
                RunWindow(restored, "SaveMarkdownPath", restoredPath, restoredPacket, restoredDraft,
                    (long)FcsAdapter.Field(restored, "draftRevision"));
                Check(File.ReadAllText(restoredPath) == File.ReadAllText(destination) &&
                    FcsAdapter.Field(restored, "project") == null && TrackingStorage.HashFile(workspace) == workspaceHash,
                    "a fresh window exports identical reviewed Markdown from a restored workspace without tracking or rewriting the bundle");
            }
            Check(adapter.Capture().Fingerprint() == fingerprint && adapter.Notes(notes.Entries[0].TargetRecordId) == nativeNotes &&
                (bool)FcsAdapter.Property(nav, "HasChanges") == exportDirty &&
                TrackingStorage.HashFile(adapter.ActiveModDirectory() + "\\scribe-test.mod") == modHash,
                "reviewed Markdown never mutates native gameplay, annotations, dirty state or saved mod bytes");
            ((TextBox)FcsAdapter.Field(window, "draft")).Text += " ";
            string late = Path.Combine(directory, "late.md");
            bool rejected = false;
            try { RunWindow(window, "SaveMarkdownPath", late, packet, notes, revision); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && !File.Exists(late), "draft edits during destination selection block stale reviewed exports");
            NoteDraft incomplete = Json.Read<NoteDraft>(Json.Write(notes));
            incomplete.Entries[0].ChangeIds.RemoveAt(0);
            string invalid = Path.Combine(directory, "incomplete.md");
            rejected = false;
            try { RunWindow(window, "SaveMarkdownPath", invalid, packet, incomplete, (long)FcsAdapter.Field(window, "draftRevision")); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected && !File.Exists(invalid), "actual reviewed Markdown path rejects missing citations before writing");
            Set(item, "Name", name);
            string stale = Path.Combine(directory, "stale.md");
            rejected = false;
            try { RunWindow(window, "SaveMarkdownPath", stale, packet, notes, (long)FcsAdapter.Field(window, "draftRevision")); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && !File.Exists(stale), "actual reviewed Markdown path rejects stale editor state");
        }
        finally { Set(item, "Name", name); Set(nav, "HasChanges", dirty); }
    }

    private static void CallWindowAction(ScribeWindow window, string method, params object[] args)
    {
        try { FcsAdapter.Call(window, method, args); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
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
            var notice = (TextBox)FcsAdapter.Field(window, "help");
            var timer = (System.Windows.Forms.Timer)FcsAdapter.Field(window, "timer");
            var draft = (TextBox)FcsAdapter.Field(window, "draft");
            Check(FcsAdapter.Field(window, "settingsError") != null &&
                notice.Text.Contains(path) && notice.Text.Contains("SETTINGS RECOVERY") &&
                notice.Text.Contains("automatic recording disabled") && TrackingStorage.HashFile(path) == original,
                "corrupt settings open the actual window with bottom recovery information and untouched source");
            var settingsAction = (ToolStripMenuItem)window.MainMenuStrip.Items[window.MainMenuStrip.Items.Count - 1];
            typeof(ToolStripItem).GetMethod("HandleMouseEnter", Flags).Invoke(settingsAction, new object[] { EventArgs.Empty });
            Check(notice.Text == settingsAction.AccessibleDescription &&
                ((Label)FcsAdapter.Field(window, "status")).Text.Contains("No tracking project"),
                "recovery information yields temporarily to hover without changing operation status");
            FcsAdapter.Call(window, "RefreshCompilationNotice");
            Check(notice.Text == settingsAction.AccessibleDescription, "notice refresh does not overwrite active hover help");
            typeof(ToolStripItem).GetMethod("HandleMouseLeave", Flags).Invoke(settingsAction, new object[] { EventArgs.Empty });
            Check(notice.Text.Contains("SETTINGS RECOVERY") && notice.Text.Contains(path),
                "hover exit restores current recovery details");
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
                ((JournalSchedule)FcsAdapter.Field(window, "schedule")).Configure(-JournalSchedule.CompilationMilliseconds, 30);
                FcsAdapter.Call(window, "ConfigureTimer");
                Put(header, "Author", "Settings recovery manual recording fixture");
                FcsAdapter.Call(timer, "OnTick", EventArgs.Empty);
                RunWindow(window, "AutomaticTick");
                Check(!timer.Enabled && project.EntryCount == 0,
                    "recovery explicitly gates timer dispatch and automatic work even with a nonzero interval");
                RunWindow(window, "Review");
                Check(project.EntryCount == 1 && project.PendingCount == 1 && notice.Text.Contains("SETTINGS RECOVERY"),
                    "manual gameplay recording works in recovery without clearing its default warning");
                RunWindow(window, "ReloadSettings");
                Check(FcsAdapter.Field(window, "settingsError") != null && !timer.Enabled &&
                    TrackingStorage.HashFile(path) == original,
                    "failed reload keeps safe manual mode and never rewrites corrupt settings");
                File.WriteAllText(path, Json.Write(new PluginSettings()));
                string external = TrackingStorage.HashFile(path);
                bool stale = false;
                try { RunWindow(window, "SaveSettings", new PluginSettings { ReviewSeconds = 60 }); }
                catch (IOException) { stale = true; }
                Check(stale && TrackingStorage.HashFile(path) == external && notice.Text.Contains("SETTINGS RECOVERY"),
                    "a stale recovery window cannot overwrite another instance's repaired settings");
                typeof(ToolStripItem).GetMethod("HandleMouseEnter", Flags).Invoke(settingsAction, new object[] { EventArgs.Empty });
                RunWindow(window, "ReloadSettings");
                Check(notice.Text.Contains(TrackingChoiceDialog.BoundaryText) && !notice.Text.Contains("SETTINGS RECOVERY"),
                    "starting a reload clears transient help and restores updated default information");
                typeof(ToolStripItem).GetMethod("HandleMouseLeave", Flags).Invoke(settingsAction, new object[] { EventArgs.Empty });
                Check(FcsAdapter.Field(window, "settingsError") == null && !notice.Text.Contains("SETTINGS RECOVERY") && !timer.Enabled,
                    "hover exit after validated reload restores current information rather than a stale recovery warning");
                File.WriteAllText(path, "another malformed original");
                RunWindow(window, "ReloadSettings");
                RunWindow(window, "SaveSettings", new PluginSettings { ReviewSeconds = 30 });
                string backup = Directory.GetFiles(directory, "recovery-settings.json.recovery-*.bak").Single();
                Check(File.ReadAllText(backup) == "another malformed original" &&
                    PluginSettings.Load(path).ReviewSeconds == 30 && !notice.Text.Contains("SETTINGS RECOVERY") && timer.Enabled,
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
                    Check(unreadable && FcsAdapter.Field(window, "settingsHash") == null && notice.Text.Contains("SETTINGS RECOVERY") && !timer.Enabled,
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
                Check(!notice.Text.Contains("SETTINGS RECOVERY") && timer.Enabled && Object.ReferenceEquals(project, FcsAdapter.Field(window, "project")) &&
                    Object.ReferenceEquals(packet, FcsAdapter.Field(window, "exported")) && draft.Text == "{ unfinished draft retained\r\n",
                    "reload after access recovery retains the current project, original packet and exact unfinished draft");
            }
            finally { Put(header, "Author", author); }
        }
    }

    private static void CheckSchedulingWindow(FcsAdapter adapter, Form editor)
    {
        string directory = Directory.GetCurrentDirectory();
        string settingsPath = Path.Combine(directory, "scheduling-settings.json");
        string path = Path.Combine(directory, "mods", "scheduling-project.json");
        TrackingStorage.WriteAtomic(settingsPath, new PluginSettings { ReviewSeconds = 30 }, null);
        object header = FcsAdapter.Field(FcsAdapter.Field(FcsAdapter.Field(
            FcsAdapter.Field(editor, "nav"), "ou"), "gameData"), "header");
        string author = (string)FcsAdapter.Field(header, "Author");
        using (var host = new Form { IsMdiContainer = true, ShowInTaskbar = false, Opacity = 0, Width = 1200, Height = 900 })
        using (var window = new ScribeWindow(adapter, editor, settingsPath))
        {
            try
            {
                window.MdiParent = host; host.Show(); window.Show();
                RunWindow(window, "StartPath", path);
                var schedule = (JournalSchedule)FcsAdapter.Field(window, "schedule");
                var clock = (System.Diagnostics.Stopwatch)FcsAdapter.Field(window, "clock");
                var timer = (System.Windows.Forms.Timer)FcsAdapter.Field(window, "timer");
                var notice = (TextBox)FcsAdapter.Field(window, "help");
                var project = (JournalProject)FcsAdapter.Field(window, "project");
                Check(schedule.NextCompilation == Int64.MaxValue && !notice.Text.Contains("Pending compilation:"),
                    "empty tracking has no phantom compilation deadline or debt notice");
                Put(header, "Author", "Compilation scheduling fixture");
                RunWindow(window, "Review");
                string anchorHash = TrackingStorage.HashFile(path);
                long deadline = schedule.NextCompilation, review = schedule.NextReview;
                Check(project.PendingCount == 1 && notice.Text.Contains("Pending compilation: 1") &&
                    notice.Text.Contains(project.OldestPendingUtc.Value.ToString("u")),
                    "actual recording exposes a default count, oldest UTC and pending deadline in the shared information box");
                string custom = Path.Combine(directory, "schedule-custom"); Directory.CreateDirectory(custom);
                for (int i = 0; i < 8; i++)
                {
                    RunWindow(window, "SaveSettings", new PluginSettings { ReviewSeconds = 30,
                        ExportDirectory = i % 2 == 0 ? custom : null });
                    RunWindow(window, "ReloadSettings");
                }
                Check(schedule.NextCompilation == deadline && schedule.NextReview == review,
                    "repeated real settings saves/reloads and folder-only edits preserve both deadlines");
                RunWindow(window, "SaveSettings", new PluginSettings { ReviewSeconds = 60 });
                Check(schedule.NextCompilation == deadline && schedule.NextReview > clock.ElapsedMilliseconds &&
                    schedule.NextReview != review, "interval changes re-arm only the recording deadline");
                RunWindow(window, "SaveSettings", new PluginSettings());
                Check(!timer.Enabled && schedule.NextCompilation == deadline && notice.Text.Contains("Manual mode"),
                    "manual transition holds debt visibly without automatic work");
                window.Width = 340; window.PerformLayout();
                Check(notice.Width <= window.ClientSize.Width && notice.ScrollBars == ScrollBars.Vertical &&
                    notice.Text.Contains("Pending compilation: 1"),
                    "pending debt remains readable by scrolling within the shared narrow-window information box");
                window.Width = 1080;
                File.WriteAllText(settingsPath, "{ invalid settings");
                RunWindow(window, "ReloadSettings");
                RunWindow(window, "AutomaticTick");
                Check(FcsAdapter.Field(window, "settingsError") != null && schedule.NextCompilation == deadline &&
                    !timer.Enabled && project.PendingCount == 1 && notice.Text.Contains("Settings recovery"),
                    "failed reload preserves debt while recovery independently blocks automatic work");
                RunWindow(window, "SaveSettings", new PluginSettings());

                timer.Stop(); project.Dispose();
                string[] lines = File.ReadAllLines(project.SessionPath);
                var session = Json.Read<SessionHeader>(lines[0]);
                var line = Json.Read<JournalLine>(lines[1]);
                DateTime aged = DateTime.UtcNow.AddMinutes(-31);
                session.StartedUtc = aged.AddSeconds(-1).ToString("o");
                line.Record.RecordedUtc = aged.ToString("o"); line.Hash = Json.StreamHash(line.Record);
                File.WriteAllLines(project.SessionPath, new[] { Json.Write(session), Json.Write(line) });
                for (int i = 0; i < 3; i++)
                {
                    RunWindow(window, "ResumePath", path);
                    project = (JournalProject)FcsAdapter.Field(window, "project");
                    Check(project.OldestPendingUtc == aged && project.PendingCount == 1 &&
                        schedule.NextCompilation <= clock.ElapsedMilliseconds &&
                        !schedule.CompilationDue(Int64.MaxValue) && notice.Text.Contains("due now/overdue") &&
                        notice.Text.Contains("Manual mode") && !File.Exists(project.JournalPath),
                        "short-session resume " + i + " retains aged debt without compiling in manual mode");
                }
                RunWindow(window, "AutomaticTick");
                Check(project.PendingCount == 1 && !File.Exists(project.JournalPath),
                    "even a direct automatic-work call cannot compile overdue manual-mode debt");
                window.Hide();
                RunWindow(window, "SaveSettings", new PluginSettings { ReviewSeconds = 30 });
                timer.Stop();
                long overdue = schedule.NextCompilation;
                review = schedule.NextReview;
                Check(schedule.CompilationDue(clock.ElapsedMilliseconds) && review > clock.ElapsedMilliseconds,
                    "automatic re-enable retains overdue debt with an independent future recording deadline");
                FcsAdapter.Call(timer, "OnTick", EventArgs.Empty);
                Check(project.PendingCount == 1 && schedule.NextCompilation == overdue,
                    "hidden-window deferral does not consume compilation debt");
                window.Show(); window.WindowState = FormWindowState.Minimized;
                FcsAdapter.Call(timer, "OnTick", EventArgs.Empty);
                Check(project.PendingCount == 1 && schedule.NextCompilation == overdue,
                    "minimized-window deferral does not consume compilation debt");
                window.WindowState = FormWindowState.Normal;
                Put(window, "busy", true);
                FcsAdapter.Call(timer, "OnTick", EventArgs.Empty);
                Put(window, "busy", false);
                Check(project.PendingCount == 1 && schedule.NextCompilation == overdue,
                    "busy deferral does not consume compilation debt");
                var core = (ToolStripMenuItem)window.MainMenuStrip.Items[0];
                core.ShowDropDown(); FcsAdapter.Call(timer, "OnTick", EventArgs.Empty); core.HideDropDown();
                Check(project.PendingCount == 1 && schedule.NextCompilation == overdue,
                    "open menus defer overdue compilation without resetting it");
                using (var modal = new Form())
                {
                    modal.Shown += delegate {
                        modal.BeginInvoke(new Action(() => {
                            FcsAdapter.Call(timer, "OnTick", EventArgs.Empty);
                            Check(project.PendingCount == 1 && schedule.NextCompilation == overdue,
                                "modal deferral retains overdue compilation");
                            modal.Close();
                        }));
                    };
                    modal.ShowDialog(window);
                }
                File.WriteAllText(project.JournalPath, "external journal conflict");
                bool failed = false;
                try { RunWindow(window, "Compile"); }
                catch (IOException) { failed = true; }
                timer.Stop();
                Check(failed && project.PendingCount == 1 && schedule.NextCompilation == overdue,
                    "a failed compilation never clears or moves the pending deadline");
                typeof(ToolStripItem).GetMethod("HandleMouseEnter", Flags).Invoke(core, new object[] { EventArgs.Empty });
                FcsAdapter.Call(window, "SetActionFailure", true);
                FcsAdapter.Call(window, "SetActionFailure", false);
                Check(notice.Text == core.AccessibleDescription &&
                    ((Label)FcsAdapter.Field(window, "status")).Text.Contains("Workshop action failed"),
                    "activity/errors remain distinct while compilation warnings update behind hover help");
                typeof(ToolStripItem).GetMethod("HandleMouseLeave", Flags).Invoke(core, new object[] { EventArgs.Empty });
                Check(notice.Text.Contains("Automatic work paused") && !timer.Enabled &&
                    schedule.NextCompilation == overdue, "failure notice preserves paused debt through later Workshop-only errors");
                File.Delete(project.JournalPath);
                RunWindow(window, "ReloadSettings"); timer.Stop();
                Check(schedule.NextCompilation == overdue && schedule.NextReview == review,
                    "explicit validated reload unpauses without postponing compilation or recording");
                RunWindow(window, "AutomaticTick");
                timer.Stop();
                Check(project.PendingCount == 0 && schedule.OldestPendingUtc == null &&
                    schedule.NextCompilation == Int64.MaxValue && schedule.NextReview == review &&
                    !notice.Text.Contains("Pending compilation:") && TrackingStorage.HashFile(path) == anchorHash &&
                    File.Exists(project.SessionPath) && File.ReadAllLines(project.JournalPath).Length == 1,
                    "eligible automatic work compiles retained debt once, clears its notice and preserves originals/review timing");
                string compiled = TrackingStorage.HashFile(project.JournalPath);
                RunWindow(window, "AutomaticTick");
                Check(TrackingStorage.HashFile(project.JournalPath) == compiled && project.EntryCount == 1,
                    "empty follow-up automatic work does not recompile or record unchanged history");
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
                schedule.Configure(now, seconds);
                FcsAdapter.Call(window, "ConfigureTimer");
                Check(timer.Enabled && timer.Interval == 1000 && schedule.NextReview == now + seconds * 1000L,
                    "automatic deadline is exactly " + seconds + " seconds with independent UI polling");
            }
            settings.ReviewSeconds = 0;
            FcsAdapter.Call(window, "ConfigureTimer");
            Check(!timer.Enabled, "manual review disables scheduling");
            ToolStripMenuItem reviewAction = window.MainMenuStrip.Items.Cast<ToolStripMenuItem>().First()
                .DropDownItems.Cast<ToolStripMenuItem>().Single(x => x.Text.Replace("&", "") == "Save Changes");
            reviewAction.PerformClick();
            var reviewTimeout = System.Diagnostics.Stopwatch.StartNew();
            while ((bool)FcsAdapter.Field(window, "busy") && reviewTimeout.Elapsed < TimeSpan.FromMinutes(3))
            {
                Application.DoEvents(); Thread.Sleep(10);
            }
            Check(!(bool)FcsAdapter.Field(window, "busy") && !((bool)FcsAdapter.Field(window, "automaticPaused")),
                "the Save Changes menu dispatches through the serialized asynchronous operation gate");
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
                    schedule.Configure(clock.ElapsedMilliseconds - 30000, 30);
                    schedule.Reviewed(clock.ElapsedMilliseconds - 30000);
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
                    schedule.Start(clock.ElapsedMilliseconds, DateTime.UtcNow.AddMinutes(31), 30, saved.OldestPendingUtc);
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

    private static TextBox OpenNameEditor(object grid, string value)
    {
        object row = FcsAdapter.Call(grid, "getItem", "Name");
        Set(grid, "SelectedItem", row);
        object property = FcsAdapter.Call(grid, "CreateEditor");
        var text = (TextBox)FcsAdapter.Field(property, "mTextBox");
        Check(text != null && text.Focus(), "the native bound Name editor has a focused textbox");
        text.Text = value;
        return text;
    }

    private static void CheckSamplingWindow(FcsAdapter adapter, Form editor, Assembly assembly, string modPath)
    {
        string directory = Directory.GetCurrentDirectory();
        object nav = FcsAdapter.Field(editor, "nav");
        object data = FcsAdapter.Field(FcsAdapter.Field(nav, "ou"), "gameData");
        object item = ((IDictionary)FcsAdapter.Field(data, "items")).Values.Cast<object>().First();
        string id = (string)FcsAdapter.Property(item, "stringID");
        string original = (string)FcsAdapter.Property(item, "Name");
        string nameA = original + " S11 A", nameB = original + " S11 B";
        string modHash = TrackingStorage.HashFile(modPath), notes = adapter.Notes(id);
        Set(nav, "Undo", Activator.CreateInstance(assembly.GetType("forgotten_construction_set.UndoSystem", true),
            new[] { data }));
        object list = FcsAdapter.Field(nav, "listView1");
        Set(list, "Source", data);
        Set(list, "Filter", Activator.CreateInstance(assembly.GetType("forgotten_construction_set.ItemFilter", true),
            new object[] { "" }));
        using (var host = new Form { ShowInTaskbar = false, Opacity = 0, Width = 900, Height = 700 })
        using (var box = (Control)Activator.CreateInstance(assembly.GetType("forgotten_construction_set.ObjectPropertyBox", true)))
        using (var window = new ScribeWindow(adapter, editor, Path.Combine(directory, "sampling-settings.json")))
        {
            box.Dock = DockStyle.Fill;
            var focusTarget = new Button { Dock = DockStyle.Bottom, Text = "Focus boundary" };
            host.Controls.Add(box); host.Controls.Add(focusTarget);
            FcsAdapter.Call(box, "setup", item, nav);
            host.Show(); Application.DoEvents();
            object grid = FcsAdapter.Field(box, "grid");
            TextBox text = OpenNameEditor(grid, nameA);
            FcsAdapter.Call(text, "OnKeyDown", new KeyEventArgs(Keys.Enter));
            Check((string)FcsAdapter.Property(item, "Name") == nameA &&
                (bool)FcsAdapter.Property(nav, "HasChanges"), "the real bound textbox Enter event commits to FCS and marks dirty");
            RunWindow(window, "StartPath", Path.Combine(directory, "mods", "sampling.json"));
            var project = (JournalProject)FcsAdapter.Field(window, "project");
            var review = (TextBox)FcsAdapter.Field(window, "review");
            Check(review.Text.Contains("PARTIAL EXTRACTION") && review.Text.Contains(Tracker.SamplingNotice) &&
                project.State.Warnings.All(x => review.Text.Contains(x)), "every new baseline presents partial/sampling guidance and captured warnings");
            string fingerprint = project.Fingerprint, sessionHash = TrackingStorage.HashFile(project.SessionPath);
            text = OpenNameEditor(grid, nameB);
            RunWindow(window, "Review");
            Check(text.Text == nameB && (string)FcsAdapter.Property(item, "Name") == nameA &&
                project.Fingerprint == fingerprint && project.EntryCount == 0 &&
                TrackingStorage.HashFile(project.SessionPath) == sessionHash,
                "Review neither flushes pending native text nor fabricates evidence for it");
            Check(review.Text.Contains("Zero evidence is not proof") && review.Text.Contains("Uncommitted UI controls"),
                "zero-evidence results retain both sampling and native pending-control warnings");
            FcsAdapter.Call(text, "OnKeyDown", new KeyEventArgs(Keys.Escape));
            Check((string)FcsAdapter.Property(item, "Name") == nameA &&
                adapter.Capture().Fingerprint() == fingerprint, "the native textbox Escape event discards pending text without changing captured state");
            text = OpenNameEditor(grid, nameB);
            FcsAdapter.Call(text, "OnKeyDown", new KeyEventArgs(Keys.Enter));
            RunWindow(window, "Review");
            Check(project.EntryCount == 1 && project.State.Records.Single(x => x.Id == id).Name == nameB,
                "a native Enter commit becomes the next recorded observation");
            text = OpenNameEditor(grid, nameA);
            int lostFocus = 0;
            text.LostFocus += delegate { lostFocus++; };
            focusTarget.Focus();
            Application.DoEvents();
            Check(lostFocus > 0 && !text.Focused, "a real WinForms focus transition leaves the native textbox");
            Check((string)FcsAdapter.Property(item, "Name") == nameA,
                "native lost-focus handling commits the pending Name text");
            RunWindow(window, "Review");
            Check(project.EntryCount == 2 && project.Fingerprint == fingerprint,
                "the recorded focus-committed reversal returns to the same captured state without losing history");
            var recordings = project.ExportRecordings(adapter.Capture());
            Packet packet = RangeExport.Create(project.ProjectId, recordings, 0, 1, adapter.Capture());
            Check(packet.Changes.Any(x => x.RecordId == id && x.Before.Text == nameA && x.After.Text == nameB) &&
                packet.Changes.Any(x => x.RecordId == id && x.Before.Text == nameB && x.After.Text == nameA),
                "actual native forward/reverse observations remain in the exported range");
            using (var dialog = new RecordingRangeDialog(recordings))
                Check(((TextBox)FcsAdapter.Field(dialog, "preview")).Text.Contains(Tracker.SamplingNotice) &&
                    ((TextBox)FcsAdapter.Field(dialog, "preview")).Text.Contains("PARTIAL EXTRACTION"),
                    "range previews keep partial-capture and sampling guidance");
            packet.Instructions = "Historical instructions remain unchanged.";
            string serialized = Json.Write(packet);
            FcsAdapter.Call(window, "ShowChanges", packet);
            Check(review.Text.Contains(Tracker.SamplingNotice) && Json.Write(packet) == serialized,
                "presenting old/restored evidence adds current guidance without rewriting its packet");
            text = OpenNameEditor(grid, nameB);
            FcsAdapter.Call(text, "OnKeyDown", new KeyEventArgs(Keys.Enter));
            text = OpenNameEditor(grid, nameA);
            FcsAdapter.Call(text, "OnKeyDown", new KeyEventArgs(Keys.Enter));
            sessionHash = TrackingStorage.HashFile(project.SessionPath);
            RunWindow(window, "Review");
            Check(project.EntryCount == 2 && TrackingStorage.HashFile(project.SessionPath) == sessionHash,
                "two native commits reversed between reviews are not an event log");
            Check(TrackingStorage.HashFile(modPath) == modHash && adapter.Notes(id) == notes &&
                (bool)FcsAdapter.Property(nav, "HasChanges"), "sampling never saves the mod/notes or clears native dirty state");
        }
    }

    private static void CheckTrackingRecoveryWindow(FcsAdapter adapter, Form editor, Assembly assembly, string modPath)
    {
        string directory = Directory.GetCurrentDirectory();
        string path = Path.Combine(directory, "mods", "recovery-boundary.json");
        string settings = Path.Combine(directory, "recovery-boundary-settings.json");
        object nav = FcsAdapter.Field(editor, "nav");
        object data = FcsAdapter.Field(FcsAdapter.Field(nav, "ou"), "gameData");
        object item = ((IDictionary)FcsAdapter.Field(data, "items")).Values.Cast<object>().First();
        string id = (string)FcsAdapter.Property(item, "stringID");
        string originalName = (string)FcsAdapter.Property(item, "Name");
        string recordedName = originalName + " RECORDED NOT SAVED";
        string modHash = TrackingStorage.HashFile(modPath);
        string notes = adapter.Notes(id);
        string recordedFingerprint;
        using (var window = new ScribeWindow(adapter, editor, settings))
        {
            RunWindow(window, "StartPath", path);
            Set(item, "Name", recordedName); Set(nav, "HasChanges", true);
            RunWindow(window, "Review");
            var project = (JournalProject)FcsAdapter.Field(window, "project");
            recordedFingerprint = project.Fingerprint;
            Check(project.EntryCount == 1 && project.PendingCount == 1 &&
                project.State.Records.Single(x => x.Id == id).Name == recordedName,
                "an unsaved real FCS edit becomes durable uncompiled tracking evidence");
            Check(TrackingStorage.HashFile(modPath) == modHash && adapter.Notes(id) == notes &&
                (bool)FcsAdapter.Property(nav, "HasChanges"), "recording neither saves nor restores the mod/notes or clears dirty state");
            Check(((Label)FcsAdapter.Field(window, "status")).Text.Contains(project.CapturedUtc) &&
                ((Label)FcsAdapter.Field(window, "status")).Text.Contains("NOT verified"),
                "recording status names the observed UTC without claiming a successful FCS save");
        }
        adapter = ReopenRecoveryModel(editor, assembly, modPath);
        Check(adapter.Capture().Records.Single(x => x.Id == id).Name == originalName &&
            TrackingStorage.HashFile(modPath) == modHash, "fresh native reload discards the unsaved edit without changing the saved mod");
        var preserved = Directory.GetFiles(path + ".scribe", "*", SearchOption.AllDirectories)
            .Where(x => Path.GetFileName(x) != "writer.lock").Concat(new[] { path })
            .ToDictionary(x => x, TrackingStorage.HashFile);
        using (var window = new ScribeWindow(adapter, editor, settings))
        {
            Packet discrepancy = null;
            Put(window, "resumeDecision", new Func<Packet, DialogResult>(packet => {
                discrepancy = packet; return DialogResult.No;
            }));
            RunWindow(window, "ResumePath", path);
            Check(discrepancy != null && discrepancy.Changes.Any(x => x.RecordId == id &&
                x.Before.Text == recordedName && x.After.Text == originalName),
                "actual Resume compares recorded unsaved edits against the freshly loaded saved state");
            Check(FcsAdapter.Field(window, "project") == null &&
                preserved.All(x => TrackingStorage.HashFile(x.Key) == x.Value) &&
                Directory.GetFiles(path + ".scribe", "*.jsonl", SearchOption.AllDirectories).Length == 1,
                "declining a discrepancy leaves history byte-identical and starts no session");
            string message = (string)FcsAdapter.Call(window, "ResumeMessage", discrepancy);
            Check(message.Contains(discrepancy.BaselineCapturedUtc) && message.Contains("automatic review") &&
                message.Contains("No: do not resume") && message.Contains(TrackingChoiceDialog.BoundaryText),
                "the discrepancy prompt explains Yes/No, lost edits, timing and no restoration/save");
            Put(window, "resumeDecision", new Func<Packet, DialogResult>(packet => DialogResult.Yes));
            RunWindow(window, "ResumePath", path);
            var project = (JournalProject)FcsAdapter.Field(window, "project");
            Check(project.Fingerprint == recordedFingerprint && project.EntryCount == 1 &&
                File.ReadAllLines(project.SessionPath).Length == 1 &&
                adapter.Capture().Records.Single(x => x.Id == id).Name == originalName,
                "accepting Resume keeps the recorded head until review and never restores the discarded edit");
            RunWindow(window, "Review");
            Check(project.EntryCount == 2 && project.PendingCount == 2 &&
                project.State.Records.Single(x => x.Id == id).Name == originalName,
                "the next review records the reversal as new evidence rather than rewriting the old entry");
            Check(TrackingStorage.HashFile(modPath) == modHash && adapter.Notes(id) == notes &&
                !(bool)FcsAdapter.Property(nav, "HasChanges"), "both Resume choices and the reversal review leave FCS files and clean state unchanged");
            RunWindow(window, "Compile");
            Check(project.PendingCount == 0 && File.ReadAllLines(project.JournalPath).Length == 2,
                "compilation consolidates both observations without saving the discarded edit");
        }
        data = FcsAdapter.Field(FcsAdapter.Field(nav, "ou"), "gameData");
        item = ((IDictionary)FcsAdapter.Field(data, "items"))[id];
        string savedName = originalName + " SAVED NOT RECORDED";
        Set(item, "Name", savedName); Set(nav, "HasChanges", true);
        object todo = FcsAdapter.Field(editor, "todoList");
        Type noteType = assembly.GetType("forgotten_construction_set.ToDo+NotesItem+Type", true);
        FcsAdapter.Call(todo, "AddItem", item, "S10 disposable notes backup.", Enum.Parse(noteType, "NOTE"), null, true, false);
        Check((bool)FcsAdapter.Call(editor, "saveFile"), "only an explicit normal FCS save persists the second edit and notes");
        adapter = ReopenRecoveryModel(editor, assembly, modPath);
        FcsAdapter.Call(todo, "LoadAll");
        string savedHash = TrackingStorage.HashFile(modPath), savedNotes = adapter.Notes(id);
        var noteHashes = Directory.GetFiles(Path.Combine(directory, "data"), "__TODO__*.txt")
            .ToDictionary(x => x, TrackingStorage.HashFile);
        Check(savedNotes.Contains("S10 disposable notes backup.") && noteHashes.Count > 0 &&
            adapter.Capture().Records.Single(x => x.Id == id).Name == savedName,
            "normal save/reopen retains the second edit and separate notes sidecar");
        using (var window = new ScribeWindow(adapter, editor, settings))
        {
            Packet discrepancy = null;
            Put(window, "resumeDecision", new Func<Packet, DialogResult>(packet => {
                discrepancy = packet; return DialogResult.Yes;
            }));
            RunWindow(window, "ResumePath", path);
            var project = (JournalProject)FcsAdapter.Field(window, "project");
            Check(discrepancy != null && discrepancy.Changes.Any(x => x.RecordId == id &&
                x.Before.Text == originalName && x.After.Text == savedName) && project.EntryCount == 2,
                "a saved edit ahead of compiled history is also a discrepancy, not an instruction to restore old state");
            RunWindow(window, "Review"); RunWindow(window, "Compile");
            Check(project.EntryCount == 3 && project.PendingCount == 0 &&
                project.Fingerprint == adapter.Capture().Fingerprint(), "review/compile catches history up to current saved editor state");
            Check(TrackingStorage.HashFile(modPath) == savedHash && adapter.Notes(id) == savedNotes &&
                noteHashes.All(x => TrackingStorage.HashFile(x.Key) == x.Value) &&
                !(bool)FcsAdapter.Property(nav, "HasChanges"), "tracking current saved edits never rewrites mod/notes files or dirty state");
        }
        string backup = Path.Combine(directory, "S10-backup");
        var sources = Directory.GetFiles(path + ".scribe", "*", SearchOption.AllDirectories)
            .Concat(new[] { path, modPath }).Concat(noteHashes.Keys).ToList();
        foreach (string source in sources)
        {
            string target = Path.Combine(backup, source.Substring(directory.Length + 1));
            Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(source, target);
            Check(TrackingStorage.HashFile(source) == TrackingStorage.HashFile(target),
                "backup retains exact bytes for " + Path.GetFileName(source));
        }
        using (var copied = JournalProject.Open(Path.Combine(backup, "mods", Path.GetFileName(path))))
            Check(copied.EntryCount == 3 && copied.PendingCount == 0 &&
                copied.Fingerprint == adapter.Capture().Fingerprint(), "the complete copied tracking set replays the saved state without mod restoration");
    }

    private static void CheckModWork(FcsAdapter adapter, Form editor, object data, Assembly assembly, string modPath)
    {
        string root = adapter.ActiveModDirectory(), notes = Path.Combine(root, "notes.txt"), todo = Path.Combine(root, "todo.txt");
        string original = TrackingStorage.HashFile(modPath), fingerprint = adapter.Capture().Fingerprint();
        bool dirty = (bool)FcsAdapter.Property(FcsAdapter.Field(editor, "nav"), "HasChanges");
        object nativeTodo = FcsAdapter.Field(editor, "todoList");
        string nativeFile = (string)FcsAdapter.Field(nativeTodo, "activeFile");
        string nativeHash = File.Exists(nativeFile) ? TrackingStorage.HashFile(nativeFile) : null;
        using (var window = new ScribeWindow(adapter, editor))
        using (var hook = new ModTodoMenu(assembly, editor, () => window))
        {
            Check(!File.Exists(notes) && !File.Exists(todo), "disabled mod work creates no text files on open");
            var view = (ModWorkView)FcsAdapter.Field(window, "modWork");
            var information = (TextBox)FcsAdapter.Field(window, "help");
            var tabs = window.Controls.OfType<TabControl>().Single();
            string defaultInformation = information.Text;
            tabs.SelectedTab = view.NotesTab;
            Check(((Label)FcsAdapter.Field(view, "notesPath")).Text.Contains("Enable Scribe notes") &&
                view.Notes.ReadOnly && information.Text == defaultInformation,
                "disabled notes explain their own setting without changing bottom information");
            tabs.SelectedTab = view.TodoTab;
            Check(((Label)FcsAdapter.Field(view, "todoPath")).Text.Contains("Enable Mod To Do") &&
                !view.Todos.Enabled && information.Text == defaultInformation,
                "disabled tasks explain their own setting without a duplicate footer warning");
            var menu = FcsAdapter.RecordMenu(editor);
            Check(menu.Items.IndexOf(hook.Item) == menu.Items.IndexOf(FcsAdapter.NativeTodoItem(editor)) + 1 &&
                hook.Item.Text == "Add Mod To Do", "Scribe task action is directly below the unchanged native Add Todo Item");
            FcsAdapter.Call(hook, "Opening", menu, new System.ComponentModel.CancelEventArgs());
            Check(!hook.Item.Available, "Mod To Do context entry is hidden by default");
            RunWindow(window, "SaveSettings", new PluginSettings { ModNotesEnabled = true, ModTodoEnabled = true });
            Check(File.Exists(notes) && File.Exists(todo) &&
                File.Exists(Path.Combine(root, "FCS-Scribe.settings.json")), "enabling work persists separate files and mod-local settings");
            Check(((Label)FcsAdapter.Field(view, "notesPath")).Text.Contains(notes) &&
                ((Label)FcsAdapter.Field(view, "todoPath")).Text.Contains(todo) &&
                !view.Notes.ReadOnly && view.Todos.Enabled &&
                !information.Text.Contains("Enable Scribe notes") && !information.Text.Contains("Enable Mod To Do"),
                "enabled tab messages update immediately without duplicating text-feature instructions in the footer");
            view.Notes.Text = "Free-form author notes.\r\nPlain writing.";
            RunWindow(window, "Review");
            Check(File.ReadAllText(notes) == view.Notes.Text &&
                ((Label)FcsAdapter.Field(window, "status")).Text.Contains("No tracking project"),
                "Save Changes persists notes even without a tracking project and does not imply a native save");
            var list = (ListView)FcsAdapter.Field(FcsAdapter.Field(editor, "nav"), "listView1");
            Set(list, "Source", data);
            var records = ((IDictionary)FcsAdapter.Field(data, "items")).Values.Cast<object>().Take(2).ToList();
            foreach (object item in records) FcsAdapter.Call(list, "AddItem", item);
            list.CreateControl();
            IntPtr handle = list.Handle;
            list.SelectedIndices.Add(0);
            if (records.Count > 1) list.SelectedIndices.Add(1);
            var selected = adapter.SelectedModTodos();
            Check(selected.Count == records.Count && selected.All(x => !String.IsNullOrWhiteSpace(x.RecordId)),
                "the adapter resolves actual native multi-selection into stable task targets");
            FcsAdapter.NativeTodoItem(editor).Enabled = true;
            FcsAdapter.Call(hook, "Opening", menu, new System.ComponentModel.CancelEventArgs());
            Check(hook.Item.Available && hook.Item.Enabled, "the native record-menu entry follows the saved opt-in setting and selection");
            window.AddModTodos(selected, "Adjust this record.");
            Check(view.Work.Todos.Count == selected.Count && File.ReadAllText(todo).Contains(selected[0].RecordId),
                "record-linked tasks save in todo.txt, separate from free-form notes");
            view.Todos.SetItemChecked(0, true);
            Check(new ModWork(root, adapter.ActiveModName(), true, true).Todos[0].Done, "checking a task persists completion");
            view.Notes.Text = "Unsaved local text";
            File.WriteAllText(notes, "External author edit");
            Reject<IOException>(() => RunWindow(window, "Review"), "external notes edits block action saves rather than overwriting either intent");
            Check(view.Notes.Text == "Unsaved local text" && File.ReadAllText(notes) == "External author edit",
                "a conflict retains local editor text and external bytes");
            string taskText = File.ReadAllText(todo);
            File.WriteAllText(todo, "Malformed external task file");
            view.Load(root, adapter.ActiveModName(), (PluginSettings)FcsAdapter.Field(window, "settings"));
            Check(view.LoadError != null && view.Work == null && view.Notes.ReadOnly && !view.Todos.Enabled &&
                !((Button)FcsAdapter.Field(view, "edit")).Enabled && !((Button)FcsAdapter.Field(view, "remove")).Enabled &&
                view.Notes.Text == "Unsaved local text", "failed reload preserves editor text and disables every task mutation control");
            File.WriteAllText(todo, taskText);
            view.Load(root, adapter.ActiveModName(), (PluginSettings)FcsAdapter.Field(window, "settings"));
            string foreign = Path.Combine(root, "foreign-project"); Directory.CreateDirectory(foreign);
            string foreignSettings = Path.Combine(foreign, "FCS-Scribe.settings.json");
            SettingsStorage.Save(foreignSettings, new PluginSettings { ProjectModId = "another-mod" }, null, false);
            string foreignHash = TrackingStorage.HashFile(foreignSettings);
            Reject<InvalidDataException>(() => RunWindow(window, "SaveSettings", new PluginSettings { ProjectDirectory = foreign }),
                "an external root owned by another mod cannot be adopted");
            Check(TrackingStorage.HashFile(foreignSettings) == foreignHash &&
                ProjectLocation.Open(root, adapter.ActiveModId()).Directory == root, "foreign settings rejection retains destination bytes and the original association");
            string external = Path.Combine(Directory.GetCurrentDirectory(), "external-project"); Directory.CreateDirectory(external);
            using (var lease = new FileStream(Path.Combine(root, "FCS-Scribe.location.json.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                Reject<IOException>(() => RunWindow(window, "SaveSettings", new PluginSettings {
                    ModNotesEnabled = true, ModTodoEnabled = true, ProjectDirectory = external }),
                    "a held locator lease rejects an external root switch");
            Check(ProjectLocation.Open(root, adapter.ActiveModId()).Directory == root &&
                (string)FcsAdapter.Field(window, "settingsPath") == Path.Combine(root, "FCS-Scribe.settings.json") &&
                File.Exists(Path.Combine(external, "FCS-Scribe.settings.json")),
                "a partial root switch retains old window association and safely written candidate settings for retry");
            RunWindow(window, "SaveSettings", new PluginSettings { ModNotesEnabled = true, ModTodoEnabled = true, ProjectDirectory = external });
            Check(ProjectLocation.Open(root, adapter.ActiveModId()).Directory == external &&
                File.ReadAllText(notes) == "External author edit" && File.Exists(Path.Combine(external, "FCS-Scribe.settings.json")),
                "external mode moves default destinations through a locator, not old content or tracking history");
            view.Notes.Text = "External project note";
            RunWindow(window, "Review");
            Check(File.ReadAllText(Path.Combine(external, "notes.txt")) == "External project note" &&
                (string)FcsAdapter.Call(window, "ExportDirectory") == external, "external project root is used for work and default new exports");
            RunWindow(window, "StartPath", Path.Combine(external, "work-tracking.json"));
            view.Notes.Text = "Saved with tracked changes";
            RunWindow(window, "Review");
            Check(File.ReadAllText(Path.Combine(external, "notes.txt")) == "Saved with tracked changes" &&
                ((JournalProject)FcsAdapter.Field(window, "project")).EntryCount == 0,
                "Save Changes persists work alongside tracking without inventing gameplay recordings");
            RunWindow(window, "SaveSettings", new PluginSettings {
                ModNotesEnabled = true, ModTodoEnabled = true, ProjectDirectory = external, ReviewSeconds = 30 });
            Set(FcsAdapter.Field(window, "schedule"), "NextReview", 0L);
            view.Notes.Text = "Saved on automatic recording";
            RunWindow(window, "AutomaticTick");
            Check(File.ReadAllText(Path.Combine(external, "notes.txt")) == "Saved on automatic recording",
                "an eligible automatic recording action also persists enabled work");
            RunWindow(window, "SaveSettings", new PluginSettings { ProjectDirectory = external });
            FcsAdapter.Call(hook, "Opening", menu, new System.ComponentModel.CancelEventArgs());
            Check(!hook.Item.Available && File.ReadAllText(Path.Combine(external, "notes.txt")) == "Saved on automatic recording",
                "disabling features hides the extension without deleting either text file");
            RunWindow(window, "SaveSettings", new PluginSettings { ModNotesEnabled = true, ModTodoEnabled = true, ProjectDirectory = external });
            view.Notes.Text = "Saved on close";
            var closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
            FcsAdapter.Call(window, "OnFormClosing", closing);
            Check(!closing.Cancel && File.ReadAllText(Path.Combine(external, "notes.txt")) == "Saved on close",
                "normal Scribe close persists enabled free-form notes");
        }
        using (var reopened = new ScribeWindow(adapter, editor))
            Check(((ModWorkView)FcsAdapter.Field(reopened, "modWork")).Notes.Text == "Saved on close",
                "a fresh window discovers external settings and restores text through the mod-local locator");
        Check(TrackingStorage.HashFile(modPath) == original && adapter.Capture().Fingerprint() == fingerprint &&
            (bool)FcsAdapter.Property(FcsAdapter.Field(editor, "nav"), "HasChanges") == dirty &&
            (nativeHash == null ? !File.Exists(nativeFile) : TrackingStorage.HashFile(nativeFile) == nativeHash),
            "Scribe notes/tasks never save or mutate native gameplay or the shared FCS notes file");
    }

    private static void CheckCompatibility(FcsAdapter adapter, Form editor, object data, object nav,
        Assembly assembly, string modPath)
    {
        string originalHash = TrackingStorage.HashFile(modPath);
        bool dirty = (bool)FcsAdapter.Property(nav, "HasChanges");
        Check(TrackingStorage.HashFile(assembly.Location) == FcsAdapter.SupportedHash,
            "the tested native executable matches the exact supported hash, not merely its version label");
        Reject<NotSupportedException>(() => FcsAdapter.CheckVersion(typeof(IntegrationTests).Assembly),
            "a different executable is rejected by the hash guard");
        Reject<NotSupportedException>(() => new FcsAdapter(typeof(IntegrationTests).Assembly, null),
            "constructor rejects an unsupported assembly before any editor reflection");
        Put(editor, "mergeModsMode", true);
        try { Reject<NotSupportedException>(() => adapter.Capture(), "merge mode cannot use the normal editing adapter"); }
        finally { Put(editor, "mergeModsMode", false); }
        Type fileMode = assembly.GetType("forgotten_construction_set.navigation+ModFileMode", true);
        FcsAdapter.Call(nav, "setActiveFilename", Path.ChangeExtension(modPath, ".translation"), Enum.Parse(fileMode, "USER"));
        try { Reject<NotSupportedException>(() => adapter.Capture(), "non-mod filenames cannot bypass the editing guard"); }
        finally { FcsAdapter.Call(nav, "setActiveFilename", modPath, Enum.Parse(fileMode, "USER")); }
        object ou = FcsAdapter.Field(nav, "ou");
        Put(ou, "gameData", Activator.CreateInstance(data.GetType()));
        try { Reject<InvalidOperationException>(() => adapter.Capture(), "an old adapter rejects a replaced native data session"); }
        finally { Put(ou, "gameData", data); }

        Snapshot initial = adapter.Capture();
        Check(initial.LoadOrder.SequenceEqual(((IEnumerable)FcsAdapter.Field(editor, "activeMods")).Cast<string>()) &&
            initial.Dependencies.SequenceEqual(((IEnumerable)FcsAdapter.Field(FcsAdapter.Field(data, "header"), "Dependencies")).Cast<string>()) &&
            initial.LockedMods.SequenceEqual(((IEnumerable)FcsAdapter.Field(data, "lockedMods")).Cast<string>()),
            "loaded order, declared dependencies and locked context retain their separate native meanings");
        var baseCells = initial.Records.SelectMany(x => x.Cells).Where(x => x.Layer == "base").ToList();
        Check(baseCells.Count > 0 && baseCells.All(x => x.Source == "base:unresolved") &&
            initial.Warnings.Any(x => x.Contains("field-level source mod provenance is unresolved")),
            "flattened inherited fields/names/references retain unresolved ownership even with named dependencies");
        var tracker = new Tracker(initial);
        Check(tracker.Export(adapter.Capture()).Changes.Count == 0, "a loaded dependency stack is not reported as new gameplay edits");
        IDictionary records = (IDictionary)FcsAdapter.Field(data, "items");
        string id = initial.Records.First(x => x.Cells.Any(c => c.Path == "/name" && c.Layer == "base")).Id;
        object item = records[id];
        string activeName = (string)FcsAdapter.Field(item, "modName");
        string baseName = (string)FcsAdapter.Field(item, "baseName");
        try
        {
            Set(item, "Name", (string)FcsAdapter.Property(item, "Name") + " S16 active edit");
            Packet packet = tracker.Export(adapter.Capture());
            Check(packet.Changes.Any(x => x.RecordId == id && x.Layer == "active" &&
                x.AfterSource == initial.ModName), "active name evidence identifies only the writable active mod");
        }
        finally { Put(item, "modName", activeName); }
        try
        {
            Put(item, "baseName", baseName + " S16 inherited change");
            Reject<InvalidOperationException>(() => tracker.Export(adapter.Capture()),
                "changed inherited dependency values cannot become active-mod evidence");
        }
        finally { Put(item, "baseName", baseName); }
        IList order = (IList)FcsAdapter.Field(editor, "activeMods");
        object first = order[0], second = order[1];
        try
        {
            order[0] = second; order[1] = first;
            Reject<InvalidOperationException>(() => tracker.Export(adapter.Capture()),
                "changed reported load order rejects instead of silently rebasing the history");
        }
        finally { order[0] = first; order[1] = second; }
        Check(tracker.Export(adapter.Capture()).Changes.Count == 0,
            "restoring the exact native context and test edits preserves the original no-op baseline");

        string lockedPath = Path.Combine(Directory.GetCurrentDirectory(), "s16-locked.mod");
        File.Copy(Path.Combine(Directory.GetCurrentDirectory(), "fixture.mod"), lockedPath);
        Type mode = assembly.GetType("forgotten_construction_set.GameData+ModMode", true);
        Check((bool)FcsAdapter.Call(data, "load", lockedPath, Enum.Parse(mode, "LOCKED"), false, false),
            "load a controlled copied overlay through the actual native LOCKED mode");
        FcsAdapter.Call(data, "resolveAllReferences");
        order.Add(Path.GetFileName(lockedPath));
        Snapshot locked = adapter.Capture();
        var lockedCells = locked.Records.SelectMany(x => x.Cells).Where(x => x.Layer == "locked").ToList();
        Check(locked.LockedMods.Contains(lockedPath) && lockedCells.Count > 0 &&
            lockedCells.All(x => x.Source == "locked:unresolved"),
            "native locked overlay fields/names/references retain unresolved source ownership");
        Check(locked.Records.SelectMany(x => x.Cells).Where(x => x.Layer == "effective" &&
            x.Source == "locked:unresolved").Any(), "effective locked values do not infer a source filename from load order");
        Reject<InvalidOperationException>(() => tracker.Export(locked),
            "adding a real locked overlay requires a fresh tracking context");
        var lockedTracker = new Tracker(locked);
        Check(lockedTracker.Export(adapter.Capture()).Changes.Count == 0,
            "an explicitly fresh locked-stack baseline does not invent gameplay changes");
        string lockedId = locked.Records.First(x => x.Cells.Any(c => c.Layer == "locked")).Id;
        object lockedItem = records[lockedId];
        string lockedName = (string)FcsAdapter.Field(lockedItem, "lockedName");
        try
        {
            Put(lockedItem, "lockedName", lockedName + " S16 locked change");
            Reject<InvalidOperationException>(() => lockedTracker.Export(adapter.Capture()),
                "locked-layer changes cannot be attributed to the active mod");
        }
        finally { Put(lockedItem, "lockedName", lockedName); }
        IList lockedMods = (IList)FcsAdapter.Field(data, "lockedMods");
        object lockedFile = lockedMods[0];
        try
        {
            lockedMods.RemoveAt(0);
            Reject<InvalidOperationException>(() => lockedTracker.Export(adapter.Capture()),
                "changed reported locked context rejects even when captured values still match");
        }
        finally { lockedMods.Insert(0, lockedFile); }
        Check(lockedTracker.Export(adapter.Capture()).Changes.Count == 0 &&
            TrackingStorage.HashFile(modPath) == originalHash &&
            TrackingStorage.HashFile(lockedPath) == TrackingStorage.HashFile(Path.Combine(Directory.GetCurrentDirectory(), "fixture.mod")) &&
            (bool)FcsAdapter.Property(nav, "HasChanges") == dirty,
            "capture/context guards preserve disk mod bytes, controlled overlay bytes and editor dirty state");
    }

    private static void CheckNotesSafetyWindow(FcsAdapter adapter, Form editor, Assembly assembly, string modPath)
    {
        string directory = Directory.GetCurrentDirectory();
        object nav = FcsAdapter.Field(editor, "nav"), todo = FcsAdapter.Field(editor, "todoList");
        object data = FcsAdapter.Field(FcsAdapter.Field(nav, "ou"), "gameData");
        var records = (IDictionary)FcsAdapter.Field(data, "items");
        while (adapter.Capture().Records.Count < 3)
        {
            object created = FcsAdapter.Call(data, "createItem", FcsAdapter.Property(records.Values.Cast<object>().First(), "type"));
            Set(created, "Name", "S15 disposable fixture record");
        }
        var ids = adapter.Capture().Records.Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal).Take(3).ToList();
        Check(ids.Count == 3, "notes-only fixtures contain three native records, creating disposable records when needed");
        Type noteType = assembly.GetType("forgotten_construction_set.ToDo+NotesItem+Type", true);
        string notesFile = adapter.NotesFile();
        string inheritedFile = Path.Combine(directory, "data", "__TODO__scribe-s15-other.txt");
        FcsAdapter.Call(todo, "AddItem", records[ids[0]], "S15 inherited TODO.", Enum.Parse(noteType, "TODO"), null, false, false);
        FcsAdapter.Call(todo, "AddItem", records[ids[2]], "S15 unrelated shared-file annotation.",
            Enum.Parse(noteType, "NOTE"), null, false, false);
        Put(todo, "activeFile", inheritedFile);
        try { FcsAdapter.Call(todo, "SaveData"); }
        finally { Put(todo, "activeFile", notesFile); }
        FcsAdapter.Call(todo, "LoadAll");
        FcsAdapter.Call(todo, "AddItem", records[ids[0]], "S15 existing TODO.", Enum.Parse(noteType, "TODO"), null, true, false);
        Set(nav, "HasChanges", true);
        Check((bool)FcsAdapter.Call(editor, "saveFile"), "explicit initial native Save normalizes dependencies and existing notes");
        adapter = ReopenRecoveryModel(editor, assembly, modPath);
        FcsAdapter.Call(todo, "LoadAll");
        data = FcsAdapter.Field(FcsAdapter.Field(nav, "ou"), "gameData");
        records = (IDictionary)FcsAdapter.Field(data, "items");
        string inheritedHash = TrackingStorage.HashFile(inheritedFile);
        Snapshot initial = adapter.Capture();
        foreach (string id in ids.Take(2))
            Set(records[id], "Name", (string)FcsAdapter.Property(records[id], "Name") + " S15");
        Set(nav, "HasChanges", true);
        Check((bool)FcsAdapter.Call(editor, "saveFile"), "explicit Save settles gameplay/dependency metadata before exporting notes evidence");
        adapter = ReopenRecoveryModel(editor, assembly, modPath);
        FcsAdapter.Call(todo, "LoadAll");
        Snapshot documented = adapter.Capture();
        Packet packet = new Tracker(initial).Export(documented);
        NoteDraft draft = Json.Read<NoteDraft>(Json.Write(packet.ResponseTemplate));
        foreach (NoteEntry entry in draft.Entries) entry.Text = "S15 approved prose for " + entry.TargetRecordId + ".";
        Check(draft.Entries.Count == 2, "native notes fixture uses two Name edits without a numeric-field prerequisite");
        string first = draft.Entries[0].TargetRecordId, second = draft.Entries[1].TargetRecordId;
        string firstBefore = adapter.Notes(first), secondBefore = adapter.Notes(second);
        string savedModHash = TrackingStorage.HashFile(modPath);
        var diskNotes = Directory.GetFiles(Path.Combine(directory, "data"), "__TODO__*.txt")
            .ToDictionary(x => x, TrackingStorage.HashFile);
        string settings = Path.Combine(directory, "notes-settings.json"), workspace = Path.Combine(directory, "notes-workspace.json");
        using (var window = new ScribeWindow(adapter, editor, settings))
        {
            FcsAdapter.Call(window, "ShowChanges", packet);
            Put(window, "exported", packet);
            ((TextBox)FcsAdapter.Field(window, "draft")).Text = Json.Write(draft);
            string approval = null;
            Put(window, "notesDecision", new Func<string, DialogResult>(message => { approval = message; return DialogResult.No; }));
            FcsAdapter.Call(window, "Apply");
            Check(approval != null && approval.Contains(notesFile) && approval.Contains(NoteApplyReport.SidecarNotice) &&
                approval.Contains("2 pending note(s)"), "real approval workflow names the active shared sidecar and backup/distribution boundary");
            Check(adapter.Notes(first) == firstBefore && adapter.Notes(second) == secondBefore,
                "declining approval leaves both native notes unchanged");
            bool destinationRejected = false;
            Put(window, "notesDecision", new Func<string, DialogResult>(message => {
                Put(todo, "activeFile", inheritedFile); return DialogResult.Yes;
            }));
            try { FcsAdapter.Call(window, "Apply"); }
            catch (TargetInvocationException error)
            {
                destinationRejected = error.InnerException is InvalidOperationException &&
                    error.InnerException.Message.Contains("notes file changed during approval");
            }
            finally { Put(todo, "activeFile", notesFile); }
            Check(destinationRejected && adapter.Notes(first) == firstBefore && adapter.Notes(second) == secondBefore,
                "changing the notes destination during approval rejects before either append");
            NoteApplyException stopped = null;
            try
            {
                adapter.Apply(packet, draft, id => { throw new IOException("Injected stop between confirmed record one and record two."); });
            }
            catch (NoteApplyException error) { stopped = error; }
            Check(stopped != null && stopped.InnerException is IOException && stopped.Stage == "reporting completion" &&
                stopped.LastRecordId == first && stopped.Report.CompletedRecordIds.SequenceEqual(new[] { first }) &&
                stopped.Report.UnconfirmedRecordIds.SequenceEqual(new[] { second }) && stopped.Report.SkippedRecordIds.Count == 0,
                "an injected boundary failure after a real native append reports exact completed/unconfirmed targets and cause");
            Check(adapter.Notes(first).Contains(DraftValidation.NoteText(packet.BatchId, draft.Entries[0]).Trim()) &&
                adapter.Notes(second) == secondBefore && (bool)FcsAdapter.Property(nav, "HasChanges"),
                "partial application retains the completed native note, untouched next target and dirty state");
            Check(TrackingStorage.HashFile(modPath) == savedModHash && diskNotes.All(x => TrackingStorage.HashFile(x.Key) == x.Value) &&
                diskNotes.Count == Directory.GetFiles(Path.Combine(directory, "data"), "__TODO__*.txt").Length,
                "partial application never saves mod or shared notes files");
            FcsAdapter.Call(window, "SetActionFailure", true);
            FcsAdapter.Call(window, "ShowNoteFailure", stopped);
            Check(((TextBox)FcsAdapter.Field(window, "review")).Text.Contains(first) &&
                ((TextBox)FcsAdapter.Field(window, "review")).Text.Contains(second) &&
                ((Label)FcsAdapter.Field(window, "status")).Text.Contains("unconfirmed: 1") &&
                (bool)FcsAdapter.Field(window, "automaticPaused") &&
                ((TextBox)FcsAdapter.Field(window, "draft")).Text == Json.Write(draft),
                "partial-failure presentation retains per-record details, paused automation and the complete original draft");
            RunWindow(window, "SaveWorkspacePath", workspace);
        }
        string partialFirst = adapter.Notes(first);
        Check((bool)FcsAdapter.Call(editor, "saveFile"), "only an explicit normal Save persists the inspected partial notes");
        string partialModHash = TrackingStorage.HashFile(modPath);
        adapter = ReopenRecoveryModel(editor, assembly, modPath);
        FcsAdapter.Call(todo, "LoadAll");
        Snapshot reloadedNotes = adapter.Capture();
        Check(reloadedNotes.Fingerprint() == packet.SourceFingerprint,
            "fresh native model/notes reload keeps the original batch current");
        Check(adapter.Notes(first).Contains("S15 inherited TODO.") && adapter.Notes(first).Contains("S15 existing TODO.") &&
            adapter.Notes(first).Split(new[] { DraftValidation.Marker(packet.BatchId) }, StringSplitOptions.None).Length == 2,
            "native Save/reload retains existing/inherited text and exactly one completed marker despite formatting normalization");
        partialFirst = adapter.Notes(first);
        Check(adapter.Notes(second) == secondBefore,
            "fresh native notes reload preserves the untouched target bytes");
        Check(DraftValidation.Pending(packet, draft, adapter.Capture(), adapter.Notes).Single().TargetRecordId == second,
            "the original complete draft skips only the exact persisted completed block after restart");
        object resumedData = FcsAdapter.Field(FcsAdapter.Field(nav, "ou"), "gameData");
        FcsAdapter.Call(todo, "AddItem", ((IDictionary)FcsAdapter.Field(resumedData, "items"))[first],
            DraftValidation.NoteText(packet.BatchId, draft.Entries[0]), Enum.Parse(noteType, "NOTE"), null, true, false);
        string ambiguous = adapter.Notes(first);
        Reject<InvalidOperationException>(() => adapter.Apply(packet, draft), "duplicate native batch markers reject an ambiguous retry before the pending target");
        Check(adapter.Notes(first) == ambiguous && adapter.Notes(second) == secondBefore &&
            TrackingStorage.HashFile(modPath) == partialModHash && TrackingStorage.HashFile(inheritedFile) == inheritedHash,
            "ambiguous retry preserves both notes and saved mod/other-author sidecar bytes");
        FcsAdapter.Call(todo, "LoadAll");
        Check(adapter.Notes(first) == partialFirst, "discarding the deliberately injected unsaved duplicate reloads the exact saved completed note");
        using (var window = new ScribeWindow(adapter, editor, settings))
        {
            RunWindow(window, "OpenWorkspacePath", workspace);
            string approval = null;
            Put(window, "notesDecision", new Func<string, DialogResult>(message => { approval = message; return DialogResult.Yes; }));
            FcsAdapter.Call(window, "Apply");
            Check(approval.Contains("1 pending note(s)") && approval.Contains("1 matching completed") &&
                adapter.Notes(first) == partialFirst && adapter.Notes(second).Contains("S15 approved prose"),
                "reapproved workspace retry skips the persisted completed target and appends only the pending one");
            Check(((Label)FcsAdapter.Field(window, "status")).Text.Contains("previously completed/skipped: 1") &&
                ((TextBox)FcsAdapter.Field(window, "review")).Text.Contains("unconfirmed: 0"),
                "successful UI retry reports new/skipped outcomes without claiming a save");
            Reject<InvalidOperationException>(() => adapter.Apply(packet, draft), "fully completed batches still reject repeat application");
        }
        Check((bool)FcsAdapter.Call(editor, "saveFile"), "explicit final native Save persists the retry");
        adapter = ReopenRecoveryModel(editor, assembly, modPath);
        FcsAdapter.Call(todo, "LoadAll");
        Check(adapter.Notes(first) == partialFirst && adapter.Notes(second).Contains("S15 approved prose") &&
            adapter.Notes(first).Contains("S15 inherited TODO.") && adapter.Notes(first).Contains("S15 existing TODO."),
            "normal Save/fresh reload retains retry results and inherited/existing notes");
        Check(TrackingStorage.HashFile(inheritedFile) == inheritedHash &&
            adapter.Notes(ids[2]).Contains("S15 unrelated shared-file annotation."),
            "the other-author sidecar and unrelated shared annotation remain unchanged");
        Check(FcsAdapter.Field(((IDictionary)FcsAdapter.Field(todo, "items"))[first], "type").ToString() == "TODO" &&
            !(bool)FcsAdapter.Property(nav, "HasChanges"), "retry preserves TODO category and only normal Save/reload clears dirty state");
    }

    private static void CheckNotesReopened(FcsAdapter adapter, string modPath)
    {
        var workspace = DraftWorkspace.Load(Path.Combine(Directory.GetCurrentDirectory(), "notes-workspace.json"));
        NoteDraft draft = Json.Read<NoteDraft>(workspace.DraftText);
        Check(adapter.Capture().Fingerprint() == workspace.Packet.SourceFingerprint,
            "a new native host process loads the saved gameplay under the original packet fingerprint");
        foreach (NoteEntry entry in draft.Entries)
            Check(adapter.Notes(entry.TargetRecordId).Split(new[] { DraftValidation.Marker(workspace.Packet.BatchId) },
                StringSplitOptions.None).Length == 2 && adapter.Notes(entry.TargetRecordId).Contains(entry.Text),
                "cold-process notes reload retains exactly one completed block per target");
        string first = draft.Entries[0].TargetRecordId;
        Check(adapter.Notes(first).Contains("S15 inherited TODO.") && adapter.Notes(first).Contains("S15 existing TODO."),
            "cold-process reload preserves inherited/existing notes");
        string modHash = TrackingStorage.HashFile(modPath);
        var hashes = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "data"), "__TODO__*.txt")
            .ToDictionary(x => x, TrackingStorage.HashFile);
        Reject<InvalidOperationException>(() => adapter.Apply(workspace.Packet, draft),
            "cold-process retry still rejects an already fully completed batch");
        Check(TrackingStorage.HashFile(modPath) == modHash && hashes.All(x => TrackingStorage.HashFile(x.Key) == x.Value),
            "cold-process duplicate rejection does not save or change sidecars");
    }

    private static void CheckPortabilityWindow(FcsAdapter adapter, Form editor, Assembly assembly, string modPath)
    {
        string directory = Directory.GetCurrentDirectory();
        string original = Path.Combine(directory, "mods", "portability-original.json");
        string settings = Path.Combine(directory, "portability-settings.json");
        string modHash = TrackingStorage.HashFile(modPath);
        object nav = FcsAdapter.Field(editor, "nav");
        object data = FcsAdapter.Field(FcsAdapter.Field(nav, "ou"), "gameData");
        object item = ((IDictionary)FcsAdapter.Field(data, "items")).Values.Cast<object>().First();
        string id = (string)FcsAdapter.Property(item, "stringID");
        string name = (string)FcsAdapter.Property(item, "Name"), notes = adapter.Notes(id);
        bool dirty = (bool)FcsAdapter.Property(nav, "HasChanges");
        string projectId, fingerprint;
        using (var window = new ScribeWindow(adapter, editor, settings))
        {
            RunWindow(window, "StartPath", original);
            Set(item, "Name", name + " S14"); Set(nav, "HasChanges", true);
            RunWindow(window, "Review");
            Set(item, "Name", name); Set(nav, "HasChanges", dirty);
            RunWindow(window, "Review"); RunWindow(window, "Compile");
            var project = (JournalProject)FcsAdapter.Field(window, "project");
            projectId = project.ProjectId; fingerprint = project.Fingerprint;
            Check(project.EntryCount == 2 && project.PendingCount == 0,
                "native portability starts with two compiled forward/reverse observations");
        }
        var closedHashes = Directory.GetFiles(original + ".scribe", "*", SearchOption.AllDirectories)
            .Concat(new[] { original }).ToDictionary(x => x.Substring(original.Length), TrackingStorage.HashFile);
        string storage = Path.Combine(directory, "pstore");
        Directory.CreateDirectory(storage);
        string relocated = Path.Combine(storage, "renamed.json");
        File.Move(original, relocated); Directory.Move(original + ".scribe", relocated + ".scribe");
        Check(closedHashes.All(x => TrackingStorage.HashFile(relocated + x.Key) == x.Value),
            "moving/renaming the closed tracking set preserves all bytes without changing the native mod path");
        using (var window = new ScribeWindow(adapter, editor, settings))
        {
            Put(window, "resumeDecision", new Func<Packet, DialogResult>(packet => {
                throw new Exception("Unchanged native mod must not require discrepancy approval after moving tracking storage.");
            }));
            RunWindow(window, "ResumePath", relocated);
            var project = (JournalProject)FcsAdapter.Field(window, "project");
            Check(project.ProjectId == projectId && project.Fingerprint == fingerprint &&
                project.EntryCount == 2 && project.PendingCount == 0,
                "native Resume of relocated tracking storage retains original identity and compiled history");
            RunWindow(window, "Review"); RunWindow(window, "Compile");
            Check(project.EntryCount == 2 && project.PendingCount == 0,
                "unchanged native review/compilation after a tracking-only move adds no duplicate observations");
        }
        var protectedFiles = Directory.GetFiles(relocated + ".scribe", "*", SearchOption.AllDirectories)
            .Concat(new[] { relocated }).ToDictionary(x => x, TrackingStorage.HashFile);
        string movedDirectory = Path.Combine(directory, "moved-mod");
        Directory.CreateDirectory(movedDirectory);
        string movedMod = Path.Combine(movedDirectory, Path.GetFileName(modPath));
        File.Copy(modPath, movedMod);
        Type fileMode = assembly.GetType("forgotten_construction_set.navigation+ModFileMode", true);
        try
        {
            FcsAdapter.Call(nav, "setActiveFilename", movedMod, Enum.Parse(fileMode, "USER"));
            adapter = ReopenRecoveryModel(editor, assembly, movedMod);
            Snapshot moved = adapter.Capture();
            Check(TrackingStorage.HashFile(movedMod) == modHash &&
                moved.ModName == Path.GetFileName(modPath) &&
                moved.ModId == Json.Hash(Path.GetFullPath(movedMod).ToUpperInvariant()) &&
                moved.ModId != Json.Hash(Path.GetFullPath(modPath).ToUpperInvariant()),
                "byte-identical native mod copies with the same filename acquire different path identities");
            using (var window = new ScribeWindow(adapter, editor, settings))
            {
                bool rejected = false, prompted = false;
                Put(window, "resumeDecision", new Func<Packet, DialogResult>(packet => {
                    prompted = true; return DialogResult.Yes;
                }));
                try { RunWindow(window, "ResumePath", relocated); }
                catch (InvalidOperationException error)
                {
                    rejected = error.Message.Contains("New tracking JSON with a new filename");
                }
                Check(rejected && !prompted && FcsAdapter.Field(window, "project") == null,
                    "native moved-mod Resume rejects before discrepancy approval and explains the fresh-baseline route");
                Check(protectedFiles.Count == Directory.GetFiles(relocated + ".scribe", "*", SearchOption.AllDirectories).Length + 1 &&
                    protectedFiles.All(x => TrackingStorage.HashFile(x.Key) == x.Value),
                    "rejected native relocation leaves every old tracking file unchanged");
                string fresh = Path.Combine(storage, "new.json");
                RunWindow(window, "StartPath", fresh);
                var project = (JournalProject)FcsAdapter.Field(window, "project");
                Check(project.ProjectId != projectId && project.EntryCount == 0 && project.PendingCount == 0 &&
                    project.State.ModId == moved.ModId && project.Fingerprint == moved.Fingerprint(),
                    "explicit New at a fresh filename captures the moved native mod without importing old history");
                RunWindow(window, "Review");
                Check(project.EntryCount == 0 && protectedFiles.All(x => TrackingStorage.HashFile(x.Key) == x.Value),
                    "fresh-project no-op review neither backfills old edits nor rewrites the old branch");
            }
            Check(TrackingStorage.HashFile(modPath) == modHash && TrackingStorage.HashFile(movedMod) == modHash &&
                adapter.Notes(id) == notes && !(bool)FcsAdapter.Property(nav, "HasChanges"),
                "native relocation/new-baseline checks never save mod bytes or change notes/dirty state");
        }
        finally
        {
            FcsAdapter.Call(nav, "setActiveFilename", modPath, Enum.Parse(fileMode, "USER"));
            ReopenRecoveryModel(editor, assembly, modPath);
        }
    }

    private static FcsAdapter ReopenRecoveryModel(Form editor, Assembly assembly, string modPath)
    {
        object nav = FcsAdapter.Field(editor, "nav"), ou = FcsAdapter.Field(nav, "ou");
        object reloaded = Activator.CreateInstance(FcsAdapter.Field(ou, "gameData").GetType());
        Type mode = assembly.GetType("forgotten_construction_set.GameData+ModMode", true);
        Check((bool)FcsAdapter.Call(reloaded, "load", Path.Combine(Directory.GetCurrentDirectory(), "fixture.mod"),
            Enum.Parse(mode, "BASE"), false, false), "reload disposable dependency through native FCS");
        Check((bool)FcsAdapter.Call(reloaded, "load", modPath, Enum.Parse(mode, "ACTIVE"), false, false),
            "reload disposable saved active mod through native FCS");
        FcsAdapter.Call(reloaded, "resolveAllReferences");
        Put(ou, "gameData", reloaded); Set(FcsAdapter.Field(editor, "modInfo"), "Data", reloaded);
        Set(nav, "HasChanges", false);
        return new FcsAdapter(assembly, editor);
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
                schedule.Configure(clock.ElapsedMilliseconds - 30000, 30);
                schedule.Reviewed(clock.ElapsedMilliseconds - 30000);
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
                schedule.Configure(clock.ElapsedMilliseconds - 30000, 30);
                schedule.Reviewed(clock.ElapsedMilliseconds - 30000);
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
