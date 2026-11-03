using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Xml.Serialization;

namespace FcsScribe
{
    [XmlRoot("ModData")]
    public sealed class WorkshopFile
    {
        public string mod;
        public string title;
        public ulong? id;
        public int? visibility;
        public DateTime? lastUpdate;
        [XmlArrayItem("string")] public string[] tags;
    }

    internal sealed class WorkshopPreview
    {
        private readonly byte[] bytes;
        public readonly PreviewImage Info;

        private WorkshopPreview(byte[] bytes, PreviewImage info) { this.bytes = bytes; Info = info; }

        public static WorkshopPreview Read(string path)
        {
            var info = new PreviewImage { SourceFileName = Path.GetFileName(path), Status = "missing" };
            byte[] bytes;
            try
            {
                using (var input = File.OpenRead(path))
                using (var reader = new BinaryReader(input))
                {
                    if (input.Length > 1024 * 1024)
                        throw new NotSupportedException("The Workshop preview exceeds FCS's supported 1 MB image size.");
                    int length = (int)input.Length;
                    bytes = reader.ReadBytes(length);
                    if (bytes.Length != length) throw new EndOfStreamException("The Workshop preview could not be read completely.");
                }
            }
            catch (FileNotFoundException) { return new WorkshopPreview(null, info); }
            catch (DirectoryNotFoundException) { return new WorkshopPreview(null, info); }
            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var image = Image.FromStream(stream, false, true))
                {
                    Guid format = image.RawFormat.Guid;
                    if (format == ImageFormat.Png.Guid) info.Format = "png";
                    else if (format == ImageFormat.Jpeg.Guid) info.Format = "jpg";
                    else if (format == ImageFormat.Gif.Guid) info.Format = "gif";
                    else throw new NotSupportedException("The Workshop preview is not a supported PNG, JPEG or GIF image.");
                }
            }
            catch (ArgumentException error) { throw new InvalidDataException("The Workshop preview image could not be decoded.", error); }
            catch (ExternalException error) { throw new InvalidDataException("The Workshop preview image decoder failed.", error); }
            using (var sha = SHA256.Create())
                info.Sha256 = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            info.ByteLength = bytes.LongLength;
            info.Status = "available";
            return new WorkshopPreview(bytes, info);
        }

        public void Save(string destination, string fcsDirectory)
        {
            if (bytes == null)
                throw new InvalidOperationException("No local Workshop preview exists. Set an image in FCS's Steam Workshop window first.");
            string target = Path.GetFullPath(destination);
            string protectedRoot = Path.GetFullPath(fcsDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (target.StartsWith(protectedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose an archive location outside the Kenshi/FCS installation.");
            string extension = Path.GetExtension(target);
            if (!String.Equals(extension, "." + Info.Format, StringComparison.OrdinalIgnoreCase) &&
                !(Info.Format == "jpg" && String.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Archive the preview with its original ." + Info.Format + " image extension.");
            using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                output.Write(bytes, 0, bytes.Length);
        }
    }
}
