using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;

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
                Warnings = new List<string> {
                    "Experimental in-memory extraction, not a complete catalogue of all FCS editor state.",
                    "Base and locked field-level source mod provenance is unresolved; load order is reported as editor context.",
                    "Earlier edits made before Start tracking are outside this baseline.",
                    "Uncommitted UI controls may not yet be in the model. Finish editing the field before reviewing changes.",
                    "Names, scalar fields, layered references and instances are captured; external files, layouts, " +
                        "dialogue editor buffers and mod metadata are outside this catalogue."
                }
            };
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
                CaptureItem(item, record, "", snapshot, 0);
                record.Cells = record.Cells.OrderBy(Snapshot.Key, StringComparer.Ordinal).ToList();
                snapshot.Records.Add(record);
            }
            snapshot.Records = snapshot.Records.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
            snapshot.Warnings = snapshot.Warnings.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            snapshot.Validate();
            return snapshot;
        }

        private void CaptureItem(object item, Record record, string prefix, Snapshot snapshot, int depth)
        {
            if (depth > 8) throw new NotSupportedException("Nested instance depth exceeds supported extraction at " + record.Id + prefix);
            foreach (string layer in new[] { "base", "mod", "locked" })
            {
                string name = (string)Field(item, layer + "Name");
                if (name == null) continue;
                string scope = layer == "mod" ? "active" : layer;
                Add(record, prefix + "/name", scope, Value.Of("string", name),
                    scope == "active" ? snapshot.ModName : scope + ":unresolved", null);
            }
            var data = (IDictionary)Field(item, "data");
            var active = (IDictionary)Field(item, "modData");
            var locked = (IDictionary)Field(item, "lockedData");
            CaptureFields(data, record, prefix, "base", snapshot.ModName);
            CaptureFields(active, record, prefix, "active", snapshot.ModName);
            CaptureFields(locked, record, prefix, "locked", snapshot.ModName);
            var keys = data.Keys.Cast<string>().Union(active.Keys.Cast<string>()).Union(locked.Keys.Cast<string>());
            foreach (string key in keys)
            {
                string source = locked.Contains(key) ? "locked:unresolved" :
                    active.Contains(key) ? snapshot.ModName : "base:unresolved";
                Add(record, prefix + "/fields/" + Escape(key), "effective",
                    Scalar(item.GetType().GetProperty("Bah", Flags).GetValue(item, new object[] { key })), source, null);
            }
            CaptureReferences((IDictionary)Field(item, "references"), record, prefix + "/references", snapshot);
            // FCS moves the same reference object between these collections on removal/re-add.
            CaptureReferences((IDictionary)Field(item, "removed"), record, prefix + "/references", snapshot);
            foreach (DictionaryEntry entry in (IDictionary)Field(item, "instances"))
            {
                string path = prefix + "/instances/" + Escape((string)entry.Key);
                int state = Convert.ToInt32(Call(entry.Value, "getState"), CultureInfo.InvariantCulture);
                Add(record, path + "/present", "effective", Value.Of("boolean", (state & 8) == 0 ? "true" : "false"), null, null);
                Add(record, path + "/name", "effective", Value.Of("string", (string)Property(entry.Value, "Name")), null, null);
                CaptureItem(entry.Value, record, path, snapshot, depth + 1);
            }
        }

        private void CaptureFields(IDictionary fields, Record record, string prefix, string layer, string modName)
        {
            foreach (DictionaryEntry field in fields)
                Add(record, prefix + "/fields/" + Escape((string)field.Key), layer, Scalar(field.Value),
                    layer == "active" ? modName : layer + ":unresolved", null);
        }

        private void CaptureReferences(IDictionary lists, Record record, string prefix, Snapshot snapshot)
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
                        name == "active" ? snapshot.ModName : name + ":unresolved", id);
                }
                object effective = Property(reference, "Values");
                Add(record, path, "effective", effective == null ? Value.Of("null", null) : ReferenceValue(effective),
                    Field(reference, "locked") != null ? "locked:unresolved" :
                    Field(reference, "mod") != null ? snapshot.ModName : "base:unresolved", id);
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

        private static void Add(Record record, string path, string layer, Value value, string source, string referenceId)
        {
            record.Cells.Add(new Cell { Path = path, Layer = layer, Value = value, Source = source, ReferencedId = referenceId });
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
            EnsureReady();
            Snapshot current = Capture();
            DraftValidation.Check(packet, draft, current, Notes);
            object todo = Field(form, "todoList");
            string notesFile = (string)Field(todo, "activeFile");
            if (String.IsNullOrEmpty(notesFile))
                throw new InvalidOperationException("FCS has not initialized the active notes file. Open FCS Notes, then try again.");
            Type noteType = assembly.GetType("forgotten_construction_set.ToDo+NotesItem+Type", true);
            int applied = 0;
            try
            {
                foreach (NoteEntry entry in draft.Entries)
                {
                    string text = "\r\n\r\n" + DraftValidation.Marker(packet.BatchId) + "\r\n" +
                        entry.Text.Trim() + "\r\nChanges: " + String.Join(", ", entry.ChangeIds) + "\r\n";
                    Call(todo, "AddItem", Item(entry.TargetRecordId), text, Enum.Parse(noteType, "NOTE"), null, true, false);
                    applied++;
                    // AddItem marks new notes dirty, but not its existing-note append branch.
                    nav.GetType().GetProperty("HasChanges", Flags).SetValue(nav, true, null);
                }
                Call(todo, "UpdateList");
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("Notes application stopped after " + applied +
                    " completed record(s). FCS may contain partial notes; inspect them before retrying or saving. " +
                    "Nothing was saved by FCS-Scribe.", error);
            }
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
