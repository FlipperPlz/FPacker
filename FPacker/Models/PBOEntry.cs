using FPacker.Compression;
using FPacker.IO;

namespace FPacker.Models;

[Flags]
public enum PackingTypeFlags : int { Uncompressed = 0x00000000, Compressed = 0x43707273 }

public class PBOEntry : IDisposable {
    public string EntryName { get; set; }
    public int PackingType { get; set; }
    public Stream EntryData { get; set; }
    
    private ulong Offset { get; set; }
    private ulong OffsetLocation { get; set; }

    private bool _disposed;
    
    public PBOEntry(string name, Stream entryData, int packingType) {
        EntryName = name;
        EntryData = entryData;
        PackingType = packingType;
    }

    public void WriteMetaData(BinaryWriter writer) {
        writer.WriteAsciiZ(EntryName);
        writer.Write(PackingType);
        writer.Write((ulong) EntryData.Length);
        OffsetLocation = (ulong) writer.BaseStream.Position;
        writer.Write((ulong) 0);
        writer.Write((ulong) 0);

        switch ((PackingTypeFlags) PackingType) {
            case PackingTypeFlags.Uncompressed: {
                writer.Write((ulong) EntryData.Length);
                break;
            }
            case PackingTypeFlags.Compressed: {
                var memStream = new MemoryStream();
                EntryData.CopyTo(memStream);
                var compressedBytes = BisCompression.Compress(memStream.ToArray());
                writer.Write((ulong) compressedBytes.Length);
                
                EntryData.Close();
                EntryData = new MemoryStream(compressedBytes);
                break;
            }
            default: throw new NotSupportedException();
        }
    }

    public void WriteEntryData(BinaryWriter writer) {
        Offset = (ulong) writer.BaseStream.Position;
        var data = new MemoryStream();
        EntryData.CopyTo(data);
        writer.Write(data.ToArray());
        var jump = writer.BaseStream.Position;
        writer.BaseStream.Position = (long)OffsetLocation;
        writer.Write(BitConverter.GetBytes(Offset), 0, 4);
        writer.BaseStream.Position = jump;
    }

    public void Dispose() {
        if(_disposed) return;
        
        _disposed = true;
        EntryData.Dispose();
    }
}