using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
                            window = new ScribeWindow(new FcsAdapter(assembly, editor));
                        window.Show(editor);
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
        private Packet exported;
        private readonly TextBox review = TextArea(true);
        private readonly TextBox draft = TextArea(false);
        private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 48 };

        public ScribeWindow(FcsAdapter adapter)
        {
            this.adapter = adapter;
            Text = "FCS-Scribe - experimental documentation";
            Width = 1080;
            Height = 780;
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            AddButton(actions, "Start tracking", Start);
            AddButton(actions, "Review changes", Review);
            AddButton(actions, "Export AI packet", Export);
            AddButton(actions, "Import draft", Import);
            AddButton(actions, "Paste draft", delegate { draft.Text = Clipboard.GetText(); });
            AddButton(actions, "Apply approved notes", Apply);
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var changesTab = new TabPage("Changes and warnings");
            changesTab.Controls.Add(review);
            var draftTab = new TabPage("Editable draft JSON");
            draftTab.Controls.Add(draft);
            tabs.TabPages.Add(changesTab);
            tabs.TabPages.Add(draftTab);
            Controls.Add(tabs);
            Controls.Add(actions);
            Controls.Add(status);
            status.Text = "Open a writable mod. Finish editing fields before capturing. Baselines live only until this window closes.";
        }

        private void Start()
        {
            if (MessageBox.Show(this, "Capture the current in-memory state as a new baseline? " +
                "Earlier edits will not be catalogued. This replaces any existing baseline and exported batch; " +
                "copy an edited draft elsewhere first. No mod will be saved.",
                "Start tracking", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            tracker = new Tracker(adapter.Capture());
            exported = null;
            draft.Clear();
            review.Clear();
            status.Text = "Tracking started. Earlier edits are outside this baseline. Saving or applying notes does not advance it.";
        }

        private Packet Compare()
        {
            if (tracker == null) throw new InvalidOperationException("Select Start tracking first.");
            // Moving focus out of the editor commits normal control validation before model capture.
            adapter.EnsureReady();
            return tracker.Export(adapter.Capture());
        }

        private void Review()
        {
            Packet packet = Compare();
            ShowChanges(packet);
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
            status.Text = packet.Changes.Count + " change entries (stored and effective entries may describe the same edit). " +
                "Baseline unchanged.";
        }

        private void Export()
        {
            Packet packet = Compare();
            if (packet.Changes.Count == 0) throw new InvalidOperationException("No captured changes to document.");
            if (exported != null && !String.IsNullOrWhiteSpace(draft.Text) &&
                MessageBox.Show(this, "Exporting replaces the editable draft with a blank response template. Continue?",
                    "Replace draft", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            using (var dialog = new SaveFileDialog { Filter = "JSON packet (*.json)|*.json", FileName = "fcs-scribe-packet.json" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!String.Equals(Path.GetExtension(dialog.FileName), ".json", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Export requires a .json filename; mod files are never export targets.");
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
            using (var dialog = new OpenFileDialog { Filter = "JSON draft (*.json)|*.json|All files (*.*)|*.*" })
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

        private static void AddButton(Control parent, string label, Action action)
        {
            var button = new Button { Text = label, AutoSize = true };
            button.Click += delegate {
                try { action(); }
                catch (Exception error) { Plugin.ShowError(error); }
            };
            parent.Controls.Add(button);
        }
    }
}
