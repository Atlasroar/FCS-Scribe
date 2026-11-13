using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace FcsScribe
{
    internal sealed class ModWorkView
    {
        internal readonly TabPage NotesTab = new TabPage("Notes");
        internal readonly TabPage TodoTab = new TabPage("Mod To Do");
        internal readonly TextBox Notes = new TextBox { Dock = DockStyle.Fill, Multiline = true,
            AcceptsReturn = true, AcceptsTab = true, ScrollBars = ScrollBars.Both, MaxLength = Int32.MaxValue };
        internal readonly DataGridView Todos = new DataGridView { Dock = DockStyle.Fill,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            AutoGenerateColumns = false, RowHeadersVisible = false, MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, BackgroundColor = SystemColors.Window,
            AccessibleName = "Mod To Do task table" };
        internal readonly TextBox Search = new TextBox { Width = 190, AccessibleName = "Search Mod To Do tasks" };
        internal readonly ComboBox SearchField = Choice("Task search field", 130);
        internal readonly ComboBox State = Choice("Task completion filter", 105);
        internal readonly ComboBox ItemType = Choice("Task item type filter", 140);
        internal readonly ComboBox Priority = Choice("Task priority filter", 115);
        internal readonly DateTimePicker CreatedFrom = DateFilter("Created from date");
        internal readonly DateTimePicker CreatedTo = DateFilter("Created to date");
        internal readonly CheckBox UseCreatedFrom = new CheckBox { Text = "From:", AutoSize = true };
        internal readonly CheckBox UseCreatedTo = new CheckBox { Text = "To:", AutoSize = true };
        private readonly TableLayoutPanel todoWorkspace = new TableLayoutPanel {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        private readonly FlowLayoutPanel filters = new FlowLayoutPanel {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        private readonly Label summary = new Label { Dock = DockStyle.Fill, AutoSize = true };
        private readonly Label notesPath = new Label { Dock = DockStyle.Top, AutoSize = true };
        private readonly Label todoPath = new Label { Dock = DockStyle.Top, AutoSize = true };
        private readonly Button edit = new Button { Text = "Edit selected task...", AutoSize = true };
        private readonly Button remove = new Button { Text = "Remove selected task", AutoSize = true };
        private bool binding, todoEnabled, refreshQueued;
        private ModTodoColumn? sort;
        private bool descending;
        internal ModWork Work { get; private set; }
        internal Exception LoadError { get; private set; }

        private sealed class SearchChoice
        {
            public readonly ModTodoField Field;
            private readonly string label;
            public SearchChoice(ModTodoField field, string label) { Field = field; this.label = label; }
            public override string ToString() { return label; }
        }

        private static ComboBox Choice(string name, int width)
        { return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width, AccessibleName = name }; }

        private static DateTimePicker DateFilter(string name)
        {
            return new DateTimePicker { Format = DateTimePickerFormat.Short,
                Enabled = false, Width = 120, AccessibleName = name };
        }

        private void AddTextColumn(ModTodoColumn column, string text, int width)
        {
            Todos.Columns.Add(new DataGridViewTextBoxColumn { Name = column.ToString(), HeaderText = text,
                ReadOnly = true, Width = width, MinimumWidth = width, SortMode = DataGridViewColumnSortMode.Programmatic,
                AutoSizeMode = column == ModTodoColumn.Description ? DataGridViewAutoSizeColumnMode.Fill :
                    DataGridViewAutoSizeColumnMode.None });
        }

        internal ModWorkView(Action<Action> action, Action reload)
        {
            NotesTab.Controls.Add(Notes); NotesTab.Controls.Add(notesPath);
            TodoTab.Controls.Add(todoWorkspace); TodoTab.Controls.Add(todoPath);
            todoWorkspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            todoWorkspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            todoWorkspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            todoWorkspace.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Todos.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Done", HeaderText = "Done",
                Width = 45, SortMode = DataGridViewColumnSortMode.Programmatic });
            AddTextColumn(ModTodoColumn.ItemName, "Item name", 160);
            AddTextColumn(ModTodoColumn.StringId, "String ID", 170);
            AddTextColumn(ModTodoColumn.ItemType, "Item type", 115);
            var priorityColumn = new DataGridViewComboBoxColumn { Name = "Priority", HeaderText = "Priority",
                Width = 100, SortMode = DataGridViewColumnSortMode.Programmatic,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton };
            priorityColumn.Items.AddRange("Unspecified", "Low", "Normal", "High");
            Todos.Columns.Add(priorityColumn);
            AddTextColumn(ModTodoColumn.CreatedDate, "Created date", 145);
            Todos.Columns["CreatedDate"].DefaultCellStyle.Format = "g";
            Todos.Columns["CreatedDate"].DefaultCellStyle.NullValue = "Unknown";
            AddTextColumn(ModTodoColumn.Description, "Description", 200);
            foreach (var option in new[] {
                new SearchChoice(ModTodoField.All, "All fields"), new SearchChoice(ModTodoField.ItemName, "Item name"),
                new SearchChoice(ModTodoField.StringId, "String ID"), new SearchChoice(ModTodoField.ItemType, "Item type"),
                new SearchChoice(ModTodoField.Priority, "Priority"), new SearchChoice(ModTodoField.CreatedDate, "Created date"),
                new SearchChoice(ModTodoField.Description, "Description") }) SearchField.Items.Add(option);
            SearchField.SelectedIndex = 0;
            State.Items.AddRange(new object[] { "All", "Open", "Completed" }); State.SelectedIndex = 0;
            Priority.Items.AddRange(new object[] { "All priorities", "Unspecified", "Low", "Normal", "High" }); Priority.SelectedIndex = 0;
            ItemType.Items.Add("All item types"); ItemType.SelectedIndex = 0;
            foreach (Control control in new Control[] { Caption("Search:"), Search, SearchField, State,
                ItemType, Priority, UseCreatedFrom, CreatedFrom, UseCreatedTo, CreatedTo }) filters.Controls.Add(control);
            var clear = new Button { Text = "Clear filters", AutoSize = true };
            clear.Click += delegate {
                binding = true;
                try
                {
                    Search.Clear(); SearchField.SelectedIndex = State.SelectedIndex = ItemType.SelectedIndex = Priority.SelectedIndex = 0;
                    UseCreatedFrom.Checked = UseCreatedTo.Checked = false;
                }
                finally { binding = false; }
                RefreshTasks();
            };
            filters.Controls.Add(clear);
            Search.TextChanged += delegate { if (!binding) RefreshTasks(); };
            foreach (ComboBox choice in new[] { SearchField, State, ItemType, Priority })
                choice.SelectedIndexChanged += delegate { if (!binding) RefreshTasks(); };
            CreatedFrom.ValueChanged += delegate { if (!binding) RefreshTasks(); };
            CreatedTo.ValueChanged += delegate { if (!binding) RefreshTasks(); };
            UseCreatedFrom.CheckedChanged += delegate {
                CreatedFrom.Enabled = UseCreatedFrom.Checked;
                if (!binding) RefreshTasks();
            };
            UseCreatedTo.CheckedChanged += delegate {
                CreatedTo.Enabled = UseCreatedTo.Checked;
                if (!binding) RefreshTasks();
            };
            Todos.ColumnHeaderMouseClick += delegate(object sender, DataGridViewCellMouseEventArgs e) {
                SortBy((ModTodoColumn)e.ColumnIndex);
            };
            Todos.SelectionChanged += delegate { if (!binding) UpdateSelection(); };
            Todos.CurrentCellDirtyStateChanged += delegate {
                if (!binding && Todos.IsCurrentCellDirty &&
                    (Todos.CurrentCell is DataGridViewCheckBoxCell || Todos.CurrentCell is DataGridViewComboBoxCell))
                    Todos.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            Todos.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e) {
                if (binding || e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var task = (ModTodo)Todos.Rows[e.RowIndex].Tag;
                string column = Todos.Columns[e.ColumnIndex].Name;
                action(delegate {
                    object value = Todos.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                    if (column == "Done")
                    {
                        if (!(value is bool)) throw new InvalidDataException("A TODO completion value is missing.");
                        task.Done = (bool)value;
                    }
                    else if (column == "Priority")
                    {
                        string priority = value as string;
                        if (priority != "Unspecified" && priority != "Low" && priority != "Normal" && priority != "High")
                            throw new InvalidDataException("Unknown TODO priority selection.");
                        task.Priority = priority == "Unspecified" ? null : priority;
                    }
                    else throw new InvalidOperationException("Only TODO completion and priority can be edited in the table.");
                });
                QueueRefresh();
            };
            Todos.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e) {
                action(delegate { throw new InvalidOperationException("The TODO table could not display or commit a task value.", e.Exception); });
                e.ThrowException = false;
            };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            var reloadNotes = new Button { Text = "Reload notes / TODOs...", AutoSize = true };
            var reloadTodo = new Button { Text = "Reload notes / TODOs...", AutoSize = true };
            edit.Click += delegate { action(delegate {
                ModTodo task = SelectedTask();
                if (task == null) throw new InvalidOperationException("Select a task first.");
                using (var dialog = new ModTodoDialog(task.Text))
                    if (dialog.ShowDialog(TodoTab.FindForm()) == DialogResult.OK)
                    { task.Text = dialog.Value; RefreshTasks(); }
            }); };
            remove.Click += delegate { action(delegate {
                ModTodo task = SelectedTask();
                if (task == null) throw new InvalidOperationException("Select a task first.");
                if (MessageBox.Show(TodoTab.FindForm(), "Remove this Scribe task? Native FCS notes are unchanged.",
                    "Remove Mod To Do", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                Work.Todos.Remove(task); RefreshTasks();
            }); };
            reloadNotes.Click += delegate { reload(); }; reloadTodo.Click += delegate { reload(); };
            buttons.Controls.Add(edit); buttons.Controls.Add(remove); buttons.Controls.Add(reloadTodo);
            todoWorkspace.Controls.Add(filters, 0, 0); todoWorkspace.Controls.Add(summary, 0, 1);
            todoWorkspace.Controls.Add(Todos, 0, 2); todoWorkspace.Controls.Add(buttons, 0, 3);
            var notesButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
            notesButtons.Controls.Add(reloadNotes); NotesTab.Controls.Add(notesButtons);
            todoWorkspace.Visible = false;
        }

        private static Label Caption(string text)
        { return new Label { Text = text, AutoSize = true, Padding = new Padding(0, 5, 0, 0) }; }

        internal void Load(string root, string modName, PluginSettings settings)
        {
            try { Adopt(new ModWork(root, modName, settings.ModNotesEnabled, settings.ModTodoEnabled), settings); }
            catch (Exception error)
            {
                LoadError = error; Work = null;
                Notes.ReadOnly = true; todoEnabled = settings.ModTodoEnabled;
                todoWorkspace.Visible = todoEnabled;
                Todos.Enabled = filters.Enabled = edit.Enabled = remove.Enabled = false;
                notesPath.Text = todoPath.Text = "SCRIBE WORK RECOVERY: " + error.Message +
                    " Files were not changed. Back up unsaved text, repair the files and explicitly Reload, or disable these features in Settings.";
                Console.Error.WriteLine("FCS-Scribe mod work recovery: " + error);
                RefreshTasks();
            }
        }

        internal void Adopt(ModWork work, PluginSettings settings)
        {
            Work = work; LoadError = null;
            Notes.Text = work.Notes;
            Notes.ReadOnly = !settings.ModNotesEnabled;
            todoEnabled = settings.ModTodoEnabled; todoWorkspace.Visible = todoEnabled;
            Todos.Enabled = filters.Enabled = todoEnabled;
            notesPath.Text = settings.ModNotesEnabled ? work.NotesPath +
                " - saved on Scribe actions/close; not native FCS notes. Mod-folder text may be distributed." :
                "Enable Scribe notes in Settings. Plain writing only; native FCS notes remain separate.";
            todoPath.Text = settings.ModTodoEnabled ? work.TodoPath +
                " - right-click an FCS record: Add Mod To Do. Done marks completion; priority is editable. " +
                "Created dates are task dates; unknown legacy metadata is not inferred." :
                "Enable Mod To Do in Settings to add record-linked tasks. Native Add Todo Item is unchanged.";
            RefreshTasks();
        }

        internal void RefreshTasks()
        {
            string selectedId = SelectedTask() == null ? null : SelectedTask().Id;
            string selectedType = ItemType.SelectedIndex > 0 ? (string)ItemType.SelectedItem : null;
            binding = true;
            try
            {
                ItemType.Items.Clear(); ItemType.Items.Add("All item types");
                if (Work != null) foreach (string type in Work.Todos.Select(ModTodoQuery.TypeLabel).Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)) ItemType.Items.Add(type);
                ItemType.SelectedIndex = selectedType == null ? 0 : ItemType.Items.IndexOf(selectedType);
                if (ItemType.SelectedIndex < 0) ItemType.SelectedIndex = 0;
                Todos.Rows.Clear();
                DateTime? from = UseCreatedFrom.Checked ? (DateTime?)CreatedFrom.Value.Date : null;
                DateTime? to = UseCreatedTo.Checked ? (DateTime?)CreatedTo.Value.Date : null;
                summary.ForeColor = SystemColors.ControlText;
                if (!ModTodoQuery.ValidDates(from, to))
                {
                    summary.Text = "TODO date range is invalid: From must not be after To.";
                    summary.ForeColor = Color.DarkRed;
                }
                else
                {
                    var query = new ModTodoQuery { Search = Search.Text, Field = ((SearchChoice)SearchField.SelectedItem).Field,
                        State = (ModTodoState)State.SelectedIndex, ItemType = ItemType.SelectedIndex == 0 ? null : (string)ItemType.SelectedItem,
                        Priority = Priority.SelectedIndex == 0 ? null : (string)Priority.SelectedItem,
                        CreatedFrom = from, CreatedTo = to, Sort = sort, Descending = descending };
                    List<ModTodo> visible = Work == null ? new List<ModTodo>() : query.Apply(Work.Todos);
                    foreach (ModTodo task in visible)
                    {
                        DateTime? created = task.CreatedDateUtc;
                        int index = Todos.Rows.Add(task.Done, task.RecordName, task.RecordId, ModTodoQuery.TypeLabel(task),
                            ModTodoQuery.PriorityLabel(task), created.HasValue ? (object)created.Value.ToLocalTime() : null, task.Text);
                        Todos.Rows[index].Tag = task;
                        Todos.Rows[index].Cells["Description"].ToolTipText = task.Text;
                    }
                    summary.Text = visible.Count + " of " + (Work == null ? 0 : Work.Todos.Count) +
                        " tasks shown. Search/filter/sort do not save or change tasks." +
                        (from.HasValue || to.HasValue ? " Unknown creation dates are excluded by date filters." : "");
                }
                foreach (DataGridViewColumn column in Todos.Columns)
                    column.HeaderCell.SortGlyphDirection = sort.HasValue && column.Index == (int)sort.Value ?
                        (descending ? SortOrder.Descending : SortOrder.Ascending) : SortOrder.None;
                if (Todos.Rows.Count > 0)
                {
                    DataGridViewRow selected = Todos.Rows.Cast<DataGridViewRow>()
                        .FirstOrDefault(row => ((ModTodo)row.Tag).Id == selectedId) ?? Todos.Rows[0];
                    Todos.CurrentCell = selected.Cells["ItemName"]; selected.Selected = true;
                }
            }
            finally { binding = false; }
            UpdateSelection();
        }

        internal void SortBy(ModTodoColumn column)
        {
            if (!Enum.IsDefined(typeof(ModTodoColumn), column))
                throw new InvalidOperationException("Unknown TODO sort column.");
            descending = sort == column && !descending;
            sort = column; RefreshTasks();
        }

        private ModTodo SelectedTask()
        { return Todos.CurrentRow == null ? null : Todos.CurrentRow.Tag as ModTodo; }

        private void UpdateSelection()
        { edit.Enabled = remove.Enabled = todoEnabled && Work != null && SelectedTask() != null; }

        private void QueueRefresh()
        {
            if (!Todos.IsHandleCreated) { RefreshTasks(); return; }
            if (refreshQueued) return;
            refreshQueued = true;
            Todos.BeginInvoke((Action)delegate {
                refreshQueued = false;
                if (!Todos.IsDisposed) RefreshTasks();
            });
        }

        internal void Save()
        {
            if (LoadError != null) throw new InvalidOperationException("Scribe notes/TODO files need recovery before this action. " +
                "Back up unsaved text and explicitly Reload after repairing the files.", LoadError);
            Work.Notes = Notes.Text;
            Work.Save();
        }
    }

    internal sealed class ModTodoDialog : Form
    {
        private readonly TextBox text = new TextBox { Dock = DockStyle.Fill, Multiline = true,
            ScrollBars = ScrollBars.Vertical, MaxLength = Int32.MaxValue, AcceptsReturn = true };
        internal string Value { get { return text.Text; } }
        internal ModTodoDialog(string initial)
        {
            Text = "Mod To Do - task text"; Width = 580; Height = 300;
            StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; MaximizeBox = false;
            text.Text = initial;
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
            var add = new Button { Text = "Save task", AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            add.Click += delegate {
                if (String.IsNullOrWhiteSpace(text.Text))
                    MessageBox.Show(this, "Enter task text.", "Mod To Do", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else DialogResult = DialogResult.OK;
            };
            buttons.Controls.Add(add); buttons.Controls.Add(cancel);
            Controls.Add(text); Controls.Add(buttons); CancelButton = cancel;
        }
    }

    internal sealed class ModTodoMenu : IDisposable
    {
        private readonly Assembly assembly;
        private readonly Form editor;
        private readonly Func<ScribeWindow> open;
        private readonly ContextMenuStrip menu;
        internal readonly ToolStripMenuItem Item = new ToolStripMenuItem("Add Mod To Do");

        internal ModTodoMenu(Assembly assembly, Form editor, Func<ScribeWindow> open)
        {
            this.assembly = assembly; this.editor = editor; this.open = open;
            menu = FcsAdapter.RecordMenu(editor);
            menu.Items.Insert(menu.Items.IndexOf(FcsAdapter.NativeTodoItem(editor)) + 1, Item);
            Item.Visible = false;
            menu.Opening += Opening;
            Item.Click += Click;
        }

        private void Opening(object sender, System.ComponentModel.CancelEventArgs args)
        {
            Item.Visible = false;
            if (!FcsAdapter.NativeTodoItem(editor).Enabled) return;
            try
            {
                var adapter = new FcsAdapter(assembly, editor);
                ProjectLocation location = ProjectLocation.Open(adapter.ActiveModDirectory(), adapter.ActiveModId());
                LoadedSettings loaded = SettingsStorage.Load(location.SettingsPath);
                if (loaded.Error != null) throw loaded.Error;
                if (loaded.Value.ProjectModId != null && loaded.Value.ProjectModId != adapter.ActiveModId())
                    throw new InvalidOperationException("The selected Scribe settings belong to another active mod.");
                Item.Visible = loaded.Value.ModTodoEnabled;
                Item.Enabled = loaded.Value.ModTodoEnabled && adapter.SelectedModTodos().Count > 0;
            }
            catch (Exception error) { Plugin.ShowError(error); }
        }

        private void Click(object sender, EventArgs args)
        {
            try
            {
                var adapter = new FcsAdapter(assembly, editor);
                List<ModTodo> targets = adapter.SelectedModTodos();
                ScribeWindow window = open();
                window.AddModTodos(targets);
            }
            catch (Exception error) { Plugin.ShowError(error); }
        }

        public void Dispose()
        {
            menu.Opening -= Opening; Item.Click -= Click;
            if (!menu.IsDisposed) menu.Items.Remove(Item);
            Item.Dispose();
        }
    }
}
