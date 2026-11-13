using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace FcsScribe
{
    public sealed class ModTodo
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string RecordId, RecordName, Text;
        public string RecordType, CreatedUtc, Priority = "Normal";
        public bool Done;
        public override string ToString() { return RecordName + " [" + RecordId + "] - " + Text; }

        public DateTime? CreatedDateUtc
        {
            get
            {
                if (CreatedUtc == null) return null;
                return ParseCreatedUtc(CreatedUtc);
            }
        }

        private static DateTime ParseCreatedUtc(string text)
        {
            DateTime created;
            if (!DateTime.TryParseExact(text, "o", CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out created) || created.Kind != DateTimeKind.Utc)
                throw new InvalidDataException("TODO creation dates must be exact UTC timestamps.");
            return created;
        }

        public static ModTodo Create(ModTodo target, string text, DateTime createdUtc)
        {
            if (target == null || String.IsNullOrWhiteSpace(target.RecordType))
                throw new InvalidDataException("A new TODO needs a captured record type.");
            if (createdUtc.Kind != DateTimeKind.Utc)
                throw new InvalidDataException("A new TODO creation date must be UTC.");
            var task = new ModTodo { RecordId = target.RecordId, RecordName = target.RecordName,
                RecordType = target.RecordType, Text = text, CreatedUtc = createdUtc.ToString("o", CultureInfo.InvariantCulture) };
            task.Validate();
            return task;
        }

        internal void Validate()
        {
            Guid id;
            if (!Guid.TryParseExact(Id, "N", out id) || String.IsNullOrWhiteSpace(RecordId) || String.IsNullOrWhiteSpace(Text))
                throw new InvalidDataException("Each TODO needs a unique ID, a record ID and task text.");
            if (Priority != null && Priority != "Low" && Priority != "Normal" && Priority != "High")
                throw new InvalidDataException("TODO priority must be Low, Normal, High or unspecified.");
            if (RecordType != null && String.IsNullOrWhiteSpace(RecordType))
                throw new InvalidDataException("TODO record type must be captured text or unknown.");
            if (CreatedUtc != null) ParseCreatedUtc(CreatedUtc);
        }
    }

    public sealed class ModWork
    {
        private const string Header = "FCS-Scribe Mod To Do 2", LegacyHeader = "FCS-Scribe Mod To Do 1";
        private readonly string modName;
        private readonly bool notesEnabled, todoEnabled;
        private string notesHash, todoHash, savedNotes, savedTodo;
        public readonly string NotesPath, TodoPath;
        public string Notes;
        public readonly List<ModTodo> Todos = new List<ModTodo>();

        public ModWork(string directory, string modName, bool notesEnabled, bool todoEnabled)
        {
            this.modName = modName; this.notesEnabled = notesEnabled; this.todoEnabled = todoEnabled;
            NotesPath = Path.Combine(directory, "notes.txt"); TodoPath = Path.Combine(directory, "todo.txt");
            if (notesEnabled) Notes = ReadText(NotesPath, out notesHash) ?? "";
            else Notes = "";
            savedNotes = Notes;
            if (todoEnabled)
            {
                string text = ReadText(TodoPath, out todoHash);
                if (text != null) ParseTodos(text);
            }
            savedTodo = TodoText();
        }

        private void ParseTodos(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length < 2 || (lines[0] != Header && lines[0] != LegacyHeader) ||
                lines[1] != "Mod: " + Escape(modName))
                throw new InvalidDataException("todo.txt is not a Scribe TODO file for this mod. Preserve it and choose a separate project folder.");
            bool legacy = lines[0] == LegacyHeader;
            var ids = new HashSet<Guid>();
            foreach (string line in lines.Skip(2).Where(x => x.Length != 0))
            {
                string[] fields = line.Split('\t');
                Guid id;
                if (fields.Length != (legacy ? 5 : 8) || (fields[0] != "[ ]" && fields[0] != "[x]") ||
                    !Guid.TryParseExact(fields[1], "N", out id) || !ids.Add(id))
                    throw new InvalidDataException("Malformed or duplicate task in todo.txt. Preserve the file and repair or reload it explicitly.");
                var task = new ModTodo { Done = fields[0] == "[x]", Id = fields[1],
                    RecordId = Unescape(fields[2]), RecordName = Unescape(fields[3]),
                    Text = Unescape(fields[legacy ? 4 : 7]), Priority = null };
                if (!legacy)
                {
                    task.RecordType = Unescape(fields[4]); task.Priority = Unescape(fields[5]); task.CreatedUtc = Unescape(fields[6]);
                    if (task.RecordType.Length == 0) task.RecordType = null;
                    if (task.Priority.Length == 0) task.Priority = null;
                    if (task.CreatedUtc.Length == 0) task.CreatedUtc = null;
                }
                task.Validate();
                Todos.Add(task);
            }
        }

        private string TodoText()
        {
            var text = new StringBuilder(Header + "\nMod: " + Escape(modName) + "\n");
            var ids = new HashSet<Guid>();
            foreach (ModTodo task in Todos)
            {
                if (task == null) throw new InvalidDataException("A TODO entry is missing.");
                task.Validate();
                Guid id;
                if (!Guid.TryParseExact(task.Id, "N", out id) || !ids.Add(id))
                    throw new InvalidDataException("Each TODO needs a unique ID, a record ID and task text.");
                text.Append(task.Done ? "[x]" : "[ ]").Append('\t').Append(task.Id).Append('\t')
                    .Append(Escape(task.RecordId)).Append('\t').Append(Escape(task.RecordName)).Append('\t')
                    .Append(Escape(task.RecordType)).Append('\t').Append(Escape(task.Priority)).Append('\t')
                    .Append(Escape(task.CreatedUtc)).Append('\t')
                    .Append(Escape(task.Text)).Append('\n');
            }
            return text.ToString();
        }

        public void Save()
        {
            // These are independent commits: a later failure must not undo an earlier confirmed save.
            if (notesEnabled)
            {
                if (notesHash == null || Notes != savedNotes) notesHash = WriteText(NotesPath, Notes, notesHash);
                else Verify(NotesPath, notesHash);
                savedNotes = Notes;
            }
            if (todoEnabled)
            {
                string text = TodoText();
                if (todoHash == null || text != savedTodo) todoHash = WriteText(TodoPath, text, todoHash);
                else Verify(TodoPath, todoHash);
                savedTodo = text;
            }
        }

        internal static string ReadText(string path, out string hash)
        {
            hash = null;
            FileStream stream;
            try { stream = File.OpenRead(path); }
            catch (FileNotFoundException) { return null; }
            using (stream)
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                stream.Position = 0;
                using (var reader = new StreamReader(stream, new UTF8Encoding(false, true), false))
                {
                    string text = reader.ReadToEnd();
                    return text.Length > 0 && text[0] == '\ufeff' ? text.Substring(1) : text;
                }
            }
        }

        internal static void Verify(string path, string expected)
        {
            if (expected == null ? File.Exists(path) : !File.Exists(path) || TrackingStorage.HashFile(path) != expected)
                throw new IOException("Scribe work changed or disappeared outside this window: " + path +
                    ". Unsaved text is retained. Back it up, then explicitly reload before saving.");
        }

        internal static string WriteText(string path, string text, string expected)
        {
            ExportStorage.RequireLocation(path, null);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                Verify(path, expected);
                try
                {
                    byte[] bytes = new UTF8Encoding(false, true).GetBytes(text);
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
                    string hash = TrackingStorage.HashFile(temporary);
                    Verify(path, expected);
                    if (expected == null) File.Move(temporary, path);
                    else File.Replace(temporary, path, path + ".backup-" + Guid.NewGuid().ToString("N"));
                    return hash;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        private static string Escape(string text)
        { return (text ?? "").Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n"); }

        private static string Unescape(string text)
        {
            var result = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '\\') { result.Append(text[i]); continue; }
                if (++i == text.Length) throw new InvalidDataException("Incomplete TODO text escape.");
                switch (text[i])
                {
                    case '\\': result.Append('\\'); break;
                    case 't': result.Append('\t'); break;
                    case 'r': result.Append('\r'); break;
                    case 'n': result.Append('\n'); break;
                    default: throw new InvalidDataException("Unknown TODO text escape.");
                }
            }
            return result.ToString();
        }
    }

    [DataContract]
    public sealed class ProjectLocation
    {
        [DataMember(IsRequired = true)] public string Format = "FCS-Scribe project location 1";
        [DataMember(IsRequired = true)] public string ModId;
        [DataMember(IsRequired = true)] public string Directory;
        internal string LocatorPath, LocatorHash;
        public string SettingsPath { get { return Path.Combine(Directory, "FCS-Scribe.settings.json"); } }

        public static ProjectLocation Open(string modDirectory, string modId)
        {
            string path = Path.Combine(modDirectory, "FCS-Scribe.location.json");
            string hash, text = ModWork.ReadText(path, out hash);
            var location = text == null ? new ProjectLocation { Directory = modDirectory, ModId = modId } :
                Json.Read<ProjectLocation>(text);
            if (location == null || location.Format != "FCS-Scribe project location 1" || location.ModId != modId)
                throw new InvalidDataException("The Scribe project locator is invalid or belongs to another mod path: " + path);
            RequireDirectory(location.Directory);
            location.LocatorPath = path; location.LocatorHash = hash;
            return location;
        }

        public static void RequireDirectory(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory) || Path.GetPathRoot(directory).Length < 3)
                throw new InvalidDataException("Choose an absolute Scribe project directory.");
            if (!System.IO.Directory.Exists(directory))
                throw new DirectoryNotFoundException("The Scribe project directory is missing: " + directory);
            ExportStorage.RequireLocation(Path.Combine(directory, "notes.txt"), null);
        }

        public void Switch(string directory)
        {
            RequireDirectory(directory);
            ExportStorage.RequireLocation(LocatorPath, null);
            ExportStorage.RequireLocation(LocatorPath + ".bak", null);
            var next = new ProjectLocation { Directory = Path.GetFullPath(directory), ModId = ModId };
            using (var lease = new FileStream(LocatorPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                LocatorHash = TrackingStorage.WriteAtomic(LocatorPath, next, LocatorHash);
                Directory = next.Directory;
            }
        }
    }
}
