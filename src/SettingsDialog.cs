using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace FcsScribe
{
    internal sealed class RecordingRangeDialog : Form
    {
        private readonly ComboBox first = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox last = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Label summary = new Label { Dock = DockStyle.Fill, AutoSize = true };
        private readonly TextBox preview = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
            WordWrap = false, ScrollBars = ScrollBars.Both, MaxLength = Int32.MaxValue };
        private readonly Button export = new Button { Text = "Export selected range...", AutoSize = true, DialogResult = DialogResult.OK };
        private readonly IList<RecordingEvidence> recordings;
        public int First { get { return first.SelectedIndex; } }
        public int Last { get { return last.SelectedIndex; } }

        public RecordingRangeDialog(IList<RecordingEvidence> recordings)
        {
            if (recordings == null || recordings.Count == 0) throw new ArgumentException("Recordings are required.", "recordings");
            this.recordings = recordings;
            Text = "Choose AI recording range";
            Width = 980; Height = 680; MinimumSize = new System.Drawing.Size(660, 480);
            StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Padding = new Padding(12) };
            for (int i = 0; i < 8; i++) layout.RowStyles.Add(new RowStyle(i == 6 ? SizeType.Percent : SizeType.AutoSize, i == 6 ? 100 : 0));
            layout.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill,
                Text = "Choose first and last entries (inclusive). These are ordered observations, not a net diff. " +
                    "Unrecorded edits are explicitly labelled; exporting never records or compiles them. " +
                    "Notes will be validated against the CURRENT editor state, even for an older range. " +
                    "Overlapping ranges can document the same evidence again; compare citations and existing notes." }, 0, 0);
            layout.Controls.Add(new Label { Text = "First recording (inclusive):", AutoSize = true }, 0, 1);
            layout.Controls.Add(first, 0, 2);
            layout.Controls.Add(new Label { Text = "Last recording (inclusive):", AutoSize = true }, 0, 3);
            layout.Controls.Add(last, 0, 4);
            layout.Controls.Add(summary, 0, 5);
            layout.Controls.Add(preview, 0, 6);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(export); buttons.Controls.Add(cancel);
            layout.Controls.Add(buttons, 0, 7);
            foreach (RecordingEvidence recording in recordings)
            {
                string label = (first.Items.Count + 1) + " | " + recording.RecordedUtc + " | " +
                    (recording.Kind == "pending" ? "UNRECORDED editor changes" : recording.Kind) +
                    " | " + recording.Packet.Changes.Count + " change IDs | session " + (recording.SessionId ?? "(legacy)");
                first.Items.Add(label); last.Items.Add(label);
            }
            first.SelectedIndex = 0; last.SelectedIndex = recordings.Count - 1;
            first.SelectedIndexChanged += delegate { UpdateRange(); };
            last.SelectedIndexChanged += delegate { UpdateRange(); };
            Controls.Add(layout); AcceptButton = export; CancelButton = cancel;
            UpdateRange();
        }

        private void UpdateRange()
        {
            bool valid = First >= 0 && Last >= First;
            var selected = valid ? recordings.Skip(First).Take(Last - First + 1).ToList() : new List<RecordingEvidence>();
            int changes = selected.Sum(x => x.Packet.Changes.Count);
            export.Enabled = valid && changes > 0;
            summary.Text = !valid ? "Choose a last recording at or after the first." :
                selected.Count + " entries, " + changes + " change IDs, " + selected.Count(x => x.Kind == "pending") +
                " unrecorded entry. " + (changes == 0 ? "Context-only updates are not record-change evidence." :
                "Every change ID in this range must be covered exactly once in the draft.");
            var text = new StringBuilder("PARTIAL EXTRACTION - inspect names, values and all warnings before sharing.\r\n");
            foreach (RecordingEvidence recording in selected)
            {
                text.AppendLine("\r\n" + recording.EntryId + " | " + recording.RecordedUtc + " | " + recording.Kind +
                    " | original batch " + recording.Packet.BatchId);
                foreach (string warning in recording.Packet.Context.Warnings) text.AppendLine("WARNING: " + warning);
                ScribeWindow.AppendChanges(text, RangeExport.Changes(new[] { recording }));
            }
            preview.Text = text.ToString();
        }
    }

    internal sealed class SettingsDialog : Form
    {
        private readonly ComboBox interval = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        private readonly CheckBox activeFolder = new CheckBox { Text = "Default JSON exports to the active mod's folder", AutoSize = true };
        private readonly TextBox directory = new TextBox { Width = 440 };
        public PluginSettings Value { get; private set; }

        public SettingsDialog(PluginSettings settings, string settingsPath = null, Exception loadError = null)
        {
            Text = "FCS-Scribe settings";
            Width = 640; Height = loadError == null ? 400 : 540;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(12) };
            if (loadError != null)
                layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new System.Drawing.Size(580, 0),
                    Text = "SETTINGS RECOVERY: " + loadError.Message + "\r\n" + settingsPath +
                        "\r\nProposed values start at Manual/active-mod defaults. Reload to retry reading the file, " +
                        "or explicitly Save repaired settings to preserve its original bytes in a unique recovery backup. " +
                        "An unreadable original must become readable and be reloaded before it can be replaced." });
            layout.Controls.Add(new Label { Text = "Review and save a validated checkpoint:", AutoSize = true });
            interval.Items.AddRange(new object[] { "Manual", "Automatic - 30 seconds", "Automatic - 1 minute",
                "Automatic - 5 minutes", "Automatic - 10 minutes" });
            interval.SelectedIndex = Array.IndexOf(PluginSettings.Intervals, settings.ReviewSeconds);
            layout.Controls.Add(interval);
            activeFolder.Checked = String.IsNullOrEmpty(settings.ExportDirectory);
            layout.Controls.Add(activeFolder);
            directory.Text = settings.ExportDirectory ?? "";
            directory.Enabled = !activeFolder.Checked;
            var folders = new FlowLayoutPanel { AutoSize = true };
            var browse = new Button { Text = "Browse...", AutoSize = true, Enabled = !activeFolder.Checked };
            browse.Click += delegate {
                using (var picker = new FolderBrowserDialog { Description = "Default JSON export directory" })
                    if (picker.ShowDialog(this) == DialogResult.OK) directory.Text = picker.SelectedPath;
            };
            activeFolder.CheckedChanged += delegate { directory.Enabled = browse.Enabled = !activeFolder.Checked; };
            folders.Controls.Add(directory); folders.Controls.Add(browse); layout.Controls.Add(folders);
            layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new System.Drawing.Size(540, 0),
                Text = "Recordings are flushed to timestamped session logs at each interval. Automatic mode compiles them into the main journal every 30 minutes. Manual mode uses Review changes and Compile journal. Neither saves the mod or applies notes." });
            var buttons = new FlowLayoutPanel { AutoSize = true };
            var save = new Button { Text = loadError == null ? "Save settings" : "Save repaired settings", AutoSize = true };
            var reload = new Button { Text = "Discard edits and reload", AutoSize = true, DialogResult = DialogResult.Retry };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            save.Click += delegate {
                try
                {
                    if (interval.SelectedIndex < 0) throw new InvalidDataException("Choose a review interval.");
                    Value = new PluginSettings {
                        ReviewSeconds = PluginSettings.Intervals[interval.SelectedIndex],
                        ExportDirectory = activeFolder.Checked ? null : directory.Text
                    };
                    Value.ValidateForUse();
                    if (!activeFolder.Checked && (String.IsNullOrWhiteSpace(directory.Text) || !Directory.Exists(directory.Text)))
                        throw new DirectoryNotFoundException("Choose an existing custom export directory.");
                    DialogResult = DialogResult.OK;
                }
                catch (Exception error) { Plugin.ShowError(error); }
            };
            buttons.Controls.Add(save); buttons.Controls.Add(reload); buttons.Controls.Add(cancel); layout.Controls.Add(buttons);
            Controls.Add(layout); AcceptButton = save; CancelButton = cancel;
        }
    }

    internal sealed class TrackingChoiceDialog : Form
    {
        public bool Resume { get; private set; }
        public TrackingChoiceDialog()
        {
            Text = "Start or resume tracking"; Width = 640; Height = 360;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12),
                FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new System.Drawing.Size(590, 0),
                Text = "Create a project baseline or resume its tracking JSON. Every start/resume opens a new timestamped session log in <project>.json.scribe\\sessions. Recordings contain only changed records/context and are durable before compilation. Keep the baseline and uncompiled session files together; nothing is automatically deleted. This is NOT a saved-mod backup. Replacing this session clears the editable draft. All tracking files may be private: do not include them in AI handoffs or Workshop uploads; use compact AI exports." });
            var create = new Button { Text = "New tracking JSON", AutoSize = true };
            var resume = new Button { Text = "Resume existing JSON", AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            create.Click += delegate { Resume = false; DialogResult = DialogResult.OK; };
            resume.Click += delegate { Resume = true; DialogResult = DialogResult.OK; };
            var buttons = new FlowLayoutPanel { AutoSize = true };
            buttons.Controls.Add(create); buttons.Controls.Add(resume); buttons.Controls.Add(cancel);
            layout.Controls.Add(buttons);
            Controls.Add(layout); CancelButton = cancel;
        }
    }
}
