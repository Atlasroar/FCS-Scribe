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
        private readonly TextBox review = TextArea(true);
        private readonly TextBox draft = TextArea(false);
        private readonly TextBox workshop = TextArea(true);
        private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 48 };
        private readonly Label settingsNotice = new Label { Dock = DockStyle.Top, AutoSize = true,
            BorderStyle = BorderStyle.FixedSingle, MaximumSize = new System.Drawing.Size(1040, 0) };
        private readonly Label compilationNotice = new Label { Dock = DockStyle.Bottom, AutoSize = true,
            Visible = false, BorderStyle = BorderStyle.FixedSingle };
        private const string HelpHint = "Hover over a menu action, or select it with the keyboard, to see what it does.";
        private readonly Label help = new Label { Dock = DockStyle.Bottom, Height = 48, Text = HelpHint,
            BorderStyle = BorderStyle.FixedSingle };

        public ScribeWindow(FcsAdapter adapter, Form editor)
            : this(adapter, editor, PluginSettings.DefaultPath) { }

        internal ScribeWindow(FcsAdapter adapter, Form editor, string settingsFile)
        {
            this.adapter = adapter;
            draftDecision = operation => MessageBox.Show(this,
                "Save the editable draft AND its original AI packet before " + operation + "?\r\n" +
                "Yes: save a portable draft workspace. No: discard this window's unsaved work. Cancel: keep working.\r\n" +
                "Tracking journals do not back up draft text. A saved workspace never saves the mod.",
                "Unsaved draft workspace", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            settingsPath = settingsFile;
            AdoptSettings(SettingsStorage.Load(settingsPath));
            if (!editor.IsMdiContainer) throw new NotSupportedException("The FCS main window is not an MDI container.");
            MdiParent = editor;
            Text = "FCS-Scribe - experimental documentation";
            Width = 1080;
            Height = 780;
            var coreMenu = AddMenu("&Core", "Start or resume tracking, review changes, compile the journal or export an AI packet.");
            AddAsyncAction(coreMenu.DropDownItems, "&Start tracking", "Create or resume a tracking project and begin a new timestamped session log. Replaces the editable draft; does not save or restore the mod.", Start);
            AddAsyncAction(coreMenu.DropDownItems, "&Review changes", "Record new changes since the last durable entry into this session log. Automatic intervals are available in Settings.", Review);
            AddAsyncAction(coreMenu.DropDownItems, "&Compile journal", "Consolidate durable session entries without duplicates or deleting originals. Automatic mode compiles once the oldest pending recording is 30 minutes old, including time closed or manual.", Compile);
            AddAsyncAction(coreMenu.DropDownItems, "&Export AI packet", "Choose and preview an inclusive range of recordings, including earlier sessions or unrecorded edits, then save an AI packet. Does not record, compile or send anything online. Replaces the editable draft.", Export);
            var workshopMenu = AddMenu("&Workshop", "Export local Workshop information or archive the original preview image.");
            AddAction(workshopMenu.DropDownItems, "&Export Workshop info", "Save current local Workshop information, availability warnings and preview-image details as JSON. No tracking baseline required; Workshop-only errors do not pause gameplay recording.", ExportWorkshop, false);
            AddAction(workshopMenu.DropDownItems, "&Archive preview image", "Copy the local Workshop preview, byte-for-byte, to a new image file outside Kenshi. Unavailable images fail explicitly without pausing gameplay recording. Does not download or change the original.", ArchivePreview, false);
            var draftMenu = AddMenu("&Draft", "Import/paste a response or save/open its portable workspace with the original AI packet.");
            AddAsyncAction(draftMenu.DropDownItems, "&Import draft", "Load an AI response JSON into the editable draft. Requires an exported batch; save/discard protection keeps existing work safe.", Import);
            AddAsyncAction(draftMenu.DropDownItems, "&Paste draft", "Replace the editable draft with clipboard text after save/discard protection. Nothing is applied by pasting.", Paste);
            AddAsyncAction(draftMenu.DropDownItems, "&Save workspace", "Atomically save exact editable text, even unfinished JSON, together with its original AI packet. This is separate from tracking and does not save the mod.", SaveWorkspaceAction);
            AddAsyncAction(draftMenu.DropDownItems, "&Open workspace", "Restore draft text and its original packet after restart. Validate the bundle, report stale/invalid drafts, and require fresh validation and approval before applying.", OpenWorkspace);
            AddAction(actions.Items, "&Apply approved notes", "Validate the draft and ask for approval, then append record Notes/TODO entries and mark FCS dirty. Does not save or edit Workshop notes.", Apply);
            AddAsyncAction(actions.Items, "&Settings", "Choose recording/export defaults, reload changed settings or explicitly repair unreadable/invalid settings with an original-byte backup. Existing tracking files stay at their chosen location.", Settings);
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
                try
                {
                    WorkshopMetadata metadata = adapter.CaptureWorkshop();
                    workshop.Text = Json.Pretty(Json.Write(metadata));
                    status.Text = WorkshopNotice(metadata) + "Workshop information read from local/staged/cached data; no Steam query.";
                }
                catch (Exception error) { workshop.Clear(); Plugin.ShowError(error); }
            };
            Controls.Add(tabs);
            Controls.Add(settingsNotice);
            Controls.Add(actions);
            Controls.Add(compilationNotice);
            Controls.Add(help);
            Controls.Add(status);
            ClientSizeChanged += delegate {
                settingsNotice.MaximumSize = new System.Drawing.Size(Math.Max(1, ClientSize.Width), 0);
                compilationNotice.MaximumSize = new System.Drawing.Size(Math.Max(1, ClientSize.Width), 0);
            };
            settingsNotice.MaximumSize = new System.Drawing.Size(Math.Max(1, ClientSize.Width), 0);
            compilationNotice.MaximumSize = new System.Drawing.Size(Math.Max(1, ClientSize.Width), 0);
            status.Text = "Start or resume a project. Recordings go to timestamped session logs; compiling never saves the mod.";
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
            if (!await ConfirmDraftReplacement("starting tracking")) return;
            long revision = draftRevision;
            Snapshot current = adapter.Capture();
            status.Text = "Saving the project baseline and opening a timestamped session.";
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
                await Begin(adopted, pending, revision);
            }
            finally { if (candidate != null) candidate.Dispose(); }
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
                else if (candidate.State.Workshop != null && candidate.State.Workshop.CaptureStatus == "partial")
                    review.Text = "PARTIAL EXTRACTION - baseline captured with Workshop availability warnings.\r\n" +
                        String.Join("\r\n", candidate.State.Warnings.Select(x => "WARNING: " + x));
                else review.Clear();
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
            if (tracker == null) throw new InvalidOperationException("Select Start tracking first.");
            Snapshot current = adapter.Capture();
            status.Text = "Validating and flushing new entries to the session log.";
            Packet packet = await Task.Run(() => project.Record(current));
            if (closing) return;
            tracker = project.ResumeTracker();
            schedule.Reviewed(clock.ElapsedMilliseconds);
            schedule.UpdatePending(clock.ElapsedMilliseconds, DateTime.UtcNow, project.OldestPendingUtc);
            ShowChanges(packet);
            status.Text = packet.Changes.Count + " evidence entries reviewed; session recording saved/unchanged. " + ProjectStatus();
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
            return WorkshopNotice(project.State.Workshop) + project.PendingCount +
                " recordings awaiting compilation. Tracking is NOT a saved-mod backup. " + ScheduleText();
        }

        private static string WorkshopNotice(WorkshopMetadata metadata)
        {
            return metadata != null && metadata.CaptureStatus == "partial" ?
                "PARTIAL Workshop capture; unavailable values are not validated. Review warnings. " : "";
        }

        private void ShowChanges(Packet packet)
        {
            var text = new StringBuilder("PARTIAL EXTRACTION - review all warnings.\r\n");
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
                ProtectTrackingFile(dialog.FileName);
                File.WriteAllText(dialog.FileName, Json.Pretty(Json.Write(export)), new UTF8Encoding(false));
            }
            workshop.Text = Json.Pretty(Json.Write(export.Workshop));
            status.Text = WorkshopNotice(export.Workshop) +
                "Workshop JSON exported from local data only. Review its source and warnings; use Archive preview image to copy the image separately.";
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

        private async Task Export()
        {
            if (tracker == null) throw new InvalidOperationException("Select Start tracking first.");
            Snapshot current = adapter.Capture();
            status.Text = "Validating recording sources for range selection. No history is being changed.";
            var recordings = await Task.Run(() => project.ExportRecordings(current));
            if (closing) return;
            if (recordings.Count == 0) throw new InvalidOperationException("No recorded or pending changes to document.");
            Packet packet;
            using (var selection = new RecordingRangeDialog(recordings))
            {
                if (selection.ShowDialog(this) != DialogResult.OK) return;
                int first = selection.First, last = selection.Last;
                packet = await Task.Run(() => RangeExport.Create(project.ProjectId, recordings, first, last, current));
            }
            if (closing) return;
            if (!await ConfirmDraftReplacement("exporting a new AI packet")) return;
            using (var dialog = new SaveFileDialog { Filter = "JSON packet (*.json)|*.json", FileName = "fcs-scribe-ai-packet.json",
                InitialDirectory = ExportDirectory(), RestoreDirectory = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!String.Equals(Path.GetExtension(dialog.FileName), ".json", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Export requires a .json filename; mod files are never export targets.");
                ProtectTrackingFile(dialog.FileName);
                if (adapter.Capture().Fingerprint() != packet.SourceFingerprint)
                    throw new InvalidOperationException("The editor changed while selecting the range. Export again to review the current recordings and pending edits.");
                File.WriteAllText(dialog.FileName, Json.Pretty(Json.Write(packet)), new UTF8Encoding(false));
            }
            AdoptWorkspace(packet, Json.Pretty(Json.Write(packet.ResponseTemplate)), null, null);
            ShowChanges(packet);
            status.Text = WorkshopNotice(packet.Context.Workshop) + packet.Recordings.Count +
                " selected recordings exported; no history advanced or compiled. " +
                "Share manually with AI, then import/paste its JSON response. " +
                "Mod text may be private; inspect the packet before sharing.";
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
            long revision = draftRevision;
            Packet packet = exported;
            DraftWorkspace snapshot = DraftWorkspace.Create(packet, draft.Text);
            bool same = String.Equals(path, workspacePath, StringComparison.OrdinalIgnoreCase);
            string expectedHash = same ? workspaceHash : null;
            string hash = await Task.Run(() => {
                // A workspace must never replace a tracking anchor, AI packet or unrelated JSON.
                if (!same && File.Exists(path)) expectedHash = DraftWorkspace.Load(path).FileHash;
                return TrackingStorage.WriteAtomic(path, snapshot, expectedHash);
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
            if (MessageBox.Show(this, WorkshopNotice(current.Workshop) + "The complete draft covers all " + exported.Changes.Count +
                " exported changes. Approve appending " + pending.Count + " pending note(s) to FCS? " +
                (notes.Entries.Count - pending.Count) + " matching completed note(s) will be skipped. " +
                "Existing notes are retained. The mod will be marked changed, but NOT saved. " +
                "Multi-record application is not transactional; inspect FCS Notes afterward.",
                "Approve notes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            adapter.Apply(exported, notes);
            status.Text = WorkshopNotice(current.Workshop) +
                "Approved pending notes appended in FCS; matching completed notes were not duplicated. " +
                "Inspect FCS Notes and use FCS's normal Save when ready. Baseline unchanged.";
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
            settingsError = loaded.Error; settingsHash = loaded.FileHash;
            settings = settingsError == null ? loaded.Value : new PluginSettings();
            schedule.Configure(clock.ElapsedMilliseconds, settings.ReviewSeconds);
            settingsNotice.Visible = settingsError != null;
            settingsNotice.Text = settingsError == null ? "" :
                "SETTINGS RECOVERY - automatic recording disabled. Safe manual mode uses the active mod's export folder. " +
                "Open Settings to reload or explicitly save a repair. No settings file was changed.\r\n" +
                settingsPath + "\r\n" + settingsError.Message;
            if (settingsError != null) Console.Error.WriteLine("FCS-Scribe settings recovery: " + settingsError);
        }

        private async Task ReloadSettings()
        {
            LoadedSettings loaded = await Task.Run(() => SettingsStorage.Load(settingsPath));
            if (closing) return;
            AdoptSettings(loaded);
            if (settingsError == null)
            {
                automaticPaused = false;
                status.Text = "Settings reloaded and validated. Existing tracking files were not moved. " + ScheduleText();
            }
            else status.Text = "Settings could not be loaded. Original file retained; safe manual recovery mode remains active.";
        }

        private async Task SaveSettings(PluginSettings next)
        {
            string expectedHash = settingsHash;
            bool recovering = settingsError != null;
            SavedSettings saved = await Task.Run(() => SettingsStorage.Save(settingsPath, next, expectedHash, recovering));
            if (closing) return;
            AdoptSettings(new LoadedSettings { Value = next, FileHash = saved.FileHash });
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
            compilationNotice.Visible = project != null && oldest.HasValue;
            if (project == null || !oldest.HasValue) { compilationNotice.Text = ""; return; }
            long remaining = Math.Max(0, schedule.NextCompilation - clock.ElapsedMilliseconds);
            string due = remaining == 0 ? "due now/overdue" : "due " + DateTime.UtcNow.AddMilliseconds(remaining).ToString("u");
            string hold = settingsError != null ? "Settings recovery: automatic compilation held; use Compile journal." :
                settings.ReviewSeconds == 0 ? "Manual mode: use Compile journal; debt is retained." :
                automaticPaused ? "Automatic work paused; debt is retained. Compile manually or resolve the error." :
                "Automatic compilation waits for the next eligible visible, unblocked tick.";
            compilationNotice.Text = "Pending compilation: " + project.PendingCount + " recordings; oldest " +
                oldest.Value.ToString("u") + "; " + due + ". " +
                (oldest.Value > DateTime.UtcNow ? (remaining == 0 ? "Timestamp ahead of local UTC: treated as due now. " :
                "Local UTC moved behind the recording; the existing countdown is retained. ") : "") + hold;
        }

        private void ConfigureTimer()
        {
            timer.Stop();
            RefreshCompilationNotice();
            if (closing || automaticPaused || settingsError != null || tracker == null || settings.ReviewSeconds == 0) return;
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

        private void AddAction(ToolStripItemCollection parent, string label, string description, Action action, bool pauseAutomation = true)
        {
            AddAsyncAction(parent, label, description, () => { action(); return Task.FromResult(0); }, pauseAutomation);
        }

        private void AddAsyncAction(ToolStripItemCollection parent, string label, string description, Func<Task> action, bool pauseAutomation = true)
        {
            ToolStripMenuItem item = CreateMenuItem(label, description);
            item.Click += async delegate {
                try { await RunBusy(action); }
                catch (Exception error)
                {
                    if (closing) return;
                    SetActionFailure(pauseAutomation);
                    Plugin.ShowError(error);
                }
            };
            parent.Add(item);
        }
    }
}
