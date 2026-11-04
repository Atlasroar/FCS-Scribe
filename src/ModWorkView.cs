using System;
using System.Collections.Generic;
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
        internal readonly CheckedListBox Todos = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true,
            HorizontalScrollbar = true, IntegralHeight = false };
        private readonly Label notesPath = new Label { Dock = DockStyle.Top, AutoSize = true };
        private readonly Label todoPath = new Label { Dock = DockStyle.Top, AutoSize = true };
        private readonly Button edit = new Button { Text = "Edit selected task...", AutoSize = true };
        private readonly Button remove = new Button { Text = "Remove selected task", AutoSize = true };
        private bool binding;
        internal ModWork Work { get; private set; }
        internal Exception LoadError { get; private set; }

        internal ModWorkView(Action<Action> action, Action reload)
        {
            NotesTab.Controls.Add(Notes); NotesTab.Controls.Add(notesPath);
            TodoTab.Controls.Add(Todos); TodoTab.Controls.Add(todoPath);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
            var reloadNotes = new Button { Text = "Reload notes / TODOs...", AutoSize = true };
            var reloadTodo = new Button { Text = "Reload notes / TODOs...", AutoSize = true };
            edit.Click += delegate { action(delegate {
                if (Todos.SelectedIndex < 0) throw new InvalidOperationException("Select a task first.");
                var task = (ModTodo)Todos.SelectedItem;
                using (var dialog = new ModTodoDialog(task.Text))
                    if (dialog.ShowDialog(TodoTab.FindForm()) == DialogResult.OK)
                    { task.Text = dialog.Value; RefreshTasks(); }
            }); };
            remove.Click += delegate { action(delegate {
                if (Todos.SelectedIndex < 0) throw new InvalidOperationException("Select a task first.");
                if (MessageBox.Show(TodoTab.FindForm(), "Remove this Scribe task? Native FCS notes are unchanged.",
                    "Remove Mod To Do", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                Work.Todos.Remove((ModTodo)Todos.SelectedItem); RefreshTasks();
            }); };
            reloadNotes.Click += delegate { reload(); }; reloadTodo.Click += delegate { reload(); };
            buttons.Controls.Add(edit); buttons.Controls.Add(remove); buttons.Controls.Add(reloadTodo);
            TodoTab.Controls.Add(buttons);
            var notesButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
            notesButtons.Controls.Add(reloadNotes); NotesTab.Controls.Add(notesButtons);
            Todos.ItemCheck += delegate(object sender, ItemCheckEventArgs e) {
                if (!binding) action(delegate { ((ModTodo)Todos.Items[e.Index]).Done = e.NewValue == CheckState.Checked; });
            };
        }

        internal void Load(string root, string modName, PluginSettings settings)
        {
            try { Adopt(new ModWork(root, modName, settings.ModNotesEnabled, settings.ModTodoEnabled), settings); }
            catch (Exception error)
            {
                LoadError = error; Work = null;
                Notes.ReadOnly = true; Todos.Enabled = edit.Enabled = remove.Enabled = false;
                notesPath.Text = todoPath.Text = "SCRIBE WORK RECOVERY: " + error.Message +
                    " Files were not changed. Back up unsaved text, repair the files and explicitly Reload, or disable these features in Settings.";
                Console.Error.WriteLine("FCS-Scribe mod work recovery: " + error);
            }
        }

        internal void Adopt(ModWork work, PluginSettings settings)
        {
            Work = work; LoadError = null;
            Notes.Text = work.Notes;
            Notes.ReadOnly = !settings.ModNotesEnabled;
            Todos.Enabled = edit.Enabled = remove.Enabled = settings.ModTodoEnabled;
            notesPath.Text = settings.ModNotesEnabled ? work.NotesPath +
                " - saved on Scribe actions/close; not native FCS notes. Mod-folder text may be distributed." :
                "Enable Scribe notes in Settings. Plain writing only; native FCS notes remain separate.";
            todoPath.Text = settings.ModTodoEnabled ? work.TodoPath +
                " - right-click an FCS record: Add Mod To Do. Checked tasks are complete." :
                "Enable Mod To Do in Settings to add record-linked tasks. Native Add Todo Item is unchanged.";
            RefreshTasks();
        }

        internal void RefreshTasks()
        {
            binding = true;
            try
            {
                Todos.Items.Clear();
                if (Work != null) foreach (ModTodo item in Work.Todos) Todos.Items.Add(item, item.Done);
            }
            finally { binding = false; }
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
