using System;
using System.IO;
using System.Windows.Forms;

namespace FcsScribe
{
    internal sealed class SettingsDialog : Form
    {
        private readonly ComboBox interval = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        private readonly CheckBox activeFolder = new CheckBox { Text = "Default JSON exports to the active mod's folder", AutoSize = true };
        private readonly TextBox directory = new TextBox { Width = 440 };
        public PluginSettings Value { get; private set; }

        public SettingsDialog(PluginSettings settings)
        {
            Text = "FCS-Scribe settings";
            Width = 640; Height = 360;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(12) };
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
            var save = new Button { Text = "Save settings", AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            save.Click += delegate {
                try
                {
                    if (interval.SelectedIndex < 0) throw new InvalidDataException("Choose a review interval.");
                    Value = new PluginSettings {
                        ReviewSeconds = PluginSettings.Intervals[interval.SelectedIndex],
                        ExportDirectory = activeFolder.Checked ? null : directory.Text
                    };
                    Value.Validate();
                    if (!activeFolder.Checked && (String.IsNullOrWhiteSpace(directory.Text) || !Directory.Exists(directory.Text)))
                        throw new DirectoryNotFoundException("Choose an existing custom export directory.");
                    DialogResult = DialogResult.OK;
                }
                catch (Exception error) { Plugin.ShowError(error); }
            };
            buttons.Controls.Add(save); buttons.Controls.Add(cancel); layout.Controls.Add(buttons);
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
