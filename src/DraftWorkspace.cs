using System;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;

namespace FcsScribe
{
    [DataContract]
    public sealed class DraftWorkspace
    {
        public const string FormatName = "FCS-Scribe draft workspace";
        [DataMember(Order = 0)] public string Format = FormatName;
        [DataMember(Order = 1)] public int SchemaVersion = 1;
        [DataMember(Order = 2)] public Packet Packet;
        [DataMember(Order = 3)] public string PacketHash;
        [DataMember(Order = 4)] public string DraftText;
        public string FileHash;

        public static DraftWorkspace Create(Packet packet, string text)
        {
            var workspace = new DraftWorkspace {
                Packet = packet, PacketHash = packet == null ? null : Json.StreamHash(packet), DraftText = text
            };
            workspace.Validate();
            return workspace;
        }

        public void Validate()
        {
            if (Format != FormatName || SchemaVersion != 1 || DraftText == null)
                throw new InvalidDataException("This is not a supported draft workspace.");
            if (Packet == null)
            {
                if (PacketHash != null)
                    throw new InvalidDataException("The workspace's associated AI packet is missing.");
            }
            else
            {
                TrackingDocument.ValidatePacket(Packet);
                if (PacketHash != Json.StreamHash(Packet))
                    throw new InvalidDataException("The workspace's original AI packet changed or is incomplete.");
            }
        }

        public static DraftWorkspace Load(string path)
        {
            TrackingStorage.RequireJson(path);
            using (var stream = File.OpenRead(path))
            {
                DraftWorkspace workspace = Json.ReadStream<DraftWorkspace>(stream);
                if (workspace == null) throw new InvalidDataException("The draft workspace is empty.");
                workspace.Validate();
                stream.Position = 0;
                using (var sha = SHA256.Create())
                    workspace.FileHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                return workspace;
            }
        }

        public string Review(Snapshot current, Func<string, string> existingNotes)
        {
            if (Packet == null)
                return "Text-only workspace: no associated AI packet. It cannot be applied; export a packet to document changes.";
            try
            {
                NoteDraft notes = Json.Read<NoteDraft>(DraftText);
                int count = DraftValidation.Pending(Packet, notes, current, existingNotes).Count;
                return count + " pending notes pass evidence/current-state checks. Review the prose; approval is still required.";
            }
            catch (SerializationException error) { return "Draft is unfinished or invalid JSON: " + error.Message; }
            catch (InvalidDataException error) { return "Draft cannot be applied: " + error.Message; }
            catch (InvalidOperationException error) { return "Draft cannot be applied: " + error.Message; }
        }
    }
}
