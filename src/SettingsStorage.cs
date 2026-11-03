using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Security;

namespace FcsScribe
{
    public sealed class LoadedSettings
    {
        public PluginSettings Value;
        public string FileHash;
        public Exception Error;
    }

    public sealed class SavedSettings
    {
        public string FileHash;
        public string RecoveryBackup;
    }

    public static class SettingsStorage
    {
        public static LoadedSettings Load(string path)
        {
            var result = new LoadedSettings();
            try { result.Value = PluginSettings.Load(path, out result.FileHash); }
            catch (SerializationException error) { result.Error = error; }
            catch (InvalidDataException error) { result.Error = error; }
            catch (UnauthorizedAccessException error) { result.Error = error; }
            catch (IOException error) { result.Error = error; }
            catch (ArgumentException error) { result.Error = error; }
            catch (NotSupportedException error) { result.Error = error; }
            catch (SecurityException error) { result.Error = error; }
            return result;
        }

        public static SavedSettings Save(string path, PluginSettings value, string expectedHash, bool recovering)
        {
            if (value == null) throw new ArgumentNullException("value");
            value.ValidateForUse();
            TrackingStorage.RequireJson(path);
            if (recovering && expectedHash == null)
                throw new IOException("The original settings could not be read safely. Restore access and Reload from disk " +
                    "before saving a repair. No settings file was changed.");
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (AcquireLease(path))
            {
                TrackingStorage.Verify(path, expectedHash);
                string backup = recovering ? PreserveOriginal(path, expectedHash) : null;
                string hash = TrackingStorage.WriteAtomic(path, value, expectedHash);
                return new SavedSettings { FileHash = hash, RecoveryBackup = backup };
            }
        }

        private static FileStream AcquireLease(string path)
        {
            try { return new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException error)
            {
                throw new IOException("Settings could not be locked for saving. Another instance may be saving; " +
                    "reload and retry after it finishes.", error);
            }
        }

        private static string PreserveOriginal(string path, string expectedHash)
        {
            string backup = path + ".recovery-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss'Z'", CultureInfo.InvariantCulture) +
                "_" + Guid.NewGuid().ToString("N") + ".bak";
            string temporary = backup + ".tmp";
            try
            {
                using (var input = File.OpenRead(path))
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                    output.Flush(true);
                }
                if (TrackingStorage.HashFile(temporary) != expectedHash)
                    throw new IOException("Settings changed while preserving the original. Reload before saving; the settings were not overwritten.");
                File.Move(temporary, backup);
                return backup;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
