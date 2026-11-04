using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace FcsScribe
{
    // All version-specific reflection stays here; the core never loads the FCS assembly.
    public sealed class FcsAdapter
    {
        public const string SupportedHash = "3688c4d8421d51c2d30c2ff0338874779d91eff150c6a02bf415bc1cdd9c7796";
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;
        private readonly Assembly assembly;
        private readonly Form form;
        private readonly object nav;
        private readonly object gameData;
        private readonly IDictionary items;
        private readonly object removedReference;

        public static void CheckVersion(Assembly assembly)
        {
            string hash;
            using (var stream = File.OpenRead(assembly.Location))
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (hash != SupportedHash)
                throw new NotSupportedException("Unsupported FCS executable (SHA-256 " + hash +
                    "). FCS-Scribe currently supports the inspected Steam FCS 2.14.2 build only. " +
                    "Do not bypass this check: inspect and validate the adapter for this build first.");
        }

        public FcsAdapter(Assembly assembly, Form form)
        {
            CheckVersion(assembly);
            this.assembly = assembly;
            this.form = form;
            nav = Field(form, "nav");
            gameData = Field(Field(nav, "ou"), "gameData");
            items = (IDictionary)Field(gameData, "items");
            removedReference = assembly.GetType("forgotten_construction_set.GameData+Reference", true)
                .GetField("Removed", Flags).GetValue(null);
            EnsureReady();
        }

        public void EnsureReady()
        {
            if (form.InvokeRequired)
                throw new InvalidOperationException("FCS access must run on its UI thread.");
            if (form.IsDisposed || (bool)Property(nav, "ReadOnly") ||
                String.IsNullOrEmpty((string)Property(nav, "ActiveFile")))
                throw new InvalidOperationException("Open a writable active mod before using FCS-Scribe.");
            if (!String.Equals(Path.GetExtension((string)Property(nav, "ActiveFile")), ".mod", StringComparison.OrdinalIgnoreCase) ||
                (bool)Field(form, "mergeModsMode"))
                throw new NotSupportedException("Only normal .mod editing is supported, not translation or merge mode.");
            if (!Object.ReferenceEquals(gameData, Field(Field(nav, "ou"), "gameData")))
                throw new InvalidOperationException("The editor data session changed. Reopen FCS-Scribe and start tracking again.");
            if (Property(nav, "FileMode").ToString() != "USER")
                throw new NotSupportedException("Only normal mod editing is supported; merge, translation and other file modes are not.");
        }

        public Snapshot Capture()
        {
            EnsureReady();
            string activeFile = (string)Property(nav, "ActiveFile");
            var snapshot = new Snapshot {
                ModId = Json.Hash(Path.GetFullPath(activeFile).ToUpperInvariant()),
                ModName = Path.GetFileName(activeFile),
                LoadOrder = Strings(Field(form, "activeMods")),
                Dependencies = Strings(Field(Field(gameData, "header"), "Dependencies")),
                LockedMods = Strings(Field(gameData, "lockedMods")),
                Workshop = CaptureWorkshop(),
                Warnings = new List<string> {
                    "Experimental in-memory extraction, not a complete catalogue of all FCS editor state.",
                    "Base and locked field-level source mod provenance is unresolved; load order is reported as editor context.",
                    "Earlier edits made before Start tracking are outside this baseline.",
                    "Uncommitted UI controls may not yet be in the model. Finish editing the field before reviewing changes.",
                    "Names, scalar fields, layered references and instances are captured; external files, layouts, " +
                        "dialogue editor buffers and metadata change history are outside this catalogue. " +
                        "Workshop metadata is current context, not a list of new edits."
                }
            };
            if (snapshot.Workshop.Issues != null)
                snapshot.Warnings.AddRange(snapshot.Workshop.Issues.Select(x =>
                    "Workshop " + x.Component + " unavailable [" + x.Code + "]: " + x.Message +
                    " Gameplay capture continues; unavailable values are not validated."));
            var strings = new SnapshotStrings();
            foreach (DictionaryEntry pair in items)
            {
                object item = pair.Value;
                int state = Convert.ToInt32(Call(item, "getState"), CultureInfo.InvariantCulture);
                if ((state & 8) != 0) continue;
                var record = new Record {
                    Id = (string)Property(item, "stringID"), Type = Property(item, "type").ToString(),
                    Name = (string)Property(item, "Name")
                };
                if ((state & (16 | 32)) != 0)
                    snapshot.Warnings.Add("Missing/invalid record state: " + record.Id + ".");
                CaptureItem(item, record, "", snapshot, 0, strings);
                record.Cells = record.Cells.OrderBy(Snapshot.Key, StringComparer.Ordinal).ToList();
                snapshot.Records.Add(record);
            }
            snapshot.Records = snapshot.Records.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            snapshot.Warnings = snapshot.Warnings.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            snapshot.Validate();
            return snapshot;
        }

        private object WorkshopLocation()
        {
            return Activator.CreateInstance(assembly.GetType("forgotten_construction_set.ModData", true),
                new[] { Field(gameData, "header"), Property(nav, "ModPath"),
                    Path.GetFileNameWithoutExtension((string)Property(nav, "ActiveFile")) });
        }

        public WorkshopMetadata CaptureWorkshop()
        {
            EnsureReady();
            object header = Field(gameData, "header");
            object dialog = Field(form, "modInfo");
            object live = Field(dialog, "info");
            object location = WorkshopLocation();
            string file = (string)Call(location, "GetFileName");
            bool matching = MatchingWorkshop(live, header, file);
            WorkshopIssue busy = BusyWorkshop(dialog, matching);
            var result = new WorkshopMetadata {
                Author = (string)Field(header, "Author"),
                Version = (int)Field(header, "Version"),
                Description = (string)Field(header, "Description"),
                Dependencies = Strings(Call(gameData, "getRequiredMods")),
                ReferencedMods = Strings(Call(gameData, "getReferencedMods"))
            };
            result.Warnings.Add("Workshop values are local/staged or cached, not a fresh Steam query. No Save, Refresh or Upload was invoked.");
            result.Warnings.Add("Dependencies are FCS's current required mods; ReferencedMods are the additional parenthesized entries shown by FCS.");
            string imagePath = (string)Call(location, "GetAbsoluteImagePath");
            if (busy != null)
            {
                ReportWorkshopIssue(result, busy.Component, busy.Code, busy.Message, null);
                result.Source = "Active mod header; busy Workshop dialog values and preview unavailable";
                result.Preview = new PreviewImage { Status = "unavailable", SourceFileName = Path.GetFileName(imagePath),
                    UnavailableReason = busy.Message };
                return result;
            }
            WorkshopPreview preview = OptionalWorkshop(result, "preview", () => WorkshopPreview.Read(imagePath));
            result.Preview = preview == null ? new PreviewImage { Status = "unavailable",
                SourceFileName = Path.GetFileName(imagePath), UnavailableReason = result.Issues.Last().Message } : preview.Info;
            if (matching)
            {
                result.Source = "Active mod header + current FCS Workshop dialog";
                result.ModName = (string)Field(live, "title");
                result.Tags = Strings(Field(live, "tags"));
                result.ChangeNotes = ((TextBox)Field(dialog, "txtChangeNotes")).Text;
                result.SteamId = ((ulong)Field(live, "id")).ToString(CultureInfo.InvariantCulture);
                result.VisibilityCode = (int)Field(live, "visibility");
                result.LastUpdate = FormatUpdate(Field(live, "lastUpdate"));
                result.Warnings.Add("ChangeNotes is the dialog's current upload draft, not published Workshop changelog history.");
            }
            else
            {
                result.Source = "Active mod header; Workshop cache unavailable";
                WorkshopFile saved = OptionalWorkshop(result, "cache",
                    () => ReadWorkshopCache(file, (string)Field(location, "mod"), result));
                if (saved != null)
                {
                    result.Source = "Active mod header + saved Workshop .info cache";
                    result.ModName = saved.title;
                    result.Tags = saved.tags == null ? null : saved.tags.ToList();
                    result.SteamId = saved.id.HasValue ? saved.id.Value.ToString(CultureInfo.InvariantCulture) : null;
                    result.VisibilityCode = saved.visibility;
                    result.LastUpdate = FormatUpdate(saved.lastUpdate);
                }
                result.Warnings.Add("Open the active mod's Steam Workshop window to capture staged title/tags/visibility and its change-notes draft.");
                result.Warnings.Add("ChangeNotes is unavailable without a matching Workshop dialog; FCS does not reload it from the .info cache.");
            }
            if (result.VisibilityCode.HasValue)
            {
                var choices = ((ComboBox)Field(dialog, "cbVisiblity")).Items;
                int code = result.VisibilityCode.Value;
                if (code >= 0 && code < choices.Count) result.Visibility = choices[code].ToString();
                else result.Warnings.Add("Unrecognized Workshop visibility code: " + code.ToString(CultureInfo.InvariantCulture) + ".");
            }
            if (result.SteamId == null || result.SteamId == "0")
                result.Warnings.Add("No assigned Steam Workshop ID is known; zero is FCS's unpublished/default ID.");
            if (result.LastUpdate == null)
                result.Warnings.Add("No last-update timestamp is known. Null does not mean the mod has never been updated.");
            else result.Warnings.Add("LastUpdate preserves FCS's cached round-trip date/time and timezone kind; it is not the local file modification time.");
            if (result.Preview.Status == "missing")
                result.Warnings.Add("No local Workshop preview image exists; no image was downloaded from Steam.");
            return result;
        }

        private static bool MatchingWorkshop(object live, object header, string file)
        {
            return live != null && Object.ReferenceEquals(Field(live, "header"), header) &&
                String.Equals(Path.GetFullPath((string)Call(live, "GetFileName")), Path.GetFullPath(file),
                    StringComparison.OrdinalIgnoreCase);
        }

        private WorkshopIssue BusyWorkshop(object dialog, bool matching)
        {
            if (!matching) return null;
            if ((bool)Field(dialog, "uploading"))
                return new WorkshopIssue { Component = "dialog", Code = "upload-in-progress",
                    Message = "Workshop upload is in progress; staged Workshop values and preview were not captured." };
            object steam = assembly.GetType("forgotten_construction_set.SteamManager", true)
                .GetField("instance", Flags).GetValue(null);
            if (steam != null && (bool)Property(steam, "Enabled") &&
                !((Button)Field(dialog, "btnRefreshFromSteam")).Enabled &&
                !((Button)Field(dialog, "btnUpload")).Enabled)
                return new WorkshopIssue { Component = "dialog", Code = "refresh-in-progress",
                    Message = "Workshop refresh is in progress; staged Workshop values and preview were not captured." };
            return null;
        }

        private static WorkshopFile ReadWorkshopCache(string file, string mod, WorkshopMetadata result)
        {
            try
            {
                using (var input = File.OpenRead(file))
                using (var reader = XmlReader.Create(input, new XmlReaderSettings {
                    DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024
                }))
                {
                    reader.MoveToContent();
                    if (reader.LocalName != "ModData" || reader.NamespaceURI != "")
                        throw new InvalidDataException("The Workshop .info cache has an unsupported root element.");
                    var saved = (WorkshopFile)new XmlSerializer(typeof(WorkshopFile)).Deserialize(reader);
                    if (saved == null || (saved.mod != null && saved.mod != mod))
                        throw new InvalidDataException("The Workshop .info cache is empty or identifies a different mod.");
                    return saved;
                }
            }
            catch (FileNotFoundException) { result.Warnings.Add("No saved Workshop .info cache exists; Workshop-only fields are unknown."); }
            catch (DirectoryNotFoundException) { result.Warnings.Add("No saved Workshop cache directory exists; Workshop-only fields are unknown."); }
            catch (XmlException error) { throw new InvalidDataException("The Workshop .info cache XML is invalid or prohibited.", error); }
            catch (InvalidOperationException error)
            {
                if (!(error.InnerException is XmlException) && !(error.InnerException is FormatException) &&
                    !(error.InnerException is OverflowException)) throw;
                throw new InvalidDataException("The Workshop .info cache XML or field values are invalid.", error);
            }
            return null;
        }

        private static T OptionalWorkshop<T>(WorkshopMetadata result, string component, Func<T> capture) where T : class
        {
            try { return capture(); }
            catch (InvalidDataException error) { ReportWorkshopIssue(result, component, "invalid-data", error.Message, error); }
            catch (NotSupportedException error) { ReportWorkshopIssue(result, component, "unsupported", error.Message, error); }
            catch (UnauthorizedAccessException error) { ReportWorkshopIssue(result, component, "access-denied",
                "Access to the Workshop " + component + " was denied. Check permissions and retry.", error); }
            catch (IOException error) { ReportWorkshopIssue(result, component, "read-failed",
                "The Workshop " + component + " could not be read; it may be locked or changing. Check the FCS-Scribe log and retry.", error); }
            return null;
        }

        private static void ReportWorkshopIssue(WorkshopMetadata result, string component, string code, string message, Exception error)
        {
            if (result.Issues == null) result.Issues = new List<WorkshopIssue>();
            result.Issues.Add(new WorkshopIssue { Component = component, Code = code, Message = message });
            result.CaptureStatus = "partial";
            result.Warnings.Add("Workshop " + component + " unavailable [" + code + "]: " + message);
            if (error != null) Console.Error.WriteLine("FCS-Scribe Workshop " + component + " [" + code + "]: " + error);
        }

        private static string FormatUpdate(object value)
        {
            return value == null ? null : ((DateTime)value).ToString("o", CultureInfo.InvariantCulture);
        }

        public WorkshopExport CaptureWorkshopExport()
        {
            EnsureReady();
            return new WorkshopExport {
                ModFileName = Path.GetFileName((string)Property(nav, "ActiveFile")),
                ExportedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                Workshop = CaptureWorkshop()
            };
        }

        public string ActiveModDirectory()
        {
            EnsureReady();
            return Path.GetDirectoryName(Path.GetFullPath((string)Property(nav, "ActiveFile")));
        }

        internal string ActiveModId()
        {
            EnsureReady();
            return Json.Hash(Path.GetFullPath((string)Property(nav, "ActiveFile")).ToUpperInvariant());
        }

        internal string ActiveModName()
        {
            EnsureReady();
            return Path.GetFileName((string)Property(nav, "ActiveFile"));
        }

        internal static ContextMenuStrip RecordMenu(Form editor) { return (ContextMenuStrip)Field(Field(editor, "nav"), "contextMenuStrip1"); }
        internal static ToolStripMenuItem NativeTodoItem(Form editor) { return (ToolStripMenuItem)Field(Field(editor, "nav"), "addTodo"); }
        internal List<ModTodo> SelectedModTodos()
        {
            EnsureReady();
            object list = Field(nav, "listView1");
            object selected = list.GetType().GetProperty("SelectedItems", Flags | BindingFlags.DeclaredOnly).GetValue(list, null);
            return ((IEnumerable)selected).Cast<object>()
                .Select(x => new ModTodo { RecordId = (string)Property(x, "stringID"),
                    RecordName = (string)Property(x, "Name"), Text = "Review this record." }).ToList();
        }

        public PreviewImage ArchivePreview(string destination, string expectedHash)
        {
            EnsureReady();
            object location = WorkshopLocation(), dialog = Field(form, "modInfo");
            WorkshopIssue busy = BusyWorkshop(dialog, MatchingWorkshop(Field(dialog, "info"),
                Field(gameData, "header"), (string)Call(location, "GetFileName")));
            if (busy != null) throw new InvalidOperationException(busy.Message + " Wait before archiving.");
            WorkshopPreview preview = WorkshopPreview.Read((string)Call(location, "GetAbsoluteImagePath"));
            if (preview.Info.Sha256 != expectedHash)
                throw new InvalidOperationException("The preview changed while choosing an archive location. Review it and try again.");
            preview.Save(destination, Path.GetDirectoryName(assembly.Location));
            return preview.Info;
        }

        private void CaptureItem(object item, Record record, string prefix, Snapshot snapshot, int depth, SnapshotStrings strings)
        {
            if (depth > 8) throw new NotSupportedException("Nested instance depth exceeds supported extraction at " + record.Id + prefix);
            foreach (string layer in new[] { "base", "mod", "locked" })
            {
                string name = (string)Field(item, layer + "Name");
                if (name == null) continue;
                string scope = layer == "mod" ? "active" : layer;
                Add(record, prefix + "/name", scope, Value.Of("string", name),
                    scope == "active" ? snapshot.ModName : scope + ":unresolved", null, strings);
            }
            var data = (IDictionary)Field(item, "data");
            var active = (IDictionary)Field(item, "modData");
            var locked = (IDictionary)Field(item, "lockedData");
            CaptureFields(data, record, prefix, "base", snapshot.ModName, strings);
            CaptureFields(active, record, prefix, "active", snapshot.ModName, strings);
            CaptureFields(locked, record, prefix, "locked", snapshot.ModName, strings);
            var keys = data.Keys.Cast<string>().Union(active.Keys.Cast<string>()).Union(locked.Keys.Cast<string>());
            foreach (string key in keys)
            {
                string source = locked.Contains(key) ? "locked:unresolved" :
                    active.Contains(key) ? snapshot.ModName : "base:unresolved";
                Add(record, prefix + "/fields/" + Escape(key), "effective",
                    Scalar(item.GetType().GetProperty("Bah", Flags).GetValue(item, new object[] { key })), source, null, strings);
            }
            CaptureReferences((IDictionary)Field(item, "references"), record, prefix + "/references", snapshot, strings);
            // FCS moves the same reference object between these collections on removal/re-add.
            CaptureReferences((IDictionary)Field(item, "removed"), record, prefix + "/references", snapshot, strings);
            foreach (DictionaryEntry entry in (IDictionary)Field(item, "instances"))
            {
                string path = prefix + "/instances/" + Escape((string)entry.Key);
                int state = Convert.ToInt32(Call(entry.Value, "getState"), CultureInfo.InvariantCulture);
                Add(record, path + "/present", "effective", Value.Of("boolean", (state & 8) == 0 ? "true" : "false"), null, null, strings);
                Add(record, path + "/name", "effective", Value.Of("string", (string)Property(entry.Value, "Name")), null, null, strings);
                CaptureItem(entry.Value, record, path, snapshot, depth + 1, strings);
            }
        }

        private void CaptureFields(IDictionary fields, Record record, string prefix, string layer, string modName, SnapshotStrings strings)
        {
            foreach (DictionaryEntry field in fields)
                Add(record, prefix + "/fields/" + Escape((string)field.Key), layer, Scalar(field.Value),
                    layer == "active" ? modName : layer + ":unresolved", null, strings);
        }

        private void CaptureReferences(IDictionary lists, Record record, string prefix, Snapshot snapshot, SnapshotStrings strings)
        {
            foreach (DictionaryEntry list in lists)
            foreach (object reference in (IEnumerable)list.Value)
            {
                string id = (string)Property(reference, "itemID");
                string path = prefix + "/" + Escape((string)list.Key) + "/" + Escape(id);
                foreach (string layer in new[] { "original", "mod", "locked" })
                {
                    object value = Field(reference, layer);
                    if (value == null) continue;
                    string name = layer == "original" ? "base" : layer == "mod" ? "active" : "locked";
                    Add(record, path, name, ReferenceValue(value),
                        name == "active" ? snapshot.ModName : name + ":unresolved", id, strings);
                }
                object effective = Property(reference, "Values");
                Add(record, path, "effective", effective == null ? Value.Of("null", null) : ReferenceValue(effective),
                    Field(reference, "locked") != null ? "locked:unresolved" :
                    Field(reference, "mod") != null ? snapshot.ModName : "base:unresolved", id, strings);
                if (!items.Contains(id) ||
                    (Convert.ToInt32(Call(items[id], "getState"), CultureInfo.InvariantCulture) & 8) != 0)
                    snapshot.Warnings.Add("Unresolved reference " + id + " from " + record.Id + path + ".");
            }
        }

        private Value ReferenceValue(object value)
        {
            return Object.ReferenceEquals(value, removedReference) ?
                Value.Of("removed-reference", null) : Scalar(value);
        }

        private static void Add(Record record, string path, string layer, Value value, string source, string referenceId,
            SnapshotStrings strings)
        {
            var cell = new Cell { Path = path, Layer = layer, Value = value, Source = source, ReferencedId = referenceId };
            strings.Share(cell);
            record.Cells.Add(cell);
        }

        private static Value Scalar(object value)
        {
            if (value == null) return Value.Of("null", null);
            Type type = value.GetType();
            if (type == typeof(string)) return Value.Of("string", (string)value);
            if (type == typeof(bool)) return Value.Of("boolean", (bool)value ? "true" : "false");
            if (type == typeof(float)) return Value.Of("float32", ((float)value).ToString("R", CultureInfo.InvariantCulture));
            if (type == typeof(double)) return Value.Of("float64", ((double)value).ToString("R", CultureInfo.InvariantCulture));
            if (type.IsEnum) return Value.Of("enum:" + type.FullName, Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
            if (type.IsPrimitive || type == typeof(decimal))
                return Value.Of(type.Name, Convert.ToString(value, CultureInfo.InvariantCulture));
            string name = type.FullName;
            if (name == "forgotten_construction_set.GameData+File")
                return Value.Of("file", (string)Field(value, "filename"));
            string[] fields;
            switch (name)
            {
                case "forgotten_construction_set.GameData+vec": fields = new[] { "x", "y", "z" }; break;
                case "forgotten_construction_set.GameData+quat": fields = new[] { "w", "x", "y", "z" }; break;
                case "forgotten_construction_set.GameData+TripleInt": fields = new[] { "v0", "v1", "v2" }; break;
                case "forgotten_construction_set.GameData+Colour": fields = new[] { "value" }; break;
                default: throw new NotSupportedException("Unsupported FCS value type " + name +
                    ". Extraction stopped rather than silently omitting data.");
            }
            return Value.Of(name, Json.Write(fields.Select(x => Scalar(Field(value, x))).ToArray()));
        }

        public string Notes(string id)
        {
            EnsureReady();
            return (string)Call(Field(form, "todoList"), "GetNotes", Item(id));
        }

        public void Apply(Packet packet, NoteDraft draft)
        {
            Apply(packet, draft, null);
        }

        internal string NotesFile()
        {
            EnsureReady();
            string path = (string)Field(Field(form, "todoList"), "activeFile");
            if (String.IsNullOrEmpty(path))
                throw new InvalidOperationException("FCS has not initialized the active notes file. Open FCS Notes, then try again.");
            return path;
        }

        internal NoteApplyReport Apply(Packet packet, NoteDraft draft, Action<string> completedRecord)
        {
            EnsureReady();
            Snapshot current = Capture();
            List<NoteEntry> pending = DraftValidation.Pending(packet, draft, current, Notes);
            object todo = Field(form, "todoList");
            string notesFile = NotesFile();
            Type noteType = assembly.GetType("forgotten_construction_set.ToDo+NotesItem+Type", true);
            var pendingIds = pending.Select(x => x.TargetRecordId).ToList();
            var skipped = draft.Entries.Select(x => x.TargetRecordId).Except(pendingIds, StringComparer.Ordinal).ToList();
            var completed = new List<string>();
            string lastRecord = null, stage = "preparing notes";
            try
            {
                foreach (NoteEntry entry in pending)
                {
                    lastRecord = entry.TargetRecordId; stage = "marking editor dirty";
                    // A native append may fail after mutation; keep even unconfirmed attempts save-visible.
                    nav.GetType().GetProperty("HasChanges", Flags).SetValue(nav, true, null);
                    stage = "appending note";
                    string text = DraftValidation.NoteText(packet.BatchId, entry);
                    Call(todo, "AddItem", Item(entry.TargetRecordId), text, Enum.Parse(noteType, "NOTE"), null, true, false);
                    stage = "verifying note";
                    if (!DraftValidation.AlreadyApplied(packet.BatchId, entry, Notes(entry.TargetRecordId)))
                        throw new InvalidOperationException("FCS did not return a matching completed note block.");
                    completed.Add(entry.TargetRecordId);
                    stage = "reporting completion";
                    if (completedRecord != null) completedRecord(entry.TargetRecordId);
                }
                lastRecord = null; stage = "refreshing Notes list";
                Call(todo, "UpdateList");
            }
            catch (Exception error)
            {
                throw new NoteApplyException(new NoteApplyReport(pendingIds, completed, skipped, notesFile),
                    lastRecord, stage, error);
            }
            return new NoteApplyReport(pendingIds, completed, skipped, notesFile);
        }

        private object Item(string id)
        {
            object item = items[id];
            if (item == null) throw new InvalidOperationException("Record no longer exists: " + id);
            return item;
        }

        internal static object Field(object target, string name)
        {
            if (target == null) throw new InvalidOperationException("FCS object is not initialized while reading " + name + ".");
            FieldInfo field = null;
            for (Type type = target.GetType(); type != null && field == null; type = type.BaseType)
                field = type.GetField(name, Flags | BindingFlags.DeclaredOnly);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            return field.GetValue(target);
        }

        internal static object Property(object target, string name)
        {
            if (target == null) throw new InvalidOperationException("FCS object is not initialized while reading " + name + ".");
            PropertyInfo property = target.GetType().GetProperty(name, Flags);
            if (property == null) throw new MissingMemberException(target.GetType().FullName, name);
            return property.GetValue(target, null);
        }

        internal static object Call(object target, string name, params object[] args)
        {
            if (target == null) throw new InvalidOperationException("FCS object is not initialized while calling " + name + ".");
            MethodInfo[] methods = target.GetType().GetMethods(Flags).Where(x => x.Name == name &&
                x.GetParameters().Length == args.Length && x.GetParameters().Select((p, i) =>
                    args[i] == null ? !p.ParameterType.IsValueType : p.ParameterType.IsInstanceOfType(args[i])).All(matches => matches)).ToArray();
            if (methods.Length != 1) throw new MissingMethodException("Expected one compatible FCS method: " + name);
            return methods[0].Invoke(target, args);
        }

        private static List<string> Strings(object sequence) { return ((IEnumerable)sequence).Cast<string>().ToList(); }
        private static string Escape(string value) { return value.Replace("~", "~0").Replace("/", "~1"); }
    }
}
