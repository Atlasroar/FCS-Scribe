using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FcsScribe
{
    internal sealed class RecordingHistoryDialog : Form
    {
        private sealed class Row
        {
            public readonly int Index;
            public readonly RecordingEvidence Evidence;
            public Row(int index, RecordingEvidence evidence) { Index = index; Evidence = evidence; }
            public override string ToString() { return RecordingRangeDialog.Label(Evidence, Index); }
        }

        private readonly IList<RecordingEvidence> recordings;
        private readonly List<Row> rows;
        private readonly ComboBox session = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330,
            AccessibleName = "History session filter" };
        private readonly TextBox search = new TextBox { Width = 260, AccessibleName = "Search history" };
        private readonly ListBox entries = new ListBox { Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended,
            HorizontalScrollbar = true, IntegralHeight = false, AccessibleName = "Recording history entries" };
        private readonly TextBox preview = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
            WordWrap = false, ScrollBars = ScrollBars.Both, MaxLength = Int32.MaxValue,
            AccessibleName = "Selected recording evidence and warnings" };
        private readonly Label summary = new Label { Dock = DockStyle.Fill, AutoSize = true };
        private readonly Button ai = new Button { Text = "Export AI range...", AutoSize = true };
        private readonly Button markdown = new Button { Text = "Export Markdown range...", AutoSize = true };
        public int First { get; private set; }
        public int Last { get; private set; }
        public bool Markdown { get; private set; }

        public RecordingHistoryDialog(IList<RecordingEvidence> recordings)
        {
            if (recordings == null || recordings.Any(x => x == null || x.Packet == null))
                throw new ArgumentException("A validated recording catalogue is required.", "recordings");
            if (recordings.Any(x => x.Packet.Context.Workshop != null && x.Packet.Context.Workshop.Issues != null &&
                x.Packet.Context.Workshop.Issues.Any(issue => issue == null)))
                throw new InvalidDataException("Missing Workshop issue in recording evidence.");
            this.recordings = recordings;
            rows = recordings.Select((evidence, index) => new Row(index, evidence)).ToList();
            First = Last = -1;
            Text = "Browse Scribe recording history";
            Width = 1100; Height = 760; MinimumSize = new System.Drawing.Size(760, 560);
            StartPosition = FormStartPosition.CenterParent; MinimizeBox = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(12) };
            for (int i = 0; i < 6; i++)
                layout.RowStyles.Add(new RowStyle(i == 2 || i == 4 ? SizeType.Percent : SizeType.AutoSize,
                    i == 2 ? 35 : i == 4 ? 65 : 0));
            layout.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill,
                Text = "READ-ONLY history snapshot. Close and reopen to refresh. These are sampled observations, not saved mod state or an event log. " +
                    "Sessions with evidence, legacy checkpoints/imports and UNRECORDED current edits are shown. Nothing is pruned or restored. " +
                    "Use Ctrl/Shift to select entries; exports always preview the complete inclusive range, including hidden entries." }, 0, 0);
            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            filters.Controls.Add(new Label { Text = "Session:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            filters.Controls.Add(session);
            filters.Controls.Add(new Label { Text = "Search:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            filters.Controls.Add(search);
            layout.Controls.Add(filters, 0, 1); layout.Controls.Add(entries, 0, 2);
            layout.Controls.Add(summary, 0, 3); layout.Controls.Add(preview, 0, 4);
            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(ai); buttons.Controls.Add(markdown); buttons.Controls.Add(close);
            layout.Controls.Add(buttons, 0, 5); Controls.Add(layout); CancelButton = close;
            session.Items.Add("All sessions and unrecorded edits");
            foreach (string key in recordings.Select(SessionKey).Distinct(StringComparer.Ordinal)) session.Items.Add(key);
            session.SelectedIndex = 0;
            session.SelectedIndexChanged += delegate { Filter(); };
            search.TextChanged += delegate { Filter(); };
            entries.SelectedIndexChanged += delegate { UpdateSelection(); };
            ai.Click += delegate { ChooseExport(false); };
            markdown.Click += delegate { ChooseExport(true); };
            Filter();
        }

        private static string SessionKey(RecordingEvidence recording)
        {
            return recording.Kind == "pending" ? "UNRECORDED editor changes" :
                recording.SessionId ?? "Legacy checkpoints/imports (no session ID)";
        }

        private static bool Contains(string text, string query)
        { return text != null && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0; }

        private static bool Matches(RecordingEvidence recording, string query)
        {
            if (query.Length == 0) return true;
            Packet packet = recording.Packet;
            IEnumerable<string> context = new[] { recording.EntryId, recording.SessionId, recording.RecordedUtc,
                recording.Kind, SessionKey(recording), packet.BatchId,
                packet.BaselineId, packet.SourceFingerprint, packet.Context.ModName, packet.Context.Scope }
                .Concat(packet.Context.Warnings).Concat(packet.Context.LoadOrder).Concat(packet.Context.Dependencies)
                .Concat(packet.Context.LockedMods);
            WorkshopMetadata workshop = packet.Context.Workshop;
            if (workshop != null)
            {
                context = context.Concat(new[] { workshop.Source, workshop.CaptureStatus }).Concat(workshop.Warnings);
                if (workshop.Issues != null)
                    context = context.Concat(workshop.Issues.SelectMany(x => new[] { x.Component, x.Code, x.Message }));
            }
            return context.Any(x => Contains(x, query)) ||
                packet.Changes.Any(change => new[] { change.Id, change.RecordId, change.RecordType, change.DisplayName,
                    change.Operation, change.Layer, change.Path, change.Before == null ? null : change.Before.Kind,
                    change.Before == null ? null : change.Before.Text, change.After == null ? null : change.After.Kind,
                    change.After == null ? null : change.After.Text, change.BeforeSource, change.AfterSource,
                    change.ReferencedId, change.ReferencedName }.Any(x => Contains(x, query)));
        }

        private void Filter()
        {
            entries.BeginUpdate();
            try
            {
                entries.Items.Clear();
                string query = search.Text.Trim();
                foreach (Row row in rows)
                    if ((session.SelectedIndex == 0 || SessionKey(row.Evidence) == (string)session.SelectedItem) &&
                        Matches(row.Evidence, query)) entries.Items.Add(row);
                if (entries.Items.Count > 0) entries.SelectedIndex = 0;
            }
            finally { entries.EndUpdate(); }
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            var selected = entries.SelectedItems.Cast<Row>().OrderBy(x => x.Index).ToList();
            ai.Enabled = markdown.Enabled = selected.Count > 0;
            summary.Text = entries.Items.Count + " of " + recordings.Count + " entries visible; " + selected.Count +
                " selected. Filters do not remove evidence from an inclusive export range.";
            preview.Text = selected.Count > 0 ? RecordingRangeDialog.Preview(selected.Select(x => x.Evidence)) :
                recordings.Count == 0 ? "No recordings yet. An empty session/baseline contains no change evidence. " +
                    "Save Changes to record observations; this browser does not record them." :
                    "No entries selected or matching these filters. Clear the search or choose another session.";
        }

        private void ChooseExport(bool asMarkdown)
        {
            var selected = entries.SelectedItems.Cast<Row>().Select(x => x.Index).OrderBy(x => x).ToList();
            if (selected.Count == 0) throw new InvalidOperationException("Select history entries before exporting.");
            using (var range = new RecordingRangeDialog(recordings, selected[0], selected[selected.Count - 1]))
            {
                if (range.ShowDialog(this) != DialogResult.OK) return;
                First = range.First; Last = range.Last; Markdown = asMarkdown;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
