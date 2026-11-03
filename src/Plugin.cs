using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FcsScribe
{
    public sealed class Plugin : FCS_extended.IPlugin
    {
        private Assembly assembly;
        private ScribeWindow window;

        public int Init(Assembly fcsAssembly)
        {
            try
            {
                FcsAdapter.CheckVersion(fcsAssembly);
                assembly = fcsAssembly;
                Application.Idle += Attach;
                return 0;
            }
            catch (Exception error)
            {
                ShowError(error);
                return 1;
            }
        }

        private void Attach(object sender, EventArgs args)
        {
            Form editor = Application.OpenForms.Cast<Form>().FirstOrDefault(x =>
                x.GetType().FullName == "forgotten_construction_set.baseForm");
            if (editor == null) return;
            Application.Idle -= Attach;
            try
            {
                var toolbar = (ToolStrip)FcsAdapter.Field(editor, "toolStrip1");
                var button = new ToolStripButton("FCS-Scribe");
                button.Click += delegate {
                    try
                    {
                        if (window == null || window.IsDisposed)
                            window = new ScribeWindow(new FcsAdapter(assembly, editor), editor);
                        window.Show();
                        window.Activate();
                    }
                    catch (Exception error) { ShowError(error); }
                };
                toolbar.Items.Add(new ToolStripSeparator());
                toolbar.Items.Add(button);
            }
            catch (Exception error) { ShowError(error); }
        }

        internal static void ShowError(Exception error)
        {
            Console.Error.WriteLine("FCS-Scribe: " + error);
            MessageBox.Show(error.ToString(), "FCS-Scribe - action failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    internal sealed class ScribeWindow : Form
    {
        private readonly FcsAdapter adapter;
        private Tracker tracker;
        private JournalProject project;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly JournalSchedule schedule = new JournalSchedule();
        private readonly string settingsPath;
        private string settingsHash;
        private PluginSettings settings;
        private bool busy, closing, automaticPaused;
        private readonly Timer timer = new Timer();
        private readonly MenuStrip actions = new MenuStrip { Dock = DockStyle.Top, AutoSize = true,
            AllowMerge = false, CanOverflow = true, TabStop = true };
        private Packet exported;
        private readonly TextBox review = TextArea(true);
        private readonly TextBox draft = TextArea(false);
        private readonly TextBox workshop = TextArea(true);
        private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 48 };
        private const string HelpHint = "Hover over a menu action, or select it with the keyboard, to see what it does.";
        private readonly Label help = new Label { Dock = DockStyle.Bottom, Height = 48, Text = HelpHint,
            BorderStyle = BorderStyle.FixedSingle };

        public ScribeWindow(FcsAdapter adapter, Form editor)
            : this(adapter, editor, PluginSettings.DefaultPath) { }

        internal ScribeWindow(FcsAdapter adapter, Form editor, string settingsFile)
        {
            this.adapter = adapter;
            settingsPath = settingsFile;
            settings = PluginSettings.Load(settingsPath);
            settingsHash = File.Exists(settingsPath) ? TrackingStorage.HashFile(settingsPath) : null;
            if (!editor.IsMdiContainer) throw new NotSupportedException("The FCS main window is not an MDI container.");
            MdiParent = editor;
            Text = "FCS-Scribe - experimental documentation";
            Width = 1080;
            Height = 780;
            var coreMenu = AddMenu("&Core", "Start or resume tracking, review changes, compile the journal or export an AI packet.");
            AddAsyncAction(coreMenu.DropDownItems, "&Start tracking", "Create or resume a tracking project and begin a new timestamped session log. Replaces the editable draft; does not save or restore the mod.", Start);
            AddAsyncAction(coreMenu.DropDownItems, "&Review changes", "Record new changes since the last durable entry into this session log. Automatic intervals are available in Settings.", Review);
            AddAsyncAction(coreMenu.DropDownItems, "&Compile journal", "Consolidate durable session entries into the main journal without duplicates or deleting session files. Automatic mode also compiles every 30 minutes.", Compile);
            AddAction(coreMenu.DropDownItems, "&Export AI packet", "Save change evidence, Workshop context and a blank AI response template as JSON. Replaces the editable draft; sends nothing online.", Export);
            var workshopMenu = AddMenu("&Workshop", "Export local Workshop information or archive the original preview image.");
            AddAction(workshopMenu.DropDownItems, "&Export Workshop info", "Save current local Workshop information and preview-image details as JSON. No tracking baseline or record changes required.", ExportWorkshop);
            AddAction(workshopMenu.DropDownItems, "&Archive preview image", "Copy the local Workshop preview, byte-for-byte, to a new image file outside Kenshi. Does not download or change the original.", ArchivePreview);
            var draftMenu = AddMenu("&Draft", "Import a draft file or paste clipboard text for review before approval.");
            AddAction(draftMenu.DropDownItems, "&Import draft", "Load an AI response JSON into the editable draft. Requires an exported batch; nothing is applied until approval.", Import);
            AddAction(draftMenu.DropDownItems, "&Paste draft", "Replace the editable draft with clipboard text. Review and validate it before applying; nothing is applied by pasting.", delegate { draft.Text = Clipboard.GetText(); });
            AddAction(actions.Items, "&Apply approved notes", "Validate the draft and ask for approval, then append record Notes/TODO entries and mark FCS dirty. Does not save or edit Workshop notes.", Apply);
            AddAsyncAction(actions.Items, "&Settings", "Choose manual or automatic review (30 seconds, 1, 5 or 10 minutes) and the default JSON export folder. Existing tracking files stay at their chosen location.", Settings);
            MainMenuStrip = actions;
            actions.OverflowButton.AccessibleDescription = "Show menu actions that do not fit in the window.";
            actions.OverflowButton.MouseEnter += delegate { help.Text = actions.OverflowButton.AccessibleDescription; };
            actions.OverflowButton.MouseLeave += delegate { UpdateHelp(); };
            actions.OverflowButton.Paint += delegate { if (actions.OverflowButton.Selected) UpdateHelp(); };
            actions.MenuDeactivate += delegate { help.Text = HelpHint; };
            actions.Leave += delegate { help.Text = HelpHint; };
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var changesTab = new TabPage("Changes and warnings");
            changesTab.Controls.Add(review);
            var draftTab = new TabPage("Editable draft JSON");
            draftTab.Controls.Add(draft);
            tabs.TabPages.Add(changesTab);
            tabs.TabPages.Add(draftTab);
            var workshopTab = new TabPage("Workshop info");
            workshopTab.Controls.Add(workshop);
            tabs.TabPages.Add(workshopTab);
            tabs.SelectedIndexChanged += delegate {
                if (tabs.SelectedTab != workshopTab || busy) return;
                try { workshop.Text = Json.Pretty(Json.Write(adapter.CaptureWorkshop())); }
                catch (Exception error) { workshop.Clear(); Plugin.ShowError(error); }
            };
            Controls.Add(tabs);
            Controls.Add(actions);
            Controls.Add(help);
            Controls.Add(status);
            status.Text = "Start or resume a project. Recordings go to timestamped session logs; compiling never saves the mod.";
            timer.Tick += async delegate {
                if (busy || closing || automaticPaused || !Visible || tracker == null ||
                    WindowState == FormWindowState.Minimized ||
                    (MdiParent != null && MdiParent.WindowState == FormWindowState.Minimized) ||
                    actions.OverflowButton.DropDown.Visible ||
                    actions.Items.OfType<ToolStripDropDownItem>().Any(x => x.DropDown.Visible) ||
                    Application.OpenForms.Cast<Form>().Any(x => x.Visible && x.Modal)) return;
                if (!schedule.ReviewDue(clock.ElapsedMilliseconds) && !schedule.CompilationDue(clock.ElapsedMilliseconds)) return;
                try { await RunBusy(AutomaticTick); }
                catch (Exception error) { if (!closing) PauseAfterError(error); }
            };
            FormClosing += delegate { closing = true; timer.Stop(); if (!busy && project != null) project.Dispose(); };
            FormClosed += delegate { timer.Dispose(); };
            Disposed += delegate { closing = true; timer.Dispose(); if (!busy && project != null) project.Dispose(); };
        }

        private async Task Start()
        {
            using (var choice = new TrackingChoiceDialog())
            {
                if (choice.ShowDialog(this) != DialogResult.OK) return;
                if (choice.Resume)
                {
                    using (var picker = new OpenFileDialog { Filter = "Tracking or AI JSON (*.json)|*.json",
                        InitialDirectory = ExportDirectory(), RestoreDirectory = true })
                    {
                        if (picker.ShowDialog(this) != DialogResult.OK) return;
                        status.Text = "Validating saved JSON in the background. Nothing has been overwritten.";
                        string path = picker.FileName;
                        await ResumePath(path);
                    }
                }
                else
                {
                    using (var picker = new SaveFileDialog { Filter = "Tracking JSON (*.json)|*.json",
                        FileName = "fcs-scribe-tracking.json", InitialDirectory = ExportDirectory(),
                        RestoreDirectory = true, OverwritePrompt = false })
                    {
                        if (picker.ShowDialog(this) != DialogResult.OK) return;
                        await StartPath(picker.FileName);
                    }
                }
            }
        }

        private async Task StartPath(string path)
        {
            Snapshot current = adapter.Capture();
            status.Text = "Saving the project baseline and opening a timestamped session.";
            JournalProject candidate = await Task.Run(() => JournalProject.Create(path, current));
            await Begin(candidate, null);
        }

        private async Task ResumePath(string path)
        {
            if (project != null && String.Equals(project.PathName, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            {
                project.Dispose(); project = null; tracker = null;
            }
            JournalProject candidate = null;
            while (candidate == null && !closing)
            {
                LegacyTrackingException legacy = null;
                IncompleteJournalException interrupted = null;
                try { candidate = await Task.Run(() => JournalProject.Open(path)); }
                catch (LegacyTrackingException error) { legacy = error; }
                catch (IncompleteJournalException error) { interrupted = error; }
                if (legacy != null)
                {
                    if (closing || MessageBox.Show(this, "This AI export cannot restore its original baseline. Preserve " +
                        "its exact bytes in .legacy.bak and its evidence, then start from the CURRENT editor state?",
                        "Convert legacy export", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                    Snapshot current = adapter.Capture();
                    candidate = await Task.Run(() => JournalProject.ConvertLegacy(path, current, legacy));
                }
                if (interrupted != null)
                {
                    if (closing || MessageBox.Show(this, interrupted.Message + "\r\nPreserve the original in a recovery backup " +
                        "and discard only its incomplete final line? Complete but invalid entries are never skipped.",
                        "Recover interrupted journal", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                    await Task.Run(() => JournalProject.RecoverTail(path, interrupted));
                }
            }
            if (candidate == null) return;
            try
            {
                if (closing) return;
                Snapshot current = adapter.Capture();
                Packet pending = await Task.Run(() => candidate.ResumeTracker().Export(current));
                if (closing) return;
                if (pending.SourceFingerprint != candidate.Fingerprint &&
                    MessageBox.Show(this, "The editor differs from the last recorded state (" + pending.Changes.Count +
                        " evidence entries). This can include edits lost after a crash or discarded unsaved changes. " +
                        "Continuing will record those differences on the next review; it will NOT restore the mod. Continue?",
                        "Confirm resumed state", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                JournalProject adopted = candidate; candidate = null;
                await Begin(adopted, pending);
            }
            finally { if (candidate != null) candidate.Dispose(); }
        }

        private async Task Begin(JournalProject candidate, Packet pending)
        {
            bool adopted = false;
            try
            {
                if (closing) return;
                await Task.Run(() => candidate.BeginSession());
                if (closing) return;
                if (project != null) project.Dispose();
                project = candidate; tracker = candidate.ResumeTracker(); adopted = true;
                if (pending != null) ShowChanges(pending);
                else review.Clear();
                workshop.Text = Json.Pretty(Json.Write(candidate.State.Workshop));
            }
            finally { if (!adopted) candidate.Dispose(); }
            automaticPaused = false;
            exported = null; draft.Clear();
            schedule.Reset(clock.ElapsedMilliseconds, settings.ReviewSeconds);
            status.Text = "New session: " + project.SessionPath + ". " + ProjectStatus();
        }

        private Packet Compare()
        {
            if (tracker == null) throw new InvalidOperationException("Select Start tracking first.");
            // Moving focus out of the editor commits normal control validation before model capture.
            adapter.EnsureReady();
            return tracker.Export(adapter.Capture());
        }

        private async Task Review()
        {
            if (tracker == null) throw new InvalidOperationException("Select Start tracking first.");
            Snapshot current = adapter.Capture();
            status.Text = "Validating and flushing new entries to the session log.";
            Packet packet = await Task.Run(() => project.Record(current));
            if (closing) return;
            tracker = project.ResumeTracker();
            schedule.Reviewed(clock.ElapsedMilliseconds);
            ShowChanges(packet);
            status.Text = packet.Changes.Count + " evidence entries reviewed; session recording saved/unchanged. " + ProjectStatus();
        }

        private async Task Compile()
        {
            if (project == null) throw new InvalidOperationException("Select Start tracking first.");
            status.Text = "Compiling durable session entries into the main journal.";
            int count = await Task.Run(() => project.Compile());
            if (closing) return;
            schedule.Compiled(clock.ElapsedMilliseconds);
            status.Text = count + " new entries compiled. Session files retained. " + ProjectStatus();
        }

        private async Task AutomaticTick()
        {
            if (schedule.ReviewDue(clock.ElapsedMilliseconds)) await Review();
            if (!closing && schedule.CompilationDue(clock.ElapsedMilliseconds)) await Compile();
        }

        private string ProjectStatus()
        {
            return project.PendingCount + " recordings awaiting compilation. Tracking is NOT a saved-mod backup. " + ScheduleText();
        }

        private void ShowChanges(Packet packet)
        {
            var text = new StringBuilder("PARTIAL EXTRACTION - review all warnings.\r\n");
            foreach (string warning in packet.Context.Warnings) text.AppendLine("WARNING: " + warning);
            text.AppendLine();
            foreach (Change change in packet.Changes)
                text.AppendLine(change.Id + " | " + change.RecordType + " | " + change.RecordId +
                    " | " + change.DisplayName + "\r\n  " + change.Operation + " " + change.Layer +
                    " " + change.Path + "\r\n  " + Json.Write(change.Before) + " -> " + Json.Write(change.After) +
                    (change.ReferencedId == null ? "" : "\r\n  Reference: " + change.ReferencedId +
                    " (" + (change.ReferencedName ?? "UNRESOLVED") + ")"));
            review.Text = text.ToString();
            workshop.Text = Json.Pretty(Json.Write(packet.Context.Workshop));
            status.Text = packet.Changes.Count + " change entries (stored and effective entries may describe the same edit). " +
                "Baseline unchanged.";
        }

        private void ExportWorkshop()
        {
            WorkshopExport export = adapter.CaptureWorkshopExport();
            using (var dialog = new SaveFileDialog { Filter = "JSON metadata (*.json)|*.json", FileName = "fcs-scribe-workshop.json",
                InitialDirectory = ExportDirectory(), RestoreDirectory = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!String.Equals(Path.GetExtension(dialog.FileName), ".json", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Workshop export requires a .json filename.");
                ProtectTrackingFile(dialog.FileName);
                File.WriteAllText(dialog.FileName, Json.Pretty(Json.Write(export)), new UTF8Encoding(false));
            }
            workshop.Text = Json.Pretty(Json.Write(export.Workshop));
            status.Text = "Workshop JSON exported from local data only. Review its source and warnings; use Archive preview image to copy the image separately.";
        }

        private void ArchivePreview()
        {
            WorkshopMetadata metadata = adapter.CaptureWorkshop();
            if (metadata.Preview.Status != "available")
                throw new InvalidOperationException("No local Workshop preview exists. Set an image in FCS's Steam Workshop window first.");
            string extension = metadata.Preview.Format;
            using (var dialog = new SaveFileDialog {
                Filter = "Original preview image (*." + extension + ")|*." + extension,
                FileName = "fcs-scribe-preview." + extension, OverwritePrompt = false,
                Title = "Archive preview image - choose a new filename outside Kenshi", RestoreDirectory = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                PreviewImage archived = adapter.ArchivePreview(dialog.FileName, metadata.Preview.Sha256);
                status.Text = "Preview archived unchanged. SHA-256: " + archived.Sha256;
            }
        }

        private void Export()
        {
            Packet packet = Compare();
            if (packet.Changes.Count == 0 && project.LastEvidence != null &&
                project.LastEvidence.SourceFingerprint == packet.SourceFingerprint) packet = project.LastEvidence;
            if (packet.Changes.Count == 0) throw new InvalidOperationException("No captured changes to document.");
            if (exported != null && !String.IsNullOrWhiteSpace(draft.Text) &&
                MessageBox.Show(this, "Exporting replaces the editable draft with a blank response template. Continue?",
                    "Replace draft", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            using (var dialog = new SaveFileDialog { Filter = "JSON packet (*.json)|*.json", FileName = "fcs-scribe-ai-packet.json",
                InitialDirectory = ExportDirectory(), RestoreDirectory = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!String.Equals(Path.GetExtension(dialog.FileName), ".json", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Export requires a .json filename; mod files are never export targets.");
                ProtectTrackingFile(dialog.FileName);
                File.WriteAllText(dialog.FileName, Json.Pretty(Json.Write(packet)), new UTF8Encoding(false));
            }
            exported = packet;
            draft.Text = Json.Pretty(Json.Write(packet.ResponseTemplate));
            ShowChanges(packet);
            status.Text = "Packet exported. Share it manually with AI, then import/paste its JSON response. " +
                "Mod text may be private; inspect the packet before sharing.";
        }

        private void Import()
        {
            if (exported == null) throw new InvalidOperationException("Export a packet in this tracking session first.");
            using (var dialog = new OpenFileDialog { Filter = "JSON draft (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = ExportDirectory(), RestoreDirectory = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string text = File.ReadAllText(dialog.FileName);
                Json.Read<NoteDraft>(text);
                draft.Text = text;
            }
            status.Text = "Imported as an editable draft, not applied. Review TargetRecordId, Text and ChangeIds before approval.";
        }

        private void Apply()
        {
            if (exported == null) throw new InvalidOperationException("Export a packet before importing/applying notes.");
            NoteDraft notes = Json.Read<NoteDraft>(draft.Text);
            DraftValidation.Check(exported, notes, adapter.Capture(), adapter.Notes);
            if (MessageBox.Show(this, "Approve the edited draft and append its " + notes.Entries.Count +
                " note(s) to FCS? Existing notes are retained. The mod will be marked changed, but NOT saved. " +
                "Multi-record application is not transactional; inspect FCS Notes afterward.",
                "Approve notes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            adapter.Apply(exported, notes);
            status.Text = "Approved notes appended in FCS. Inspect FCS Notes and use FCS's normal Save when ready. Baseline unchanged.";
        }

        private static TextBox TextArea(bool readOnly)
        {
            return new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both,
                WordWrap = false, ReadOnly = readOnly, MaxLength = Int32.MaxValue };
        }

        private string ExportDirectory() { return settings.DirectoryFor(adapter.ActiveModDirectory()); }

        private void ProtectTrackingFile(string path)
        {
            if (project != null && (String.Equals(Path.GetFullPath(path), project.PathName, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFullPath(path).StartsWith(project.DirectoryName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("The active tracking JSON cannot be overwritten by an AI or Workshop export.");
        }

        private async Task Settings()
        {
            using (var dialog = new SettingsDialog(settings))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                PluginSettings next = dialog.Value;
                string hash = await Task.Run(() => {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(settingsPath)));
                    return TrackingStorage.WriteAtomic(settingsPath, next, settingsHash);
                });
                if (closing) return;
                settings = next; settingsHash = hash; automaticPaused = false;
                schedule.Reset(clock.ElapsedMilliseconds, settings.ReviewSeconds);
                status.Text = "Settings saved. Existing tracking files are not moved. " + ScheduleText();
            }
        }

        private string ScheduleText()
        {
            return automaticPaused ? "Automatic review paused after an error; resume or save settings to re-enable." :
                settings.ReviewSeconds == 0 ? "Manual recording/compilation." : "Automatic recording: " +
                    settings.ReviewSeconds + " seconds; compilation: 30 minutes while Scribe is visible.";
        }

        private void ConfigureTimer()
        {
            timer.Stop();
            if (closing || automaticPaused || tracker == null || settings.ReviewSeconds == 0) return;
            timer.Interval = 1000;
            timer.Start();
        }

        private async Task RunBusy(Func<Task> action)
        {
            if (busy || closing) return;
            busy = true; actions.Enabled = false; timer.Stop();
            try { await action(); }
            finally
            {
                busy = false;
                if (!closing) { actions.Enabled = true; ConfigureTimer(); }
                else if (project != null) project.Dispose();
            }
        }

        private void PauseAfterError(Exception error)
        {
            automaticPaused = true; timer.Stop();
            status.Text = "Action failed; automatic review paused. The baseline advances only after a successful checkpoint save.";
            Plugin.ShowError(error);
        }

        private ToolStripMenuItem AddMenu(string label, string description)
        {
            ToolStripMenuItem item = CreateMenuItem(label, description);
            item.DropDownClosed += delegate { UpdateHelp(); };
            actions.Items.Add(item);
            return item;
        }

        private ToolStripMenuItem CreateMenuItem(string label, string description)
        {
            var item = new ToolStripMenuItem(label) {
                AccessibleName = label.Replace("&", ""), AccessibleDescription = description,
                Overflow = ToolStripItemOverflow.AsNeeded
            };
            item.MouseEnter += delegate { help.Text = description; };
            item.MouseLeave += delegate { UpdateHelp(); };
            // ToolStrip items have no focus event; keyboard selection repaints the selected item.
            item.Paint += delegate { if (item.Selected) UpdateHelp(); };
            return item;
        }

        private void UpdateHelp()
        {
            var items = actions.Items.Cast<ToolStripItem>();
            ToolStripItem selected = items.FirstOrDefault(x => x is ToolStripDropDownItem &&
                ((ToolStripDropDownItem)x).DropDown.Visible) ?? items.FirstOrDefault(x => x.Selected) ??
                (actions.OverflowButton.Selected || actions.OverflowButton.DropDown.Visible ? actions.OverflowButton : null);
            var menu = selected as ToolStripDropDownItem;
            while (menu != null && menu.DropDown.Visible)
            {
                ToolStripItem child = menu.DropDownItems.Cast<ToolStripItem>().FirstOrDefault(x => x.Selected);
                if (child == null) break;
                selected = child;
                menu = child as ToolStripDropDownItem;
            }
            string text = selected == null ? HelpHint : selected.AccessibleDescription;
            if (help.Text != text) help.Text = text;
        }

        private void AddAction(ToolStripItemCollection parent, string label, string description, Action action)
        {
            AddAsyncAction(parent, label, description, () => { action(); return Task.FromResult(0); });
        }

        private void AddAsyncAction(ToolStripItemCollection parent, string label, string description, Func<Task> action)
        {
            ToolStripMenuItem item = CreateMenuItem(label, description);
            item.Click += async delegate {
                try { await RunBusy(action); }
                catch (Exception error) { if (!closing) PauseAfterError(error); }
            };
            parent.Add(item);
        }
    }
}
