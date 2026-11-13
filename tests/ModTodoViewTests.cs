using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FcsScribe;

internal static class ModTodoViewTests
{
    private static int passed;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception("FAILED: " + message); passed++; }

    private static ModTodo Row(ModWorkView view, int index)
    { return (ModTodo)view.Todos.Rows[index].Tag; }

    [STAThread]
    public static int Main()
    {
        string root = Path.Combine(Directory.GetCurrentDirectory(), "build", "todo-view-tests", "fixtures",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        ModWorkView view = null;
        try
        {
            var settings = new PluginSettings { ModNotesEnabled = true, ModTodoEnabled = true };
            var work = new ModWork(root, "example.mod", true, true);
            DateTime created = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
            var first = ModTodo.Create(new ModTodo { RecordId = "string-1", RecordName = "Zulu sword", RecordType = "ITEM" },
                "Review damage\nKeep exact text", created);
            var second = ModTodo.Create(new ModTodo { RecordId = "string-2", RecordName = "alpha research", RecordType = "RESEARCH" },
                "Review cost", created.AddMonths(-1));
            var legacy = new ModTodo { RecordId = "string-3", RecordName = "Legacy task", Text = "Unknown metadata", Priority = null };
            first.Priority = "High"; second.Priority = "Low"; second.Done = true;
            work.Notes = "Saved plain notes"; work.Todos.AddRange(new[] { first, second, legacy }); work.Save();
            int saves = 0;
            view = new ModWorkView(action => { action(); view.Save(); saves++; }, () => { });
            view.Load(root, "example.mod", new PluginSettings { ModNotesEnabled = true });
            var workspace = view.TodoTab.Controls.OfType<TableLayoutPanel>().Single();
            Check(!workspace.Visible && !view.Todos.Enabled && !view.Search.Visible && !view.Priority.Visible &&
                view.TodoTab.Controls.OfType<Label>().Single().Text.Contains("Enable Mod To Do"),
                "disabled TODO hides its table/search/filters and retains tab-local settings information");
            Check(view.Notes.Text == "Saved plain notes" && !view.Notes.ReadOnly,
                "TODO visibility does not change the separate enabled plain-notes tab");
            view.Load(root, "example.mod", settings);
            Check(workspace.Visible && view.Todos.Enabled && view.Search.Visible && view.SearchField.Visible &&
                view.State.Visible && view.ItemType.Visible && view.Priority.Visible && view.CreatedFrom.Visible && view.CreatedTo.Visible,
                "every new task control is inside the enabled TODO workspace");
            Check(view.Todos.Columns.Cast<DataGridViewColumn>().Select(column => column.HeaderText)
                .SequenceEqual(new[] { "Done", "Item name", "String ID", "Item type", "Priority", "Created date", "Description" }),
                "the table retains completion and the six requested column headings");
            Check(!view.Todos.AllowUserToAddRows && !view.Todos.AllowUserToDeleteRows && view.Todos.Rows.Count == 3,
                "task creation/removal keep their guarded actions rather than unbound grid rows");
            Check(!view.Todos.Columns["Done"].ReadOnly && !view.Todos.Columns["Priority"].ReadOnly &&
                view.Todos.Columns.Cast<DataGridViewColumn>().Where(column => column.Name != "Done" && column.Name != "Priority")
                    .All(column => column.ReadOnly), "only completion and priority are directly editable");
            foreach (int width in new[] { 1060, 740 })
            {
                view.TodoTab.Size = new System.Drawing.Size(width, 580);
                view.TodoTab.PerformLayout(); workspace.PerformLayout();
                foreach (FlowLayoutPanel panel in workspace.Controls.OfType<FlowLayoutPanel>()) panel.PerformLayout();
                workspace.PerformLayout();
                var toolbar = workspace.Controls.OfType<FlowLayoutPanel>()
                    .Single(panel => panel.Controls.OfType<Button>().Any(button => button.Text == "Edit selected task..."));
                var filterPanel = workspace.Controls.OfType<FlowLayoutPanel>()
                    .Single(panel => panel.Controls.Contains(view.Search));
                Check(view.Todos.Height > 100 && view.Todos.Top >= filterPanel.Bottom &&
                    view.Todos.Bottom <= toolbar.Top && filterPanel.Width <= workspace.Width,
                    "task filters wrap above the table without overlapping rows/actions at width " + width);
            }
            Check(Row(view, 0).Id == first.Id && view.Todos.Rows[0].Cells["Description"].Value.ToString() == first.Text &&
                view.Todos.Rows[0].Cells["Description"].ToolTipText == first.Text,
                "table projection preserves task identity and full multiline description");
            Check(view.Todos.Rows[2].Cells["CreatedDate"].FormattedValue.ToString() == "Unknown" &&
                view.Todos.Rows[2].Cells["Priority"].Value.ToString() == "Unspecified" &&
                view.Todos.Rows[2].Cells["ItemType"].Value.ToString() == "Unknown",
                "legacy metadata is presented without invented values");
            Check(view.Todos.Handle != IntPtr.Zero && view.CreatedFrom.Handle != IntPtr.Zero &&
                view.CreatedTo.Handle != IntPtr.Zero, "hidden controls create normal handles without an FCS or modal owner");
            string todoHash = TrackingStorage.HashFile(work.TodoPath), noteHash = TrackingStorage.HashFile(work.NotesPath);
            view.Notes.Text = "Unsaved plain notes";
            view.Search.Text = "STRING-2";
            Check(view.Todos.Rows.Count == 1 && Row(view, 0).Id == second.Id, "default search matches record IDs case-insensitively");
            view.SearchField.SelectedIndex = (int)ModTodoField.Description;
            Check(view.Todos.Rows.Count == 0, "field-scoped search does not leak ID matches into descriptions");
            view.Search.Text = "cost";
            Check(view.Todos.Rows.Count == 1 && Row(view, 0).Id == second.Id, "description search uses existing task text");
            view.State.SelectedIndex = (int)ModTodoState.Open;
            Check(view.Todos.Rows.Count == 0, "completion filters combine with searches");
            view.State.SelectedIndex = 0; view.Search.Clear(); view.SearchField.SelectedIndex = 0;
            view.ItemType.SelectedItem = "ITEM"; view.Priority.SelectedItem = "High";
            Check(view.Todos.Rows.Count == 1 && Row(view, 0).Id == first.Id, "type and priority filters combine");
            view.ItemType.SelectedIndex = view.Priority.SelectedIndex = 0;
            view.CreatedFrom.Value = created.ToLocalTime().Date; view.CreatedTo.Value = created.ToLocalTime().Date;
            view.UseCreatedFrom.Checked = view.UseCreatedTo.Checked = true;
            Check(view.Todos.Rows.Count == 1 && Row(view, 0).Id == first.Id,
                "date controls use inclusive local task-creation days");
            view.CreatedTo.Value = view.CreatedFrom.Value.AddDays(-1);
            Check(view.Todos.Rows.Count == 0 && workspace.Controls.OfType<Label>().Single().Text.Contains("date range is invalid"),
                "invalid date range is visible within the TODO tab, not a silent empty-success result");
            view.UseCreatedFrom.Checked = view.UseCreatedTo.Checked = false;
            Check(view.Todos.Rows.Count == 3 && !view.CreatedFrom.Enabled && !view.CreatedTo.Enabled,
                "unchecking both date bounds immediately restores rows and disables their date pickers");
            view.Todos.CurrentCell = view.Todos.Rows[0].Cells["ItemName"];
            view.SortBy(ModTodoColumn.ItemName);
            Check(Row(view, 0).Id == second.Id && Row(view, 2).Id == first.Id &&
                ((ModTodo)view.Todos.CurrentRow.Tag).Id == first.Id,
                "sorting changes projection order while retaining the selected task identity");
            view.SortBy(ModTodoColumn.ItemName);
            Check(Row(view, 0).Id == first.Id && view.Todos.Columns["ItemName"].HeaderCell.SortGlyphDirection == SortOrder.Descending,
                "repeated header sorting reverses order and updates its indicator");
            Check(saves == 0 && TrackingStorage.HashFile(work.TodoPath) == todoHash &&
                TrackingStorage.HashFile(work.NotesPath) == noteHash && view.Notes.Text == "Unsaved plain notes",
                "search/type/priority/date filters and sorting neither save text nor mutate task files");
            view.Search.Text = "string-2";
            view.Todos.CurrentCell = view.Todos.Rows[0].Cells["Priority"];
            Check(view.Todos.BeginEdit(false) && view.Todos.EditingControl is ComboBox,
                "priority cells open their actual dropdown editing control");
            ((ComboBox)view.Todos.EditingControl).SelectedItem = "High";
            Application.DoEvents();
            var saved = new ModWork(root, "example.mod", true, true);
            Check(saved.Todos.Single(task => task.Id == second.Id).Priority == "High" &&
                saved.Todos.Single(task => task.Id == first.Id).Priority == "High" && saves == 1,
                "editing a filtered row saves its actual linked task, not the original list index");
            view.Todos.Rows[0].Cells["Done"].Value = false;
            Application.DoEvents();
            saved = new ModWork(root, "example.mod", true, true);
            Check(!saved.Todos.Single(task => task.Id == second.Id).Done && saved.Todos.Single(task => task.Id == legacy.Id).CreatedUtc == null,
                "completion edits retain metadata and the existing guarded save behavior");
            view.State.SelectedIndex = (int)ModTodoState.Open;
            view.Todos.Rows[0].Cells["Done"].Value = true;
            Application.DoEvents();
            Check(view.Todos.Rows.Count == 0 &&
                new ModWork(root, "example.mod", true, true).Todos.Single(task => task.Id == second.Id).Done,
                "completion changes remove the task from an Open filter only after its edit is committed");
            view.State.SelectedIndex = 0;
            view.Priority.SelectedItem = "High";
            view.Todos.CurrentCell = view.Todos.Rows[0].Cells["Priority"];
            Check(view.Todos.BeginEdit(false) && view.Todos.EditingControl is ComboBox,
                "a filtered priority cell can be edited through its actual dropdown");
            ((ComboBox)view.Todos.EditingControl).SelectedItem = "Low";
            Application.DoEvents();
            Check(view.Todos.Rows.Count == 0 &&
                new ModWork(root, "example.mod", true, true).Todos.Single(task => task.Id == second.Id).Priority == "Low",
                "priority changes safely remove a committed task from a nonmatching filter");
            view.Priority.SelectedIndex = 0;
            view.Todos.Rows[0].Cells["Priority"].Value = "High";
            Application.DoEvents();
            view.Search.Clear(); view.Priority.SelectedItem = "High";
            view.SortBy(ModTodoColumn.Priority);
            view.RefreshTasks();
            Check(view.Priority.SelectedItem.ToString() == "High" && view.Todos.Rows.Count == 2,
                "task refresh preserves active filters");
            view.TodoTab.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<FlowLayoutPanel>()
                .First(panel => panel.Controls.OfType<Button>().Any(button => button.Text == "Clear filters"))
                .Controls.OfType<Button>().Single(button => button.Text == "Clear filters").PerformClick();
            Check(view.Todos.Rows.Count == 3 && view.Priority.SelectedIndex == 0 && view.Search.Text == "" &&
                !view.UseCreatedFrom.Checked && !view.UseCreatedTo.Checked, "Clear filters restores the full catalogue without resetting tasks");
            string valid = File.ReadAllText(work.TodoPath);
            File.WriteAllText(work.TodoPath, "Malformed external task file");
            view.Notes.Text = "Retained unsaved notes"; view.Load(root, "example.mod", settings);
            Check(view.LoadError != null && view.Work == null && !view.Todos.Enabled && !view.Search.Enabled &&
                !view.Priority.Enabled && view.Todos.Rows.Count == 0 && view.Notes.Text == "Retained unsaved notes",
                "recovery disables all task mutation/filter controls and preserves unsaved notes");
            Check(view.TodoTab.Controls.OfType<Label>().Single().Text.Contains("SCRIBE WORK RECOVERY"),
                "load failures remain explicit on the task tab");
            File.WriteAllText(work.TodoPath, valid); view.Load(root, "example.mod", settings);
            Check(view.LoadError == null && view.Todos.Enabled && view.Search.Enabled && view.Priority.Enabled,
                "explicit valid reload restores the enabled task controls");
            todoHash = TrackingStorage.HashFile(work.TodoPath);
            view.Load(root, "example.mod", new PluginSettings { ModNotesEnabled = true });
            Check(!workspace.Visible && !view.Search.Visible && !view.CreatedFrom.Visible &&
                TrackingStorage.HashFile(work.TodoPath) == todoHash, "turning TODO off hides all new controls without changing its file");
            Console.WriteLine("Passed " + passed + " non-showing TODO view checks; no FCS executable or forms were opened.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            if (view != null) { view.NotesTab.Dispose(); view.TodoTab.Dispose(); }
            Directory.Delete(root, true);
        }
    }
}
