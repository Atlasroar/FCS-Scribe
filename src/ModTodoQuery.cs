using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FcsScribe
{
    public enum ModTodoField { All, ItemName, StringId, ItemType, Priority, CreatedDate, Description }
    public enum ModTodoState { All, Open, Completed }
    public enum ModTodoColumn { Done, ItemName, StringId, ItemType, Priority, CreatedDate, Description }

    public sealed class ModTodoQuery
    {
        public string Search = "", ItemType, Priority;
        public ModTodoField Field;
        public ModTodoState State;
        public DateTime? CreatedFrom, CreatedTo;
        public ModTodoColumn? Sort;
        public bool Descending;

        public static string TypeLabel(ModTodo task) { return task.RecordType ?? "Unknown"; }
        public static string PriorityLabel(ModTodo task) { return task.Priority ?? "Unspecified"; }

        public static string CreationLabel(ModTodo task)
        {
            DateTime? date = task.CreatedDateUtc;
            return date.HasValue ? date.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) : "Unknown";
        }

        public static bool ValidDates(DateTime? from, DateTime? to)
        { return !from.HasValue || !to.HasValue || from.Value.Date <= to.Value.Date; }

        public List<ModTodo> Apply(IEnumerable<ModTodo> tasks)
        {
            if (tasks == null) throw new ArgumentNullException("tasks");
            if (!Enum.IsDefined(typeof(ModTodoField), Field) || !Enum.IsDefined(typeof(ModTodoState), State) ||
                (Sort.HasValue && !Enum.IsDefined(typeof(ModTodoColumn), Sort.Value)))
                throw new InvalidOperationException("Unknown TODO search, state or sort field.");
            if (Priority != null && Priority != "Unspecified" && Priority != "Low" && Priority != "Normal" && Priority != "High")
                throw new InvalidOperationException("Unknown TODO priority filter.");
            if (!ValidDates(CreatedFrom, CreatedTo))
                throw new InvalidOperationException("TODO date range is invalid: From must not be after To.");
            var all = tasks.ToList();
            foreach (ModTodo task in all)
            {
                if (task == null) throw new InvalidDataException("A TODO entry is missing.");
                task.Validate();
            }
            string search = (Search ?? "").Trim();
            IEnumerable<ModTodo> result = all.Where(task =>
                (State == ModTodoState.All || task.Done == (State == ModTodoState.Completed)) &&
                (ItemType == null || String.Equals(TypeLabel(task), ItemType, StringComparison.Ordinal)) &&
                (Priority == null || PriorityLabel(task) == Priority) && InDates(task) && Matches(task, search));
            if (Sort.HasValue)
            {
                var comparer = Comparer<ModTodo>.Create((left, right) =>
                    Compare(Descending ? right : left, Descending ? left : right, Sort.Value));
                result = result.OrderBy(task => task, comparer);
            }
            return result.ToList();
        }

        private bool InDates(ModTodo task)
        {
            if (!CreatedFrom.HasValue && !CreatedTo.HasValue) return true;
            DateTime? created = task.CreatedDateUtc;
            if (!created.HasValue) return false;
            DateTime local = created.Value.ToLocalTime().Date;
            return (!CreatedFrom.HasValue || local >= CreatedFrom.Value.Date) &&
                (!CreatedTo.HasValue || local <= CreatedTo.Value.Date);
        }

        private bool Matches(ModTodo task, string search)
        {
            if (search.Length == 0) return true;
            if (Field == ModTodoField.All)
                return Enum.GetValues(typeof(ModTodoField)).Cast<ModTodoField>()
                    .Where(field => field != ModTodoField.All).Any(field => Contains(task, field, search));
            return Contains(task, Field, search);
        }

        private static bool Contains(ModTodo task, ModTodoField field, string search)
        {
            string text;
            switch (field)
            {
                case ModTodoField.ItemName: text = task.RecordName; break;
                case ModTodoField.StringId: text = task.RecordId; break;
                case ModTodoField.ItemType: text = TypeLabel(task); break;
                case ModTodoField.Priority: text = PriorityLabel(task); break;
                case ModTodoField.CreatedDate: text = CreationLabel(task) + " " + task.CreatedUtc; break;
                case ModTodoField.Description: text = task.Text; break;
                default: throw new InvalidOperationException("Unknown TODO search field.");
            }
            return text != null && text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int Compare(ModTodo left, ModTodo right, ModTodoColumn column)
        {
            switch (column)
            {
                case ModTodoColumn.Done: return left.Done.CompareTo(right.Done);
                case ModTodoColumn.ItemName: return StringComparer.OrdinalIgnoreCase.Compare(left.RecordName, right.RecordName);
                case ModTodoColumn.StringId: return StringComparer.OrdinalIgnoreCase.Compare(left.RecordId, right.RecordId);
                case ModTodoColumn.ItemType: return StringComparer.OrdinalIgnoreCase.Compare(TypeLabel(left), TypeLabel(right));
                case ModTodoColumn.Priority: return PriorityRank(left.Priority).CompareTo(PriorityRank(right.Priority));
                case ModTodoColumn.CreatedDate: return Nullable.Compare(left.CreatedDateUtc, right.CreatedDateUtc);
                case ModTodoColumn.Description: return StringComparer.OrdinalIgnoreCase.Compare(left.Text, right.Text);
                default: throw new InvalidOperationException("Unknown TODO sort column.");
            }
        }

        private static int PriorityRank(string priority)
        {
            switch (priority)
            {
                case null: return -1;
                case "Low": return 0;
                case "Normal": return 1;
                case "High": return 2;
                default: throw new InvalidDataException("Unknown TODO priority.");
            }
        }
    }
}
