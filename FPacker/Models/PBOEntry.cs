using DZConfigTools.Core.IO;
using FPacker.Compression;

namespace FPacker.Models;

[Flags]
public enum PackingTypeFlags : int { Uncompressed = 0x00000000, Compressed = 0x43707273 }

public class PBOEntry : IDisposable {
    public string EntryName { get; set; }
    public int PackingType { get; set; }
    public MemoryStream EntryData { get; set; }
    
    private ulong Offset { get; set; }
    private ulong OffsetLocation { get; set; }

    private bool _disposed;
    
    public PBOEntry(string name, MemoryStream entryData, int packingType) {
        EntryName = name;
        EntryData = entryData;
        PackingType = packingType;
    }

    public void WriteMetaData(BinaryWriter writer) {
        writer.WriteAsciiZ(EntryName);
        writer.Write(BitConverter.GetBytes(PackingType), 0, 4);
        writer.Write(BitConverter.GetBytes(EntryData.Length), 0, 4);
        OffsetLocation = (ulong) writer.BaseStream.Position;
        writer.Write(BitConverter.GetBytes((long) 0), 0, 4);
        writer.Write(BitConverter.GetBytes((long) 0), 0, 4);

        switch ((PackingTypeFlags) PackingType) {
            case PackingTypeFlags.Uncompressed: {
                writer.Write(BitConverter.GetBytes(EntryData.Length), 0, 4);
                break;
            }
            case PackingTypeFlags.Compressed: {
                
                var compressedBytes = BisCompression.Compress(EntryData.ToArray());
                writer.Write(BitConverter.GetBytes(compressedBytes.Length), 0, 4);
                EntryData = new MemoryStream(compressedBytes);
                break;
            }
            default: throw new NotSupportedException();
        }
    }

    public void WriteEntryData(BinaryWriter writer) {
        Offset = (ulong) writer.BaseStream.Position;
        writer.Write(EntryData.ToArray());
        var jump = writer.BaseStream.Position;
        writer.BaseStream.Position = (long)OffsetLocation;
        writer.Write(BitConverter.GetBytes((long) Offset), 0, 4);
        writer.BaseStream.Position = jump;
    }

    public void Dispose() {
        if(_disposed) return;
        
        _disposed = true;
        EntryData.Dispose();
    }
}