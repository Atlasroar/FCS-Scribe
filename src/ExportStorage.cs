using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace FcsScribe
{
    public static class ExportStorage
    {
        private const string MarkdownPrefix = "<!-- FCS-Scribe Markdown export v1: ";
        private const string MarkdownSuffix = " -->\n";
        private const string ProtectedMessage =
            "Exports and draft workspaces cannot replace tracking anchors, backups or reserved .json.scribe storage. " +
            "Choose a separate export filename outside the project's storage folder.";

        public static void RequireLocation(string path, string activeAnchor)
        {
            path = Path.GetFullPath(path);
            if (path.Substring(Path.GetPathRoot(path).Length).Contains(":"))
                throw new InvalidOperationException("Choose a standard file path, not an alternate data stream.");
            for (string parent = path; parent != null; parent = Path.GetDirectoryName(parent))
            {
                string name = Path.GetFileName(parent);
                if (name != name.TrimEnd(' ', '.'))
                    throw new InvalidOperationException("Choose a file path without trailing dots or spaces in its components.");
                if (parent.EndsWith(".json.scribe", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(ProtectedMessage);
            }

            string anchor = path;
            if (anchor.EndsWith(".legacy.bak", StringComparison.OrdinalIgnoreCase))
                anchor = anchor.Substring(0, anchor.Length - ".legacy.bak".Length);
            else if (anchor.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                anchor = anchor.Substring(0, anchor.Length - ".bak".Length);
            if ((activeAnchor != null && String.Equals(anchor, Path.GetFullPath(activeAnchor), StringComparison.OrdinalIgnoreCase)) ||
                Directory.Exists(anchor + ".scribe") || File.Exists(anchor + ".scribe"))
                throw new InvalidOperationException(ProtectedMessage);
        }

        public static string ObserveTarget(string path, string activeAnchor)
        {
            RequireLocation(path, activeAnchor);
            FileStream stream;
            try { stream = File.OpenRead(path); }
            catch (FileNotFoundException) { return null; }
            using (stream)
            using (var sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                Encoding encoding = RequireSingleObject(stream);
                stream.Position = 0;
                byte[] preamble = encoding.GetPreamble();
                foreach (byte value in preamble)
                    if (stream.ReadByte() != value) { stream.Position = 0; break; }
                try
                {
                    using (var reader = JsonReaderWriterFactory.CreateJsonReader(stream, encoding, XmlDictionaryReaderQuotas.Max, null))
                    {
                        if (reader.MoveToContent() != XmlNodeType.Element || reader.GetAttribute("type") != "object")
                            throw new InvalidDataException("The existing destination is not a JSON object. Choose a new export filename.");
                        while (!reader.EOF)
                        {
                            if (reader.NodeType == XmlNodeType.Element && reader.Depth == 1 &&
                                (reader.LocalName == "Format" || (reader.NamespaceURI == "item" && reader.GetAttribute("item") == "Format")))
                            {
                                if (reader.GetAttribute("type") != "string") throw Unclassifiable();
                                string format = reader.ReadElementContentAsString();
                                if (format == TrackingDocument.FormatName || format == SessionHeader.FormatName ||
                                    format == JournalLine.FormatName || format == "FCS-Scribe session storage")
                                    throw new InvalidOperationException(ProtectedMessage);
                            }
                            else reader.Read();
                        }
                    }
                }
                catch (XmlException error)
                {
                    throw new InvalidDataException("The existing destination cannot be safely classified as JSON. " +
                        "It may be damaged tracking data; choose a new export filename. " + error.Message, error);
                }
                return hash;
            }
        }

        private static Encoding RequireSingleObject(Stream stream)
        {
            // The framework JSON reader can silently ignore another value after the first object.
            stream.Position = 0;
            bool started = false, complete = false, quoted = false, escaped = false;
            int depth = 0;
            Encoding encoding;
            try
            {
                using (var reader = new StreamReader(stream, new UTF8Encoding(false, true), true, 4096, true))
                {
                    int next;
                    while ((next = reader.Read()) != -1)
                    {
                        char c = (char)next;
                        if (!started || complete)
                        {
                            if (c == ' ' || c == '\t' || c == '\r' || c == '\n') continue;
                            if (complete || c != '{') throw Unclassifiable();
                            started = true; depth = 1;
                        }
                        else if (quoted)
                        {
                            if (escaped) escaped = false;
                            else if (c == '\\') escaped = true;
                            else if (c == '"') quoted = false;
                        }
                        else if (c == '"') quoted = true;
                        else if (c == '{' || c == '[') depth++;
                        else if (c == '}' || c == ']') { depth--; complete = depth == 0; }
                    }
                    encoding = reader.CurrentEncoding;
                }
            }
            catch (DecoderFallbackException error) { throw new InvalidDataException(Unclassifiable().Message, error); }
            if (!complete) throw Unclassifiable();
            return encoding;
        }

        private static InvalidDataException Unclassifiable()
        {
            return new InvalidDataException("The existing destination cannot be safely classified as a single JSON object. " +
                "It may be damaged tracking data; choose a new export filename.");
        }

        public static void WriteJson<T>(string path, T value, string activeAnchor)
        {
            WriteJson(path, value, activeAnchor, null);
        }

        internal static void WriteJson<T>(string path, T value, string activeAnchor, Action<string> checkpoint)
        {
            RequireLocation(path, activeAnchor);
            TrackingStorage.RequireJson(path);
            string hash = ObserveTarget(path, activeAnchor);
            TrackingStorage.WriteAtomic(path, value, hash, step => {
                if (checkpoint != null) checkpoint(step);
                if (step == "commit") ObserveTarget(path, activeAnchor);
            }, preserveBackup: false, pretty: true);
        }

        internal static string MarkdownDocument(string body)
        {
            new UTF8Encoding(false, true).GetByteCount(body);
            return MarkdownPrefix + Json.Hash(body) + MarkdownSuffix + body;
        }

        public static string ObserveMarkdownTarget(string path, string activeAnchor)
        {
            RequireLocation(path, activeAnchor);
            if (!String.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Markdown export requires a .md filename; mod/game files are never export targets.");
            FileStream stream;
            try { stream = File.OpenRead(path); }
            catch (FileNotFoundException) { return null; }
            using (stream)
            using (var sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                stream.Position = 0;
                ValidateMarkdown(stream);
                return hash;
            }
        }

        private static void ValidateMarkdown(Stream stream)
        {
            byte[] header = new byte[MarkdownPrefix.Length + 64 + MarkdownSuffix.Length];
            for (int i = 0; i < header.Length; i++)
            {
                int next = stream.ReadByte();
                if (next < 0) throw InvalidMarkdown();
                header[i] = (byte)next;
            }
            string text = Encoding.ASCII.GetString(header);
            if (!text.StartsWith(MarkdownPrefix, StringComparison.Ordinal) ||
                !text.EndsWith(MarkdownSuffix, StringComparison.Ordinal)) throw InvalidMarkdown();
            using (var sha = SHA256.Create())
                if (text.Substring(MarkdownPrefix.Length, 64) !=
                    BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant())
                    throw InvalidMarkdown();
        }

        private static InvalidDataException InvalidMarkdown()
        {
            return new InvalidDataException("The destination is not an intact Scribe-generated Markdown export. " +
                "Edited, unrelated or damaged files are not overwritten; choose a new filename.");
        }

        public static void WriteMarkdown(string path, string text, string activeAnchor)
        {
            WriteMarkdown(path, text, activeAnchor, ObserveMarkdownTarget(path, activeAnchor), null);
        }

        internal static void WriteMarkdown(string path, string text, string activeAnchor, string expectedHash,
            Action<string> checkpoint)
        {
            if (ObserveMarkdownTarget(path, activeAnchor) != expectedHash)
                throw new IOException("The Markdown destination changed outside Scribe. Choose a new filename or review and export again.");
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(text);
            using (var stream = new MemoryStream(bytes)) ValidateMarkdown(stream);
            TrackingStorage.WriteAtomicContent(path, stream => stream.Write(bytes, 0, bytes.Length),
                expectedHash, step => {
                    if (checkpoint != null) checkpoint(step);
                    if (step == "commit") ObserveMarkdownTarget(path, activeAnchor);
                }, false);
        }
    }
}
