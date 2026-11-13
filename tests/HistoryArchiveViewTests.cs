using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FcsScribe;

internal static class HistoryArchiveViewTests
{
    private static int passed;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception("FAILED: " + message); passed++; }
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { passed++; return; }
        throw new Exception("FAILED to reject: " + message);
    }
    private static Snapshot State(string value)
    {
        return new Snapshot { ModId = "history-view", ModName = "view.mod",
            Records = new List<Record> { new Record { Id = "item", Type = "ITEM", Name = "Item",
                Cells = new List<Cell> { new Cell { Layer = "active", Path = "/fields/value",
                    Value = Value.Of("Int32", value), Source = "active" } } } } };
    }
    private static void Pump(Task task)
    {
        DateTime limit = DateTime.UtcNow.AddSeconds(30);
        while (!task.IsCompleted && DateTime.UtcNow < limit) { Application.DoEvents(); Thread.Sleep(5); }
        if (!task.IsCompleted) throw new TimeoutException("Non-showing history action did not complete.");
        task.GetAwaiter().GetResult();
    }

    [STAThread]
    public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FSHV-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "project.json");
            string archiveRoot = Path.Combine(root, "archives");
            string installation = Path.Combine(root, "fake-kenshi");
            Directory.CreateDirectory(archiveRoot); Directory.CreateDirectory(installation);
            string notes = Path.Combine(root, "notes.txt"), draft = Path.Combine(root, "draft-workspace.json");
            File.WriteAllText(notes, "Unsaved notes are not adopted by Manage History.");
            File.WriteAllText(draft, "Opaque unfinished draft bytes");
            using (var project = JournalProject.Create(path, State("10")))
            {
                project.BeginSession(); project.Record(State("11")); project.Compile();
                project.BeginSession(); project.Record(State("12")); project.Compile();
                project.BeginSession(); project.BeginSession(); project.Record(State("13"));
                string active = project.SessionPath, head = project.HeadId, baseline = TrackingStorage.HashFile(path);
                string catalogue = Json.Write(project.ExportRecordings(project.State));
                string notesHash = TrackingStorage.HashFile(notes), draftHash = TrackingStorage.HashFile(draft);
                Packet packet = project.LastEvidence;
                int confirmations = 0;
                bool approve = false;
                bool cancelAtConfirmation = false;
                string prompt = null;
                HistoryManagementDialog management = null;
                using (var view = new HistoryManagementDialog(project, project.HistoryInventory(), installation,
                    text => {
                        confirmations++; prompt = text;
                        if (cancelAtConfirmation) management.RequestCancellation();
                        return approve;
                    }))
                {
                    management = view;
                    IntPtr handle = view.Handle;
                    IntPtr gridHandle = view.Sessions.Handle;
                    view.PerformLayout();
                    Check(!view.Visible && !view.AgeEnabled.Checked && !view.KeepEnabled.Checked &&
                        !view.AgeDays.Enabled && !view.KeepCount.Enabled && view.SelectedSessions().Count == 0,
                        "a non-showing management form starts with guidance off and no selected originals");
                    Check(view.Sessions.Rows.Count == 4 && view.Sessions.Rows.Cast<DataGridViewRow>()
                        .Where(row => !((HistorySessionInfo)row.Tag).Eligible).All(row => row.Cells[0].ReadOnly),
                        "active/pending rows display as retained and cannot be checked interactively");
                    view.ApplyGuidance(DateTime.UtcNow);
                    Check(view.SelectedSessions().Count == 3 && view.SelectedSessions().All(item => item.Eligible),
                        "explicit unfiltered guidance checks only eligible closed originals");
                    view.AgeEnabled.Checked = true;
                    Check(view.AgeDays.Enabled && view.SelectedSessions().Count == 3,
                        "changing a guide control never silently changes the manual selection");
                    view.ApplyGuidance(DateTime.UtcNow);
                    Check(view.SelectedSessions().Count == 0, "age guidance excludes the new synthetic sessions");
                    view.AgeEnabled.Checked = false; view.KeepEnabled.Checked = true; view.KeepCount.Value = 1;
                    view.ApplyGuidance(DateTime.UtcNow);
                    Check(view.SelectedSessions().Count == 2 && view.KeepCount.Enabled && !view.AgeDays.Enabled,
                        "keep-newest guidance retains one eligible original independently of active/pending protection");
                    string[] selected = view.SelectedSessions().Select(item => item.SessionId).OrderBy(id => id).ToArray();
                    view.Sessions.Sort(view.Sessions.Columns[2], ListSortDirection.Ascending);
                    Check(view.SelectedSessions().Select(item => item.SessionId).OrderBy(id => id).SequenceEqual(selected),
                        "sorting retains exact checked session identities");
                    view.SetDestination(archiveRoot);
                    Reject(() => view.SetDestination(installation), "archives within the Kenshi upload tree");
                    Reject(() => view.SetDestination(project.DirectoryName), "archives within reserved project storage");
                    view.KeepEnabled.Checked = false; view.ApplyGuidance(DateTime.UtcNow);
                    Pump(view.PreviewArchive());
                    Check(view.PreviewText.Contains("Same drive") && view.PreviewText.Contains("plus manifest") &&
                        view.PreviewText == view.Outcome && Directory.GetFileSystemEntries(archiveRoot).Length == 0,
                        "completed non-writing space preview remains visible after busy controls are restored");
                    Pump(view.ArchiveSelected());
                    Check(confirmations == 1 && prompt.Contains("Same drive") && prompt.Contains("plus manifest") &&
                        prompt.Contains("No baseline") && view.Outcome.Contains("declined") && !view.HadError,
                        "archive requires an explicit confirmation showing source reduction and same-drive space limits");
                    Check(Directory.GetFileSystemEntries(archiveRoot).Length == 0 && project.HistoryInventory().Sessions.Count == 4 &&
                        TrackingStorage.HashFile(path) == baseline && Json.Write(project.ExportRecordings(project.State)) == catalogue,
                        "declining archive creates no files and does not advance history");
                    cancelAtConfirmation = true; approve = true; Pump(view.ArchiveSelected());
                    Check(confirmations == 2 && view.Outcome.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        Directory.GetFileSystemEntries(archiveRoot).Length == 0 && view.Sessions.Rows.Count == 4 &&
                        ((Button)view.CancelButton).Text == "Close" && !view.HadError,
                        "cancellation before transfer creates no archive, retains evidence and restores the close/escape boundary");
                    cancelAtConfirmation = false; view.ApplyGuidance(DateTime.UtcNow);
                    approve = true; Pump(view.ArchiveSelected());
                    string manifest = Directory.GetDirectories(archiveRoot).Select(folder => Path.Combine(folder, "manifest.json")).Single();
                    Check(confirmations == 3 && view.Outcome.Contains("Archived 3") && view.SelectedSessions().Count == 0 &&
                        view.Sessions.Rows.Count == 1, "confirmed archive refreshes observed originals and clears prior selection");
                    approve = false; Pump(view.RestoreFrom(manifest));
                    Check(confirmations == 4 && view.Outcome.Contains("declined") && view.Sessions.Rows.Count == 1 &&
                        prompt.Contains("Nothing is overwritten") && prompt.Contains("not restore a native mod"),
                        "restore independently confirms exact originals and declining leaves them archived");
                    approve = true; Pump(view.RestoreFrom(manifest));
                    Check(confirmations == 5 && view.Outcome.Contains("Restored 3") && view.Sessions.Rows.Count == 4,
                        "confirmed restore updates the same management window without native dialogs or reopening tracking");
                    Check(project.SessionPath == active && project.HeadId == head && project.PendingCount == 1 &&
                        Object.ReferenceEquals(packet, project.LastEvidence) &&
                        Json.Write(project.ExportRecordings(project.State)) == catalogue &&
                        TrackingStorage.HashFile(notes) == notesHash && TrackingStorage.HashFile(draft) == draftHash,
                        "management preserves active recording identity, original AI packet, pending debt and unrelated editable-storage bytes");
                    foreach (int width in new[] { 1000, 800 })
                    {
                        view.Width = width; view.PerformLayout(); view.Sessions.PerformLayout();
                        Check(view.Sessions.Width > 700 && view.Sessions.Height > 100 &&
                            view.Sessions.Columns.Cast<DataGridViewColumn>().All(column => column.Width >= 45),
                            "non-showing candidate-table layout retains readable rows/columns at width " + width);
                    }
                    Check(!view.Visible && !view.HadError, "no form or native FCS window was shown during synthetic UI checks");
                }
            }
            Console.WriteLine("PASS: " + passed + " non-showing history-management checks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { Directory.Delete(root, true); }
    }
}
