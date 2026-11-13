using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FcsScribe
{
    internal sealed class HistoryManagementDialog : Form
    {
        private readonly JournalProject project;
        private readonly string installationDirectory;
        private readonly Func<string, bool> confirm;
        private readonly TableLayoutPanel content;
        private readonly Label usage;
        private readonly TextBox destination, preview, outcome;
        private readonly Button archive, restore, cancel, close;
        private HistoryStorageInventory inventory;
        private CancellationTokenSource operation;
        private bool valid = true;
        internal readonly DataGridView Sessions = new DataGridView();
        internal readonly CheckBox AgeEnabled = new CheckBox { Text = "Last activity older than (days)", AutoSize = true };
        internal readonly NumericUpDown AgeDays = new NumericUpDown { Minimum = 1, Maximum = 36500, Value = 30, Width = 72 };
        internal readonly CheckBox KeepEnabled = new CheckBox { Text = "Keep newest eligible sessions", AutoSize = true };
        internal readonly NumericUpDown KeepCount = new NumericUpDown { Minimum = 0, Maximum = 1000000, Value = 10, Width = 72 };
        internal bool HadError { get; private set; }
        internal string Outcome { get { return outcome.Text; } }
        internal string PreviewText { get { return preview.Text; } }

        internal HistoryManagementDialog(JournalProject project, HistoryStorageInventory inventory,
            string installationDirectory, Func<string, bool> confirm)
        {
            if (project == null || inventory == null || confirm == null) throw new ArgumentNullException("project/inventory/confirm");
            this.project = project; this.installationDirectory = installationDirectory; this.confirm = confirm;
            Text = "Manage History - retained recording evidence";
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
            Size = new Size(1000, 760); MinimumSize = new Size(800, 700);
            content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Padding = new Padding(12) };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 62, 65, 72, 0, 30, 46, 82, 74 })
                content.RowStyles.Add(height == 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, height));
            content.Controls.Add(new Label { Dock = DockStyle.Fill, Text =
                "Archive only redundant, closed, fully compiled session-log originals. Every recording stays in the main journal.\r\n" +
                "Baseline, active/uncompiled sessions, recovery files, notes, tasks and draft workspaces stay untouched. Archives are private, not complete backups." }, 0, 0);
            usage = new Label { Dock = DockStyle.Fill }; content.Controls.Add(usage, 0, 1);
            var guidance = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
            var select = new Button { Text = "Select matching sessions", AutoSize = true };
            var clear = new Button { Text = "Clear selection", AutoSize = true };
            guidance.Controls.AddRange(new Control[] { AgeEnabled, AgeDays, KeepEnabled, KeepCount, select, clear });
            AgeEnabled.CheckedChanged += delegate { AgeDays.Enabled = AgeEnabled.Checked; };
            KeepEnabled.CheckedChanged += delegate { KeepCount.Enabled = KeepEnabled.Checked; };
            AgeDays.Enabled = KeepCount.Enabled = false;
            select.Click += delegate { ApplyGuidance(DateTime.UtcNow); };
            clear.Click += delegate { SetSelected(new HashSet<string>(StringComparer.Ordinal)); };
            content.Controls.Add(guidance, 0, 2);
            Sessions.Dock = DockStyle.Fill; Sessions.AllowUserToAddRows = Sessions.AllowUserToDeleteRows = false;
            Sessions.AllowUserToResizeRows = false; Sessions.RowHeadersVisible = false;
            Sessions.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            Sessions.SelectionMode = DataGridViewSelectionMode.FullRowSelect; Sessions.MultiSelect = false;
            Sessions.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Selected", HeaderText = "Select", Width = 55,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
            foreach (string name in new[] { "Last activity", "Session ID", "Recordings", "Bytes", "Status" })
                Sessions.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, ReadOnly = true });
            Sessions.Columns[1].FillWeight = 140; Sessions.Columns[2].FillWeight = 230;
            Sessions.Columns[3].FillWeight = 75; Sessions.Columns[4].FillWeight = 90; Sessions.Columns[5].FillWeight = 160;
            Sessions.CurrentCellDirtyStateChanged += delegate {
                if (Sessions.IsCurrentCellDirty) Sessions.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            Sessions.CellValueChanged += delegate { UpdateSelection(); };
            Sessions.CellBeginEdit += delegate(object sender, DataGridViewCellCancelEventArgs args) {
                if (!((HistorySessionInfo)Sessions.Rows[args.RowIndex].Tag).Eligible) args.Cancel = true;
            };
            content.Controls.Add(Sessions, 0, 3);
            content.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "No automatic archival or archive expiration. Guidance changes checks only when you click Select matching sessions." }, 0, 4);
            var location = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            location.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            location.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            destination = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, AccessibleName = "Private archive parent folder" };
            var choose = new Button { Text = "Choose archive folder...", AutoSize = true };
            choose.Click += delegate {
                using (var picker = new FolderBrowserDialog { Description = "Choose a private existing folder outside Kenshi. A new uniquely named archive folder will be created here." })
                    if (picker.ShowDialog(this) == DialogResult.OK)
                    {
                        try { SetDestination(picker.SelectedPath); }
                        catch (Exception error) { ShowFailure(error); }
                    }
            };
            location.Controls.Add(destination, 0, 0); location.Controls.Add(choose, 1, 0);
            content.Controls.Add(location, 0, 5);
            preview = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                AccessibleName = "Archive selection and space preview" }; content.Controls.Add(preview, 0, 6);
            outcome = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Text = "Opening and selection save nothing.", AccessibleName = "History operation outcome and recovery path" };
            content.Controls.Add(outcome, 0, 7);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(8), WrapContents = true };
            var refresh = new Button { Text = "Refresh inventory", AutoSize = true };
            var previewButton = new Button { Text = "Preview archive", AutoSize = true };
            archive = new Button { Text = "Archive selected...", AutoSize = true };
            restore = new Button { Text = "Restore archive...", AutoSize = true };
            cancel = new Button { Text = "Cancel operation", AutoSize = true, Enabled = false };
            close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
            refresh.Click += async delegate { await RunOperation(async token => { await Reload(); return "Inventory refreshed. No files changed."; }); };
            previewButton.Click += async delegate { await PreviewArchive(); };
            archive.Click += async delegate { await ArchiveSelected(); };
            restore.Click += async delegate { await RestoreSelected(); };
            cancel.Click += delegate { RequestCancellation(); };
            buttons.Controls.AddRange(new Control[] { refresh, previewButton, archive, restore, cancel, close });
            Controls.Add(content); Controls.Add(buttons);
            CancelButton = close;
            FormClosing += delegate(object sender, FormClosingEventArgs args) {
                if (operation == null) return;
                RequestCancellation(); args.Cancel = true;
                outcome.Text = "Cancellation requested. Wait for the current verified file operation to finish, then close.";
            };
            LoadInventory(inventory);
        }

        internal void LoadInventory(HistoryStorageInventory value)
        {
            inventory = value; valid = true;
            Sessions.Rows.Clear();
            foreach (HistorySessionInfo session in inventory.Sessions.OrderByDescending(item => item.LastActivityUtc).ThenBy(item => item.SessionId))
            {
                int index = Sessions.Rows.Add(false, session.LastActivityUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                    session.SessionId, session.Recordings, session.Bytes.ToString("N0"), session.Status);
                DataGridViewRow row = Sessions.Rows[index]; row.Tag = session;
                if (!session.Eligible) { row.Cells[0].ReadOnly = true; row.DefaultCellStyle.ForeColor = SystemColors.GrayText; }
            }
            usage.Text = "Active tracking set: " + Bytes(inventory.TotalBytes) + "\r\nBaseline: " + Bytes(inventory.BaselineBytes) +
                "   Main journal: " + Bytes(inventory.JournalBytes) + "   Session originals: " + Bytes(inventory.SessionBytes) +
                "   Recovery/other: " + Bytes(inventory.OtherBytes) + "\r\n" +
                inventory.Sessions.Count(item => item.Eligible) + " eligible; " +
                inventory.Sessions.Count(item => item.Active) + " active; " +
                inventory.Sessions.Count(item => !item.Active && !item.FullyCompiled) + " uncompiled/dependent originals retained.";
            UpdateSelection();
        }

        internal List<HistorySessionInfo> SelectedSessions()
        {
            return Sessions.Rows.Cast<DataGridViewRow>()
                .Where(row => row.Tag != null && Convert.ToBoolean(row.Cells[0].Value))
                .Select(row => (HistorySessionInfo)row.Tag).Where(item => item.Eligible).ToList();
        }

        internal void ApplyGuidance(DateTime nowUtc)
        {
            DateTime? before = AgeEnabled.Checked ? (DateTime?)nowUtc.AddDays(-(double)AgeDays.Value) : null;
            int? keep = KeepEnabled.Checked ? (int?)KeepCount.Value : null;
            SetSelected(new HashSet<string>(inventory.Select(before, keep).Select(item => item.SessionId), StringComparer.Ordinal));
        }

        private void SetSelected(HashSet<string> ids)
        {
            foreach (DataGridViewRow row in Sessions.Rows)
            {
                var session = (HistorySessionInfo)row.Tag;
                row.Cells[0].Value = session.Eligible && ids.Contains(session.SessionId);
            }
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            if (archive == null || Sessions.Rows.Cast<DataGridViewRow>().Any(row => row.Tag == null)) return;
            var selected = SelectedSessions();
            UpdateCommands();
            preview.Text = selected.Count + " selected originals; source-folder reduction: " + Bytes(selected.Sum(item => item.Bytes)) +
                ".\r\nThe main journal and every recording remain. Preview destination space before confirming.";
        }

        private void UpdateCommands()
        {
            var selected = SelectedSessions();
            archive.Enabled = operation == null && valid && selected.Count > 0 && destination.Text.Length > 0;
            restore.Enabled = operation == null && valid;
        }

        internal void RequestCancellation() { if (operation != null) operation.Cancel(); }

        internal void SetDestination(string path)
        {
            RequirePrivateLocation(path);
            if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Choose an existing archive folder.");
            destination.Text = Path.GetFullPath(path); UpdateSelection();
        }

        private void RequirePrivateLocation(string path)
        {
            ExportStorage.RequireLocation(path, project.PathName);
            JournalProject.RequireArchivePath(path);
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            string protectedRoot = Path.GetFullPath(installationDirectory).TrimEnd(Path.DirectorySeparatorChar);
            if (String.Equals(full, protectedRoot, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(protectedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Keep history archives outside the Kenshi/FCS installation and Workshop upload folders.");
        }

        private async Task<string> SpacePreview()
        {
            Sessions.EndEdit();
            var selected = SelectedSessions();
            if (selected.Count == 0) throw new InvalidOperationException("Select eligible session originals first.");
            if (destination.Text.Length == 0) throw new InvalidOperationException("Choose a private archive folder first.");
            RequirePrivateLocation(destination.Text);
            long required = selected.Sum(item => item.Bytes);
            string root = Path.GetPathRoot(destination.Text);
            string free = await Task.Run(() => {
                try
                {
                    long available = new DriveInfo(root).AvailableFreeSpace;
                    if (available < required) throw new IOException("Destination has less free space than the selected session payload.");
                    return Bytes(available) + " free";
                }
                catch (ArgumentException error) { return "Free-space information unavailable for this destination: " + error.Message; }
            });
            bool sameDrive = String.Equals(root, Path.GetPathRoot(project.PathName), StringComparison.OrdinalIgnoreCase);
            string text = selected.Count + " closed compiled originals; " + Bytes(required) + " payload plus manifest space required.\r\n" +
                "Destination: " + destination.Text + " (" + free + ").\r\n" +
                (sameDrive ? "Same drive: reorganizes originals; does not free drive space (the manifest adds overhead)." :
                    "Different drive: removes " + Bytes(required) + " of redundant originals from the source drive after verified copying.") +
                "\r\nNo baseline, recordings, active/pending sessions, recovery files or editable work are deleted or saved.";
            preview.Text = text;
            return text;
        }

        internal Task PreviewArchive() { return RunOperation(token => SpacePreview()); }

        internal async Task ArchiveSelected()
        {
            await RunOperation(async token => {
                string text = await SpacePreview();
                token.ThrowIfCancellationRequested();
                if (!confirm(text + "\r\n\r\nArchive these selected originals now? Exact copies and a verified manifest will precede guarded removal."))
                    return "Archive confirmation declined. Nothing changed.";
                var selected = SelectedSessions().Select(item => item.SessionId).ToList();
                string folder = destination.Text;
                HistoryArchiveResult result = await Task.Run(() => project.ArchiveSessions(inventory, selected, folder, token));
                await Reload();
                return "Archived " + result.Changed + " originals (" + Bytes(result.Bytes) + "). All recordings retained.\r\nManifest: " + result.ManifestPath;
            });
        }

        private async Task RestoreSelected()
        {
            string path;
            using (var picker = new OpenFileDialog { Title = "Select a private Scribe archive manifest",
                Filter = "Scribe archive manifest (manifest.json)|manifest.json", CheckFileExists = true })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                path = picker.FileName;
            }
            await RestoreFrom(path);
        }

        internal async Task RestoreFrom(string path)
        {
            await RunOperation(async token => {
                RequirePrivateLocation(path);
                HistoryRestorePreview value = await Task.Run(() => project.PreviewRestoreArchive(path));
                token.ThrowIfCancellationRequested();
                if (!confirm("Restore archive: " + path + "\r\n" + value.Sessions.Count + " verified session originals; " +
                    value.AlreadyPresent + " already unchanged in the active tracking set.\r\nPayload: " + Bytes(value.Bytes) +
                    ". Nothing is overwritten, replayed twice or deleted from the archive. This does not restore a native mod or draft.\r\n\r\nRestore missing originals now?"))
                    return "Restore confirmation declined. Nothing changed.";
                HistoryArchiveResult result = await Task.Run(() => project.RestoreArchive(value, token));
                await Reload();
                return "Restored " + result.Changed + " originals; " + result.AlreadyPresent +
                    " already unchanged. Archive copies retained.\r\nManifest: " + result.ManifestPath;
            });
        }

        private async Task Reload() { LoadInventory(await Task.Run(() => project.HistoryInventory())); }

        private async Task RunOperation(Func<CancellationToken, Task<string>> action)
        {
            if (operation != null) return;
            operation = new CancellationTokenSource();
            content.Enabled = false; archive.Enabled = restore.Enabled = close.Enabled = false; cancel.Enabled = true;
            CancelButton = cancel;
            try
            {
                bool reload = false;
                try
                {
                    outcome.Text = "Validating history/storage. Cancellation stops at a safe file-operation boundary.";
                    outcome.Text = await action(operation.Token);
                }
                catch (OperationCanceledException error) { outcome.Text = error.Message; reload = true; }
                catch (Exception error) { ShowFailure(error); reload = true; }
                if (reload) await ReloadAfterFailure();
            }
            finally
            {
                operation.Dispose(); operation = null; content.Enabled = true;
                close.Enabled = true; cancel.Enabled = false; CancelButton = close; UpdateCommands();
            }
        }

        private async Task ReloadAfterFailure()
        {
            try { await Reload(); }
            catch (Exception error)
            {
                valid = false; HadError = true;
                outcome.Text += "\r\nHistory/storage no longer validates; reopen the project before another transfer. " + error.Message;
                Console.Error.WriteLine("FCS-Scribe Manage History validation: " + error);
            }
        }

        private void ShowFailure(Exception error)
        {
            HadError = true; outcome.Text = error.Message;
            Console.Error.WriteLine("FCS-Scribe Manage History: " + error);
        }

        private static string Bytes(long value) { return value.ToString("N0") + " bytes"; }
    }
}
