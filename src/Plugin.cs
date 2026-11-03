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
        private Packet exported;
        private readonly TextBox review = TextArea(true);
        private readonly TextBox draft = TextArea(false);
        private readonly TextBox workshop = TextArea(true);
        private readonly Label status = new Label { Dock = DockStyle.Bottom, Height = 48 };
        private const string HelpHint = "Hover over a button, or focus it with Tab, to see what it does.";
        private readonly Label help = new Label { Dock = DockStyle.Bottom, Height = 48, Text = HelpHint,
            BorderStyle = BorderStyle.FixedSingle };

        public ScribeWindow(FcsAdapter adapter, Form editor)
        {
            this.adapter = adapter;
            if (!editor.IsMdiContainer) throw new NotSupportedException("The FCS main window is not an MDI container.");
            MdiParent = editor;
            Text = "FCS-Scribe - experimental documentation";
            Width = 1080;
            Height = 780;
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            AddButton(actions, "Start tracking", "Capture a new baseline. Replaces prior tracking and the exported draft; earlier edits are excluded. Does not save.", Start);
            AddButton(actions, "Review changes", "Compare the current model against your baseline, including unsaved edits already committed to FCS. Does not advance tracking.", Review);
            AddButton(actions, "Export AI packet", "Save change evidence, Workshop context and a blank AI response template as JSON. Replaces the editable draft; sends nothing online.", Export);
            AddButton(actions, "Export Workshop info", "Save current local Workshop information and preview-image details as JSON. No tracking baseline or record changes required.", ExportWorkshop);
            AddButton(actions, "Archive preview image", "Copy the local Workshop preview, byte-for-byte, to a new image file outside Kenshi. Does not download or change the original.", ArchivePreview);
            AddButton(actions, "Import draft", "Load an AI response JSON into the editable draft. Requires an exported batch; nothing is applied until approval.", Import);
            AddButton(actions, "Paste draft", "Replace the editable draft with clipboard text. Review and validate it before applying; nothing is applied by pasting.", delegate { draft.Text = Clipboard.GetText(); });
            AddButton(actions, "Apply approved notes", "Validate the draft and ask for approval, then append record Notes/TODO entries and mark FCS dirty. Does not save or edit Workshop notes.", Apply);
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
                if (tabs.SelectedTab != workshopTab) return;
                try { workshop.Text = Json.Pretty(Json.Write(adapter.CaptureWorkshop())); }
                catch (Exception error) { workshop.Clear(); Plugin.ShowError(error); }
            };
            Controls.Add(tabs);
            Controls.Add(actions);
            Controls.Add(help);
            Controls.Add(status);
            status.Text = "Open a writable mod. Finish editing fields before capturing. Baselines live only until this window closes.";
        }

        private void Start()
        {
            if (MessageBox.Show(this, "Capture the current in-memory state as a new baseline? " +
                "Earlier edits will not be catalogued. This replaces any existing baseline and exported batch; " +
                "copy an edited draft elsewhere first. No mod will be saved.",
                "Start tracking", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            Snapshot initial = adapter.Capture();
            tracker = new Tracker(initial);
            exported = null;
            draft.Clear();
            review.Clear();
            workshop.Text = Json.Pretty(Json.Write(initial.Workshop));
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
            workshop.Text = Json.Pretty(Json.Write(packet.Context.Workshop));
            status.Text = packet.Changes.Count + " change entries (stored and effective entries may describe the same edit). " +
                "Baseline unchanged.";
        }

        private void ExportWorkshop()
        {
            WorkshopExport export = adapter.CaptureWorkshopExport();
            using (var dialog = new SaveFileDialog { Filter = "JSON metadata (*.json)|*.json", FileName = "fcs-scribe-workshop.json" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!String.Equals(Path.GetExtension(dialog.FileName), ".json", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Workshop export requires a .json filename.");
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
                Title = "Archive preview image - choose a new filename outside Kenshi"
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

        private void AddButton(Control parent, string label, string description, Action action)
        {
            var button = new Button { Text = label, AutoSize = true, AccessibleDescription = description };
            button.MouseEnter += delegate { help.Text = description; };
            button.MouseLeave += delegate { help.Text = button.Focused ? description : HelpHint; };
            button.Enter += delegate { help.Text = description; };
            button.Leave += delegate { help.Text = HelpHint; };
            button.Click += delegate {
                try { action(); }
                catch (Exception error) { Plugin.ShowError(error); }
            };
            parent.Controls.Add(button);
        }
    }
}
