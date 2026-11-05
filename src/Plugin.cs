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
                Func<ScribeWindow> open = () => {
                    var adapter = new FcsAdapter(assembly, editor);
                    if (window == null || window.IsDisposed || window.ModId != adapter.ActiveModId())
                        window = new ScribeWindow(adapter, editor);
                    window.Show(); window.Activate(); return window;
                };
                button.Click += delegate {
                    try { open(); }
                    catch (Exception error) { ShowError(error); }
                };
                toolbar.Items.Add(new ToolStripSeparator());
                toolbar.Items.Add(button);
                var todoMenu = new ModTodoMenu(assembly, editor, open);
                editor.FormClosed += delegate { todoMenu.Dispose(); };
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
        private string settingsPath;
        private ProjectLocation location;
        private readonly ModWorkView modWork;
        internal readonly string ModId;
        private readonly string modName;
        private TabControl tabs;
        private string settingsHash;
        private PluginSettings settings;
        private Exception settingsError;
        private bool busy, closing, automaticPaused;
        private readonly Timer timer = new Timer();
        private readonly MenuStrip actions = new MenuStrip { Dock = DockStyle.Top, AutoSize = true,
            AllowMerge = false, CanOverflow = true, TabStop = true };
        private Packet exported;
        private string workspacePath, workspaceHash, savedDraftText;
        private Packet savedPacket;
        private long draftRevision;
        private long? approvedCloseRevision;
        private readonly Func<string, DialogResult> draftDecision;
        private readonly Func<Packet, DialogResult> resumeDecision;
        private readonly Func<string, DialogResult> notesDecision;
        private readonly TextBox review = TextArea(true);
        private readonly TextBox draft = TextArea(false);
        private readonly TextBox workshop = TextArea(true);
        private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 48,
            AccessibleName = "Plugin activity and results" };
        private string settingsNotice, compilationNotice;
        private ToolStripItem hoveredAction;
        private bool menuHelpActive;
        private readonly TextBox help = new TextBox { Dock = DockStyle.Bottom, Height = 100,
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            AccessibleName = "Warnings, information and contextual help" };

        public ScribeWindow(FcsAdapter adapter, Form editor)
            : this(adapter, editor, ProjectLocation.Open(adapter.ActiveModDirectory(), adapter.ActiveModId())) { }

        private ScribeWindow(FcsAdapter adapter, Form editor, ProjectLocation projectLocation)
            : this(adapter, editor, projectLocation.SettingsPath)
        {
            location = projectLocation;
            if (settingsError == null && !File.Exists(settingsPath) && File.Exists(PluginSettings.DefaultPath))
            {
                settingsNotice = "MOD-LOCAL SETTINGS: earlier user-wide settings are retained, not silently imported. " +
                    "Configure this mod in Settings.";
                UpdateHelp();
            }
        }

        internal ScribeWindow(FcsAdapter adapter, Form editor, string settingsFile)
        {
            this.adapter = adapter;
            ModId = adapter.ActiveModId(); modName = adapter.ActiveModName();
            draftDecision = operation => MessageBox.Show(this,
                "Save the editable draft AND its original AI packet before " + operation + "?\r\n" +
                "Yes: save a portable draft workspace. No: discard this window's unsaved work. Cancel: keep working.\r\n" +
                "Tracking journals do not back up draft text. A saved workspace never saves the mod.",
                "Unsaved draft workspace", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            notesDecision = message => MessageBox.Show(this, message, "Approve notes",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            resumeDecision = pending => MessageBox.Show(this, ResumeMessage(pending),
                "Confirm resumed state", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            settingsPath = settingsFile;
            AdoptSettings(SettingsStorage.Load(settingsPath));
            if (!editor.IsMdiContainer) throw new NotSupportedException("The FCS main window is not an MDI container.");
            MdiParent = editor;
            Text = "FCS-Scribe - experimental documentation";
            Width = 1080;
            Height = 780;
            var coreMenu = AddMenu("&Core", "Start or resume tracking, browse recording history, save Scribe changes, compile the journal or export AI/Markdown evidence.");
            AddAsyncAction(coreMenu.DropDownItems, "&Start tracking", "Create or resume a tracking project and begin a new timestamped session log. Replaces the editable draft; does not save or restore the mod.", Start);
            AddAsyncAction(coreMenu.DropDownItems, "&Save Changes", "Save enabled Scribe notes/TODOs and record captured changes to the tracking journal. Does NOT save the .mod, native FCS annotations or editable AI draft workspace.", Review);
            AddAsyncAction(coreMenu.DropDownItems, "&Compile journal", "Consolidate durable session entries without duplicates or deleting originals. Automatic mode compiles once the oldest pending recording is 30 minutes old, including time closed or manual.", Compile);
            AddAsyncAction(coreMenu.DropDownItems, "&Browse history", "Read verified session/recording evidence, filter by session or search and preview a complete range before AI/Markdown export. Opening, browsing and closing save nothing; no pruning, native restore or automatic draft replacement.", BrowseHistory, true, false);
            AddAsyncAction(coreMenu.DropDownItems, "&Export AI packet", "Choose and preview an inclusive range of recordings, including earlier sessions or unrecorded edits, then save an AI packet. Does not record, compile or send anything online. Replaces the editable draft.", Export);
            AddAsyncAction(coreMenu.DropDownItems, "Export &Markdown", "Choose and preview a recording range, then export readable captured facts with exact typed values, evidence IDs and warnings. No AI required; does not record, compile, apply notes or replace the editable draft.", ExportMarkdown);
            var workshopMenu = AddMenu("&Workshop", "Export local Workshop information or archive the original preview image.");
            AddAction(workshopMenu.DropDownItems, "&Export Workshop info", "Save current local Workshop information, availability warnings and preview-image details as JSON. No tracking baseline required; Workshop-only errors do not pause gameplay recording.", ExportWorkshop, false);
            AddAction(workshopMenu.DropDownItems, "&Archive preview image", "Copy the local Workshop preview, byte-for-byte, to a new image file outside Kenshi. Unavailable images fail explicitly without pausing gameplay recording. Does not download or change the original.", ArchivePreview, false);
            var draftMenu = AddMenu("&Draft", "Import or paste an AI response, or export reviewed draft prose and its original evidence as Markdown.");
            AddAsyncAction(draftMenu.DropDownItems, "&Import draft", "Load an AI response JSON into the editable draft. Requires an exported batch; save/discard protection keeps existing work safe.", Import);
            AddAsyncAction(draftMenu.DropDownItems, "&Paste draft", "Replace the editable draft with clipboard text after save/discard protection. Nothing is applied by pasting.", Paste);
            AddAsyncAction(draftMenu.DropDownItems, "Export reviewed &Markdown", "After reviewing the prose, export the complete current-state-valid draft and its original catalogue as Markdown. Facts and prose stay separate. No native notes lookup, approval, application or mod save; restored workspaces need no open tracking project.", ExportReviewedMarkdown);
            var workspaceMenu = AddMenu("&Workspace", "Save or restore a portable AI draft workspace. Scribe notes/TODO files are separate.");
            AddAsyncAction(workspaceMenu.DropDownItems, "&Save workspace", "Atomically save exact editable text, even unfinished JSON, together with its original AI packet. This is separate from tracking and does not save the mod.", SaveWorkspaceAction);
            AddAsyncAction(workspaceMenu.DropDownItems, "&Open workspace", "Restore draft text and its original packet after restart. Validate the bundle, report stale/invalid drafts, and require fresh validation and approval before applying.", OpenWorkspace);
            AddAction(actions.Items, "&Apply approved notes", "Validate the draft and ask for approval, then append record Notes/TODO entries and mark FCS dirty. Does not save or edit Workshop notes.", Apply);
            AddAsyncAction(actions.Items, "&Settings", "Choose mod notes/TODOs, project storage and recording/export defaults, reload changed settings or explicitly repair settings. Existing tracking files stay at their chosen location.", Settings, true, false);
            MainMenuStrip = actions;
            actions.OverflowButton.AccessibleDescription = "Show menu actions that do not fit in the window.";
            actions.OverflowButton.MouseEnter += delegate { hoveredAction = actions.OverflowButton; UpdateHelp(); };
            actions.OverflowButton.MouseLeave += delegate { hoveredAction = null; UpdateHelp(); };
            actions.OverflowButton.Paint += delegate { if (actions.OverflowButton.Selected) UpdateHelp(); };
            actions.OverflowButton.DropDown.Opened += delegate { menuHelpActive = true; UpdateHelp(); };
            actions.OverflowButton.DropDown.Closed += delegate { ResetHelp(); };
            actions.MenuActivate += delegate { menuHelpActive = true; UpdateHelp(); };
            actions.KeyDown += delegate { menuHelpActive = true; UpdateHelp(); };
            actions.MenuDeactivate += delegate { ResetHelp(); };
            actions.Leave += delegate { ResetHelp(); };
            tabs = new TabControl { Dock = DockStyle.Fill };
            modWork = new ModWorkView(LocalAction, ReloadModWork);
            var changesTab = new TabPage("Changes and warnings");
            changesTab.Controls.Add(review);
            var draftTab = new TabPage("Editable draft JSON");
            draftTab.Controls.Add(draft);
            tabs.TabPages.Add(changesTab);
            tabs.TabPages.Add(draftTab);
            var workshopTab = new TabPage("Workshop info");
            workshopTab.Controls.Add(workshop);
            tabs.TabPages.Add(workshopTab);
            tabs.TabPages.Add(modWork.NotesTab); tabs.TabPages.Add(modWork.TodoTab);
            modWork.Load(ProjectDirectory(), modName, settings);
            tabs.SelectedIndexChanged += delegate {
                ResetHelp();
                if (tabs.SelectedTab != workshopTab || busy) return;
                try
                {
                    SaveModWork();
                    WorkshopMetadata metadata = adapter.CaptureWorkshop();
                    workshop.Text = Json.Pretty(Json.Write(metadata));
                    status.Text = WorkshopNotice(metadata) + "Workshop information read from local/staged/cached data; no Steam query.";
                }
                catch (Exception error) { workshop.Clear(); Plugin.ShowError(error); }
            };
            Controls.Add(tabs);
            Controls.Add(actions);
            Controls.Add(help);
            Controls.Add(status);
            UpdateHelp();
            status.Text = "No tracking project open. Start/Resume manages recorded evidence, not saved mod/notes files.";
            timer.Tick += async delegate {
                if (busy || closing) return;
                RefreshCompilationNotice();
                if (automaticPaused || settingsError != null || !Visible || tracker == null ||
                    WindowState == FormWindowState.Minimized ||
                    (MdiParent != null && MdiParent.WindowState == FormWindowState.Minimized) ||
                    actions.OverflowButton.DropDown.Visible ||
                    actions.Items.OfType<ToolStripDropDownItem>().Any(x => x.DropDown.Visible) ||
                    Application.OpenForms.Cast<Form>().Any(x => x.Visible && x.Modal)) return;
                if (!schedule.ReviewDue(clock.ElapsedMilliseconds) && !schedule.CompilationDue(clock.ElapsedMilliseconds)) return;
                try { await RunBusy(AutomaticTick); }
                catch (Exception error) { if (!closing) PauseAfterError(error); }
            };
            draft.TextChanged += delegate { draftRevision++; UpdateDraftTitle(); };
            FormClosing += async delegate(object sender, FormClosingEventArgs args) {
                if (busy)
                {
                    args.Cancel = true;
                    status.Text = "An operation is still running. Draft work is retained; close again after it completes.";
                    return;
                }
                try { SaveModWork(); }
                catch (Exception error) { args.Cancel = true; PauseAfterError(error); return; }
                if (DraftNeedsSave && approvedCloseRevision != draftRevision)
                {
                    args.Cancel = true;
                    try
                    {
                        await RunBusy(async () => {
                            if (!await ConfirmDraftReplacement("closing Scribe")) return;
                            approvedCloseRevision = draftRevision;
                            BeginInvoke(new Action(Close));
                        });
                    }
                    catch (Exception error) { PauseAfterError(error); }
                    return;
                }
                closing = true; timer.Stop(); if (project != null) project.Dispose();
            };
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
                        status.Text = "Validating recorded tracking state in the background; no mod/notes files are restored or saved.";
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
            if (!await ConfirmDraftReplacement("starting tracking")) return;
            long revision = draftRevision;
            Snapshot current = adapter.Capture();
            status.Text = "Saving a tracking baseline of the current editor, including unsaved edits; no mod/notes save.";
            JournalProject candidate = await Task.Run(() => JournalProject.Create(path, current));
            await Begin(candidate, null, revision);
        }

        private async Task ResumePath(string path)
        {
            if (!await ConfirmDraftReplacement("resuming tracking")) return;
            long revision = draftRevision;
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
                        "its exact bytes in .legacy.bak and its evidence, then start from the CURRENT editor state?\r\n" +
                        TrackingChoiceDialog.BoundaryText,
                        "Convert legacy export", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
                    Snapshot current = adapter.Capture();
                    candidate = await Task.Run(() => JournalProject.ConvertLegacy(path, current, legacy));
                }
                if (interrupted != null)
                {
                    if (closing || MessageBox.Show(this, interrupted.Message + "\r\nPreserve the original in a recovery backup " +
                        "and discard only its incomplete final line? Complete but invalid entries are never skipped.\r\n" +
                        "This repairs tracking history only; it cannot recover unsaved mod/notes edits or save those files.",
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
                if (pending.SourceFingerprint != candidate.Fingerprint && resumeDecision(pending) != DialogResult.Yes) return;
                JournalProject adopted = candidate; candidate = null;
                await Begin(adopted, pending, revision);
            }
            finally { if (candidate != null) candidate.Dispose(); }
        }

        private static string ResumeMessage(Packet pending)
        {
            return "The editor differs from the last recorded state (" + pending.Changes.Count +
                " evidence entries; context-only differences can have zero). Last recorded UTC: " +
                pending.BaselineCapturedUtc + ".\r\n" +
                "The recording may include edits lost after a crash or discarded without saving.\r\n\r\n" +
                "Yes: continue with the CURRENT editor. The next manual or automatic review records the differences; " +
                "no edits are restored.\r\nNo: do not resume this project.\r\n\r\n" + TrackingChoiceDialog.BoundaryText;
        }

        private async Task Begin(JournalProject candidate, Packet pending, long revision)
        {
            bool adopted = false;
            try
            {
                if (closing || !DraftUnchanged(revision)) return;
                await Task.Run(() => candidate.BeginSession());
                if (closing || !DraftUnchanged(revision)) return;
                if (project != null) project.Dispose();
                project = candidate; tracker = candidate.ResumeTracker(); adopted = true;
                if (pending != null) ShowChanges(pending);
                else
                    review.Text = "PARTIAL EXTRACTION - baseline captured; review all warnings.\r\n" +
                        Tracker.SamplingNotice + "\r\n" +
                        String.Join("\r\n", candidate.State.Warnings.Select(x => "WARNING: " + x));
                workshop.Text = Json.Pretty(Json.Write(candidate.State.Workshop));
            }
            finally { if (!adopted) candidate.Dispose(); }
            automaticPaused = false;
            AdoptWorkspace(null, "", null, null);
            schedule.Start(clock.ElapsedMilliseconds, DateTime.UtcNow, settings.ReviewSeconds, project.OldestPendingUtc);
            status.Text = ProjectStatus() + " New session: " + project.SessionPath;
        }

        private async Task Review()
        {
            SaveModWork();
            if (tracker == null)
            {
                if (!settings.ModNotesEnabled && !settings.ModTodoEnabled)
                    throw new InvalidOperationException("Select Start tracking first, or enable Scribe notes/TODOs in Settings.");
                status.Text = "Enabled Scribe notes/TODOs saved. No tracking project open; .mod/native notes/draft workspace NOT saved.";
                return;
            }
            Snapshot current = adapter.Capture();
            status.Text = "Validating and flushing new entries to the session log.";
            Packet packet = await Task.Run(() => project.Record(current));
            if (closing) return;
            tracker = project.ResumeTracker();
            schedule.Reviewed(clock.ElapsedMilliseconds);
            schedule.UpdatePending(clock.ElapsedMilliseconds, DateTime.UtcNow, project.OldestPendingUtc);
            ShowChanges(packet);
            status.Text = packet.Changes.Count + " evidence entries saved/unchanged; enabled Scribe notes/TODOs saved. " + ProjectStatus();
        }

        private async Task Compile()
        {
            if (project == null) throw new InvalidOperationException("Select Start tracking first.");
            status.Text = "Compiling durable session entries into the main journal.";
            int count = await Task.Run(() => project.Compile());
            if (closing) return;
            schedule.UpdatePending(clock.ElapsedMilliseconds, DateTime.UtcNow, project.OldestPendingUtc);
            status.Text = count + " new entries compiled. Session files retained. " + ProjectStatus();
        }

        private async Task AutomaticTick()
        {
            if (settingsError != null || settings.ReviewSeconds == 0 || automaticPaused) return;
            if (schedule.ReviewDue(clock.ElapsedMilliseconds)) await Review();
            if (!closing && schedule.CompilationDue(clock.ElapsedMilliseconds)) await Compile();
        }

        private string ProjectStatus()
        {
            return WorkshopNotice(project.State.Workshop) + "Last recorded UTC: " + project.CapturedUtc +
                ". Mod/notes save state is NOT verified. " + project.PendingCount +
                " recordings awaiting compilation. " + ScheduleText();
        }

        private static string WorkshopNotice(WorkshopMetadata metadata)
        {
            return metadata != null && metadata.CaptureStatus == "partial" ?
                "PARTIAL Workshop capture; unavailable values are not validated. Review warnings. " : "";
        }

        private void ShowChanges(Packet packet)
        {
            var text = new StringBuilder("PARTIAL EXTRACTION - review all warnings.\r\n");
            text.AppendLine(Tracker.SamplingNotice);
            if (packet.Recordings != null)
            {
                text.AppendLine("RECORDING RANGE - chronological observations, not a net diff. Current-state validation is from export time.");
                foreach (RecordingEvidence recording in packet.Recordings)
                    text.AppendLine(recording.EntryId + " | " + recording.RecordedUtc + " | " + recording.Kind +
                        " | session " + (recording.SessionId ?? "(legacy)") + " | original batch " + recording.Packet.BatchId);
                foreach (string warning in packet.Recordings.SelectMany(x => x.Packet.Context.Warnings).Distinct(StringComparer.Ordinal))
                    text.AppendLine("RECORDING WARNING: " + warning);
            }
            foreach (string warning in packet.Context.Warnings) text.AppendLine("WARNING: " + warning);
            text.AppendLine();
            AppendChanges(text, packet.Changes);
            review.Text = text.ToString();
            workshop.Text = Json.Pretty(Json.Write(packet.Context.Workshop));
            status.Text = WorkshopNotice(packet.Context.Workshop) + packet.Changes.Count +
                " change entries (stored and effective entries may describe the same edit). " +
                "Baseline unchanged.";
        }

        internal static void AppendChanges(StringBuilder text, System.Collections.Generic.IEnumerable<Change> changes)
        {
            foreach (Change change in changes)
                text.AppendLine(change.Id + " | " + change.RecordType + " | " + change.RecordId +
                    " | " + change.DisplayName + "\r\n  " + change.Operation + " " + change.Layer +
                    " " + change.Path + "\r\n  " + Json.Write(change.Before) + " -> " + Json.Write(change.After) +
                    "\r\n  Source labels: " + (change.BeforeSource ?? "(none captured)") + " -> " +
                    (change.AfterSource ?? "(none captured)") +
                    (change.ReferencedId == null ? "" : "\r\n  Reference: " + change.ReferencedId +
                    " (" + (change.ReferencedName ?? "UNRESOLVED") + ")"));
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
                SaveWorkshopPath(dialog.FileName, export);
            }
            workshop.Text = Json.Pretty(Json.Write(export.Workshop));
            status.Text = WorkshopNotice(export.Workshop) +
                "Workshop JSON exported from local data only. Review its source and warnings; use Archive preview image to copy the image separately.";
        }

        private void SaveWorkshopPath(string path, WorkshopExport export)
        {
            ExportStorage.WriteJson(path, export, project == null ? null : project.PathName);
        }

        private void ArchivePreview()
        {
            WorkshopMetadata metadata = adapter.CaptureWorkshop();
            if (metadata.Preview.Status == "unavailable")
                throw new InvalidOperationException(metadata.Preview.UnavailableReason);
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

        private async Task<Packet> SelectRecordingRange()
        {
            if (tracker == null) throw new InvalidOperationException("Select Start tracking first.");
            Snapshot current = adapter.Capture();
            var recordings = await ReadRecordingHistory(current);
            if (closing) return null;
            if (recordings.Count == 0) throw new InvalidOperationException("No recorded or pending changes to document.");
            Packet packet;
            using (var selection = new RecordingRangeDialog(recordings))
            {
                if (selection.ShowDialog(this) != DialogResult.OK)
                {
                    status.Text = "Recording-range selection cancelled; no export or history advancement.";
                    return null;
                }
                int first = selection.First, last = selection.Last;
                packet = await Task.Run(() => RangeExport.Create(project.ProjectId, recordings, first, last, current));
            }
            return closing ? null : packet;
        }

        private async Task Export()
        {
            Packet packet = await SelectRecordingRange();
            if (packet == null) return;
            await ExportPacket(packet);
        }

        private Task<System.Collections.Generic.List<RecordingEvidence>> ReadRecordingHistory(Snapshot current)
        {
            if (project == null || tracker == null) throw new InvalidOperationException("Select Start tracking first.");
            status.Text = "Validating recording sources for history/range selection. No history is being changed.";
            return Task.Run(() => project.ExportRecordings(current));
        }

        private async Task BrowseHistory()
        {
            if (project == null || tracker == null) throw new InvalidOperationException("Select Start tracking first.");
            Snapshot current = adapter.Capture();
            var recordings = await ReadRecordingHistory(current);
            if (closing) return;
            using (var history = new RecordingHistoryDialog(recordings))
            {
                status.Text = recordings.Count + " history entries loaded. Browsing is read-only; close/reopen to refresh.";
                if (history.ShowDialog(this) != DialogResult.OK)
                {
                    status.Text = "History browser closed; draft/workspace, Scribe text and tracking/native files unchanged.";
                    return;
                }
                int first = history.First, last = history.Last;
                Packet packet = await Task.Run(() => RangeExport.Create(project.ProjectId, recordings, first, last, current));
                if (closing) return;
                SaveModWork();
                if (history.Markdown) await SaveMarkdownDialog(packet, null, draftRevision);
                else await ExportPacket(packet);
            }
        }

        private async Task ExportPacket(Packet packet)
        {
            if (!await ConfirmDraftReplacement("exporting a new AI packet"))
            {
                status.Text = "AI export cancelled; editable draft/workspace association and history retained.";
                return;
            }
            using (var dialog = new SaveFileDialog { Filter = "JSON packet (*.json)|*.json", FileName = "fcs-scribe-ai-packet.json",
                InitialDirectory = ExportDirectory(), RestoreDirectory = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    status.Text = "AI export cancelled; no packet written or new draft adopted. History retained.";
                    return;
                }
                if (!String.Equals(Path.GetExtension(dialog.FileName), ".json", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Export requires a .json filename; mod files are never export targets.");
                SavePacketPath(dialog.FileName, packet);
            }
            AdoptWorkspace(packet, Json.Pretty(Json.Write(packet.ResponseTemplate)), null, null);
            ShowChanges(packet);
            status.Text = WorkshopNotice(packet.Context.Workshop) + packet.Recordings.Count +
                " selected recordings exported; no history advanced or compiled. " +
                "Share manually with AI, then import/paste its JSON response. " +
                "Mod text may be private; inspect the packet before sharing.";
        }

        private void SavePacketPath(string path, Packet packet)
        {
            ProtectTrackingFile(path);
            if (adapter.Capture().Fingerprint() != packet.SourceFingerprint)
                throw new InvalidOperationException("The editor changed while selecting the range. Export again to review the current recordings and pending edits.");
            ExportStorage.WriteJson(path, packet, project == null ? null : project.PathName);
        }

        private async Task ExportMarkdown()
        {
            Packet packet = await SelectRecordingRange();
            if (packet != null) await SaveMarkdownDialog(packet, null, draftRevision);
        }

        private async Task ExportReviewedMarkdown()
        {
            if (exported == null)
                throw new InvalidOperationException("Export an AI packet or Open workspace with its original packet before exporting reviewed Markdown.");
            long revision = draftRevision;
            NoteDraft notes = Json.Read<NoteDraft>(draft.Text);
            TrackingDocument.ValidatePacket(exported);
            DraftValidation.Pending(exported, notes, adapter.Capture(), delegate { return ""; });
            await SaveMarkdownDialog(exported, notes, revision);
        }

        private async Task SaveMarkdownDialog(Packet packet, NoteDraft notes, long revision)
        {
            using (var dialog = new SaveFileDialog { Filter = "Markdown (*.md)|*.md",
                FileName = notes == null ? "fcs-scribe-changes.md" : "fcs-scribe-reviewed.md",
                InitialDirectory = ExportDirectory(), RestoreDirectory = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    status.Text = "Markdown export cancelled; no Markdown file written. Draft and history retained.";
                    return;
                }
                await SaveMarkdownPath(dialog.FileName, packet, notes, revision);
            }
            if (closing) return;
            status.Text = packet.Changes.Count + " evidence rows exported as Markdown" +
                (notes == null ? "." : " with reviewed draft prose, not native approval.") +
                " No recording/compilation, native notes, mod save or draft/workspace replacement. " +
                "Inspect private content before sharing.";
        }

        private async Task SaveMarkdownPath(string path, Packet packet, NoteDraft notes, long expectedRevision)
        {
            if (notes != null && expectedRevision != draftRevision)
                throw new InvalidOperationException("The editable draft changed while choosing the Markdown destination. Review its latest text and export again.");
            string anchor = project == null ? null : project.PathName;
            string hash = ExportStorage.ObserveMarkdownTarget(path, anchor);
            long revision = draftRevision;
            Snapshot current = adapter.Capture();
            if (current.Fingerprint() != packet.SourceFingerprint)
                throw new InvalidOperationException("The editor changed after the packet/range was captured. Review and export current evidence again.");
            status.Text = "Rendering Markdown from captured evidence; draft/workspace and native files are unchanged.";
            string text = await Task.Run(() => notes == null ? MarkdownExport.Render(packet) :
                MarkdownExport.RenderReviewed(packet, notes, current));
            if (closing) return;
            if (notes != null && draftRevision != revision)
                throw new InvalidOperationException("The editable draft changed during Markdown export. Review its latest text and export again.");
            if (adapter.Capture().Fingerprint() != packet.SourceFingerprint)
                throw new InvalidOperationException("The editor changed during Markdown export. Review and export current evidence again.");
            status.Text = "Writing the guarded Markdown export; no recording, compilation or native save.";
            await Task.Run(() => ExportStorage.WriteMarkdown(path, text, anchor, hash, null));
        }

        private async Task Import()
        {
            if (exported == null) throw new InvalidOperationException("Export an AI packet or Open workspace with its original packet first.");
            using (var dialog = new OpenFileDialog { Filter = "JSON draft (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = ExportDirectory(), RestoreDirectory = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string path = dialog.FileName;
                string text = await Task.Run(() => File.ReadAllText(path));
                if (Json.Read<NoteDraft>(text) == null) throw new InvalidDataException("The response draft is empty.");
                if (!await ConfirmDraftReplacement("importing another draft")) return;
                draft.Text = text;
            }
            status.Text = "Imported as an editable draft, not applied. Review TargetRecordId, Text and ChangeIds before approval.";
        }

        private async Task Paste()
        {
            string text = Clipboard.GetText();
            if (!await ConfirmDraftReplacement("pasting another draft")) return;
            draft.Text = text;
            status.Text = exported == null ?
                "Pasted as unassociated text; it cannot be applied without an AI packet. Save workspace can preserve this text-only work." :
                "Pasted as editable text, not applied. Save workspace preserves the original packet with this text.";
        }

        private bool DraftNeedsSave
        {
            get
            {
                return workspacePath == null ? exported != null || draft.Text.Length > 0 :
                    draft.Text != savedDraftText || !Object.ReferenceEquals(exported, savedPacket);
            }
        }

        private void UpdateDraftTitle()
        {
            Text = "FCS-Scribe - experimental documentation" + (DraftNeedsSave ? " * unsaved draft workspace" : "");
        }

        private bool DraftUnchanged(long revision)
        {
            if (revision == draftRevision) return true;
            status.Text = "Draft changed during the operation. Replacement cancelled; the latest text and original packet were retained.";
            return false;
        }

        private async Task<bool> ConfirmDraftReplacement(string operation)
        {
            long revision = draftRevision;
            if (!DraftNeedsSave) return true;
            DialogResult decision = draftDecision(operation);
            if (decision != DialogResult.Yes && decision != DialogResult.No) return false;
            if (decision == DialogResult.Yes && !await SaveWorkspace()) return false;
            return DraftUnchanged(revision);
        }

        private void AdoptWorkspace(Packet packet, string text, string path, string hash)
        {
            exported = packet; workspacePath = path; workspaceHash = hash;
            savedPacket = path == null ? null : packet;
            savedDraftText = path == null ? null : text;
            draftRevision++;
            draft.Text = text;
            UpdateDraftTitle();
        }

        private async Task SaveWorkspaceAction() { await SaveWorkspace(); }

        private async Task<bool> SaveWorkspace()
        {
            using (var dialog = new SaveFileDialog { Filter = "Draft workspace JSON (*.json)|*.json",
                FileName = workspacePath ?? "fcs-scribe-draft-workspace.json",
                InitialDirectory = ExportDirectory(), RestoreDirectory = true,
                Title = "Save draft workspace - original packet and exact editable text" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                return await SaveWorkspacePath(dialog.FileName);
            }
        }

        private async Task<bool> SaveWorkspacePath(string path)
        {
            path = Path.GetFullPath(path);
            ProtectTrackingFile(path);
            ProtectTrackingFile(path + ".bak");
            long revision = draftRevision;
            Packet packet = exported;
            DraftWorkspace snapshot = DraftWorkspace.Create(packet, draft.Text);
            bool same = String.Equals(path, workspacePath, StringComparison.OrdinalIgnoreCase);
            string expectedHash = same ? workspaceHash : null;
            string hash = await Task.Run(() => {
                // A workspace must never replace a tracking anchor, AI packet or unrelated JSON.
                if (!same && File.Exists(path)) expectedHash = DraftWorkspace.Load(path).FileHash;
                return TrackingStorage.WriteAtomic(path, snapshot, expectedHash, step => {
                    if (step == "commit")
                    {
                        ProtectTrackingFile(path);
                        ProtectTrackingFile(path + ".bak");
                    }
                });
            });
            if (Object.ReferenceEquals(exported, packet))
            {
                workspacePath = path; workspaceHash = hash;
                savedDraftText = snapshot.DraftText; savedPacket = packet;
                UpdateDraftTitle();
            }
            bool unchanged = revision == draftRevision;
            status.Text = "Draft workspace saved. " + (unchanged ? "Original evidence retained; nothing applied or saved in FCS." :
                "The saved snapshot does not include edits made during saving; the latest text remains unsaved.");
            return unchanged;
        }

        private async Task OpenWorkspace()
        {
            using (var dialog = new OpenFileDialog { Filter = "Draft workspace JSON (*.json)|*.json",
                InitialDirectory = ExportDirectory(), RestoreDirectory = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                await OpenWorkspacePath(dialog.FileName);
            }
        }

        private async Task OpenWorkspacePath(string path)
        {
            path = Path.GetFullPath(path);
            long revision = draftRevision;
            DraftWorkspace loaded = await Task.Run(() => DraftWorkspace.Load(path));
            if (closing || !DraftUnchanged(revision)) return;
            if (!await ConfirmDraftReplacement("opening another draft workspace")) return;
            await Task.Run(() => TrackingStorage.Verify(path, loaded.FileHash));
            if (closing || !DraftUnchanged(revision)) return;
            string validation = loaded.Review(adapter.Capture(), adapter.Notes);
            AdoptWorkspace(loaded.Packet, loaded.DraftText, path, loaded.FileHash);
            if (exported != null) ShowChanges(exported);
            else review.Clear();
            status.Text = "Workspace restored, not applied. " + validation;
        }

        private void Apply()
        {
            if (exported == null) throw new InvalidOperationException("Export an AI packet or Open workspace with its original packet before applying notes.");
            NoteDraft notes = Json.Read<NoteDraft>(draft.Text);
            Snapshot current = adapter.Capture();
            var pending = DraftValidation.Pending(exported, notes, current, adapter.Notes);
            string notesFile = adapter.NotesFile();
            string approval = WorkshopNotice(current.Workshop) + "The complete draft covers all " + exported.Changes.Count +
                " exported changes. Approve appending " + pending.Count + " pending note(s) to FCS? " +
                (notes.Entries.Count - pending.Count) + " matching completed note(s) will be skipped. " +
                "Existing notes are retained. The mod will be marked changed, but NOT saved. " +
                "Multi-record application is not transactional; inspect FCS Notes afterward.\r\n\r\n" +
                "Active notes file: " + notesFile + "\r\n" + NoteApplyReport.SidecarNotice;
            if (notesDecision(approval) != DialogResult.Yes) return;
            if (!String.Equals(adapter.NotesFile(), notesFile, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The active notes file changed during approval. Review the destination and approve again.");
            NoteApplyReport result = adapter.Apply(exported, notes, id => {
                status.Text = "Confirmed note append: " + id + ". Not saved; remaining targets are not yet confirmed.";
                status.Refresh();
            });
            review.Text = result.Details;
            status.Text = result.Summary + " Inspect FCS Notes and use normal Save when ready. Baseline unchanged; NOT saved.";
        }

        private void ShowNoteFailure(NoteApplyException error)
        {
            review.Text = error.Message + "\r\n\r\n" + error.Report.Details;
            status.Text = error.Report.Summary + " Notes may be partial; automatic work paused. Inspect the per-record report. NOT saved.";
        }

        private static TextBox TextArea(bool readOnly)
        {
            return new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both,
                WordWrap = false, ReadOnly = readOnly, MaxLength = Int32.MaxValue };
        }

        private string ExportDirectory() { return settings.DirectoryFor(location == null ? adapter.ActiveModDirectory() : ProjectDirectory()); }

        private void ProtectTrackingFile(string path)
        {
            ExportStorage.ObserveTarget(path, project == null ? null : project.PathName);
        }

        private string ProjectDirectory() { return location == null ? Path.GetDirectoryName(Path.GetFullPath(settingsPath)) : location.Directory; }

        private void SaveModWork()
        {
            if (modWork == null) return;
            if (location != null) ModWork.Verify(location.LocatorPath, location.LocatorHash);
            if (settingsError == null && location != null && settingsHash == null)
            {
                settings.ProjectModId = ModId;
                settingsHash = SettingsStorage.Save(settingsPath, settings, null, false).FileHash;
            }
            modWork.Save();
        }

        private void LocalAction(Action action)
        {
            if (busy || closing) return;
            try { action(); SaveModWork(); }
            catch (Exception error) { PauseAfterError(error); }
        }

        private void ReloadModWork()
        {
            if (busy || closing) return;
            if (MessageBox.Show(this, "Discard unsaved Scribe notes/TODO edits and reload both files? " +
                "Copy any text you need first. Native FCS notes are unchanged.", "Reload Scribe work",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            modWork.Load(ProjectDirectory(), modName, settings);
            status.Text = modWork.LoadError == null ? "Scribe notes/TODO files reloaded; nothing written." :
                "Scribe work still needs recovery. Files and unsaved editor text were not overwritten.";
        }

        internal void AddModTodos(System.Collections.Generic.List<ModTodo> targets)
        {
            if (busy || closing) throw new InvalidOperationException("Scribe is busy; add the task after the operation finishes.");
            if (!settings.ModTodoEnabled) throw new InvalidOperationException("Enable Mod To Do in Settings first.");
            if (targets.Count == 0) throw new InvalidOperationException("Select an FCS record first.");
            using (var dialog = new ModTodoDialog("Review " + String.Join(", ", targets.Select(x => x.RecordName))))
                if (dialog.ShowDialog(this) == DialogResult.OK) AddModTodos(targets, dialog.Value);
        }

        internal void AddModTodos(System.Collections.Generic.List<ModTodo> targets, string text)
        {
            if (busy || closing || !settings.ModTodoEnabled || modWork.Work == null)
                throw new InvalidOperationException("Mod To Do is unavailable; enable it and resolve any file recovery first.");
            if (adapter.ActiveModId() != ModId) throw new InvalidOperationException("The active mod changed. Open Scribe for that mod before adding tasks.");
            if (targets.Count == 0 || String.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Select records and enter task text.");
            foreach (ModTodo target in targets)
                modWork.Work.Todos.Add(new ModTodo { RecordId = target.RecordId, RecordName = target.RecordName, Text = text });
            modWork.RefreshTasks(); tabs.SelectedTab = modWork.TodoTab;
            SaveModWork();
            status.Text = targets.Count + " Mod To Do task(s) saved in " + modWork.Work.TodoPath + ". Native annotations unchanged.";
        }

        private async Task Settings()
        {
            using (var dialog = new SettingsDialog(settings, settingsPath, settingsError))
            {
                DialogResult result = dialog.ShowDialog(this);
                if (result == DialogResult.Retry) await ReloadSettings();
                else if (result == DialogResult.OK) await SaveSettings(dialog.Value);
            }
        }

        private void AdoptSettings(LoadedSettings loaded)
        {
            if (loaded.Error == null && loaded.Value == null)
                throw new InvalidOperationException("The settings loader returned neither settings nor a recovery error.");
            if (loaded.Error == null && loaded.Value.ProjectModId != null && loaded.Value.ProjectModId != ModId)
                loaded.Error = new InvalidDataException("These Scribe settings belong to another active mod path. Choose a separate project folder or explicitly repair settings.");
            settingsError = loaded.Error; settingsHash = loaded.FileHash;
            settings = settingsError == null ? loaded.Value : new PluginSettings();
            schedule.Configure(clock.ElapsedMilliseconds, settings.ReviewSeconds);
            settingsNotice = settingsError == null ? "" :
                "SETTINGS RECOVERY - automatic recording disabled. Safe manual mode uses the Scribe project export folder. " +
                "Open Settings to reload or explicitly save a repair. No settings file was changed.\r\n" +
                settingsPath + "\r\n" + settingsError.Message;
            if (settingsError != null) Console.Error.WriteLine("FCS-Scribe settings recovery: " + settingsError);
            RefreshCompilationNotice();
        }

        private async Task ReloadSettings()
        {
            if (modWork.LoadError == null) SaveModWork();
            LoadedSettings loaded = await Task.Run(() => SettingsStorage.Load(settingsPath));
            if (closing) return;
            AdoptSettings(loaded);
            modWork.Load(ProjectDirectory(), modName, settings);
            if (settingsError == null)
            {
                automaticPaused = false;
                status.Text = "Settings reloaded and validated. Existing tracking files were not moved. " + ScheduleText();
            }
            else status.Text = "Settings could not be loaded. Original file retained; safe manual recovery mode remains active.";
        }

        private async Task SaveSettings(PluginSettings next)
        {
            if (adapter.ActiveModId() != ModId) throw new InvalidOperationException("The active mod changed. Open its own Scribe window before changing settings.");
            if (modWork.LoadError == null) SaveModWork();
            next.ProjectModId = ModId;
            string root = String.IsNullOrEmpty(next.ProjectDirectory) ? adapter.ActiveModDirectory() : next.ProjectDirectory;
            if (location == null) root = ProjectDirectory();
            ProjectLocation.RequireDirectory(root);
            var candidate = new ModWork(root, modName, next.ModNotesEnabled, next.ModTodoEnabled);
            bool switching = !String.Equals(Path.GetFullPath(root), Path.GetFullPath(ProjectDirectory()), StringComparison.OrdinalIgnoreCase);
            string destination = switching ? Path.Combine(root, "FCS-Scribe.settings.json") : settingsPath;
            string expectedHash = settingsHash;
            bool recovering = settingsError != null;
            if (switching)
            {
                LoadedSettings existing = SettingsStorage.Load(destination);
                if (existing.Error != null) throw existing.Error;
                if (existing.FileHash != null && existing.Value.ProjectModId != ModId)
                    throw new InvalidDataException("The destination already contains settings not owned by this mod. Choose a separate project folder.");
                expectedHash = existing.FileHash; recovering = false;
            }
            SavedSettings saved = await Task.Run(() => SettingsStorage.Save(destination, next, expectedHash, recovering));
            if (closing) return;
            if (switching)
            {
                location.Switch(root);
                settingsPath = destination;
            }
            AdoptSettings(new LoadedSettings { Value = next, FileHash = saved.FileHash });
            modWork.Adopt(candidate, settings);
            SaveModWork();
            automaticPaused = false;
            status.Text = "Settings saved. " + (saved.RecoveryBackup == null ? "" :
                "Original bytes preserved at " + saved.RecoveryBackup + ". ") +
                "Existing tracking files were not moved. " + ScheduleText();
        }

        private string ScheduleText()
        {
            if (settingsError != null) return "Settings recovery: safe manual mode; use Settings to reload or save a repair.";
            return automaticPaused ? "Automatic review paused after an error; resume or save settings to re-enable." :
                settings.ReviewSeconds == 0 ? "Manual recording/compilation." : "Automatic recording: " +
                    settings.ReviewSeconds + " seconds; compilation: oldest pending age 30 minutes, on the next eligible tick.";
        }

        private void RefreshCompilationNotice()
        {
            DateTime? oldest = schedule.OldestPendingUtc;
            if (project == null || !oldest.HasValue) { compilationNotice = ""; UpdateHelp(); return; }
            long remaining = Math.Max(0, schedule.NextCompilation - clock.ElapsedMilliseconds);
            string due = remaining == 0 ? "due now/overdue" : "due " + DateTime.UtcNow.AddMilliseconds(remaining).ToString("u");
            string hold = settingsError != null ? "Settings recovery: automatic compilation held; use Compile journal." :
                settings.ReviewSeconds == 0 ? "Manual mode: use Compile journal; debt is retained." :
                automaticPaused ? "Automatic work paused; debt is retained. Compile manually or resolve the error." :
                "Automatic compilation waits for the next eligible visible, unblocked tick.";
            compilationNotice = "Pending compilation: " + project.PendingCount + " recordings; oldest " +
                oldest.Value.ToString("u") + "; " + due + ". " +
                (oldest.Value > DateTime.UtcNow ? (remaining == 0 ? "Timestamp ahead of local UTC: treated as due now. " :
                "Local UTC moved behind the recording; the existing countdown is retained. ") : "") + hold;
            UpdateHelp();
        }

        private void ConfigureTimer()
        {
            timer.Stop();
            RefreshCompilationNotice();
            if (closing || automaticPaused || settingsError != null || tracker == null || settings.ReviewSeconds == 0) return;
            timer.Interval = 1000;
            timer.Start();
        }

        private Task RunBusy(Func<Task> action) { return RunBusy(action, true); }

        private async Task RunBusy(Func<Task> action, bool persistWork)
        {
            if (busy || closing) return;
            busy = true; actions.Enabled = false; timer.Stop();
            ResetHelp();
            modWork.NotesTab.Enabled = modWork.TodoTab.Enabled = false;
            try
            {
                if (persistWork) SaveModWork();
                await action();
                if (persistWork) SaveModWork();
            }
            finally
            {
                busy = false;
                if (!closing) { actions.Enabled = true; modWork.NotesTab.Enabled = modWork.TodoTab.Enabled = true; ConfigureTimer(); }
                else if (project != null) project.Dispose();
            }
        }

        private void PauseAfterError(Exception error)
        {
            SetActionFailure(true);
            Plugin.ShowError(error);
        }

        private void SetActionFailure(bool pauseAutomation)
        {
            if (pauseAutomation)
            {
                automaticPaused = true; timer.Stop();
                status.Text = "Action failed; automatic review paused. The baseline advances only after a successful checkpoint save.";
            }
            else status.Text = "Workshop action failed; gameplay tracking was not changed. " + ScheduleText();
            RefreshCompilationNotice();
        }

        private ToolStripMenuItem AddMenu(string label, string description)
        {
            ToolStripMenuItem item = CreateMenuItem(label, description);
            item.DropDownOpened += delegate { menuHelpActive = true; UpdateHelp(); };
            item.DropDownClosed += delegate { ResetHelp(); };
            actions.Items.Add(item);
            return item;
        }

        private ToolStripMenuItem CreateMenuItem(string label, string description)
        {
            var item = new ToolStripMenuItem(label) {
                AccessibleName = label.Replace("&", ""), AccessibleDescription = description,
                Overflow = ToolStripItemOverflow.AsNeeded
            };
            item.MouseEnter += delegate { hoveredAction = item; UpdateHelp(); };
            item.MouseLeave += delegate { hoveredAction = null; UpdateHelp(); };
            // ToolStrip items have no focus event; keyboard selection repaints the selected item.
            item.Paint += delegate { if (item.Selected) UpdateHelp(); };
            return item;
        }

        private void UpdateHelp()
        {
            var items = actions.Items.Cast<ToolStripItem>();
            ToolStripItem selected = !menuHelpActive ? null : items.FirstOrDefault(x => x is ToolStripDropDownItem &&
                ((ToolStripDropDownItem)x).DropDown.Visible) ?? items.FirstOrDefault(x => x.Selected) ??
                (actions.OverflowButton.Selected || actions.OverflowButton.DropDown.Visible ? actions.OverflowButton : null);
            if (selected == null) selected = hoveredAction;
            var menu = selected as ToolStripDropDownItem;
            while (menu != null && menu.DropDown.Visible)
            {
                ToolStripItem child = menu.DropDownItems.Cast<ToolStripItem>().FirstOrDefault(x => x.Selected);
                if (child == null) break;
                selected = child;
                menu = child as ToolStripDropDownItem;
            }
            string text = selected == null ? String.Join("\r\n", new[] { settingsNotice, compilationNotice,
                TrackingChoiceDialog.BoundaryText, TrackingChoiceDialog.StorageText }.Where(x => !String.IsNullOrEmpty(x))) :
                selected.AccessibleDescription;
            if (help.Text != text) help.Text = text;
        }

        private void ResetHelp()
        {
            hoveredAction = null; menuHelpActive = false;
            UpdateHelp();
        }

        private void AddAction(ToolStripItemCollection parent, string label, string description, Action action, bool pauseAutomation = true)
        {
            AddAsyncAction(parent, label, description, () => { action(); return Task.FromResult(0); }, pauseAutomation);
        }

        private void AddAsyncAction(ToolStripItemCollection parent, string label, string description, Func<Task> action, bool pauseAutomation = true, bool persistWork = true)
        {
            ToolStripMenuItem item = CreateMenuItem(label, description);
            item.Click += async delegate {
                try { await RunBusy(action, persistWork); }
                catch (Exception error)
                {
                    if (closing) return;
                    SetActionFailure(pauseAutomation);
                    var notesError = error as NoteApplyException;
                    if (notesError != null) ShowNoteFailure(notesError);
                    Plugin.ShowError(error);
                }
            };
            parent.Add(item);
        }
    }
}
