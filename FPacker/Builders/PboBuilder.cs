using System.Security.Cryptography;
using System.Text;
using DZConfigTools.Core.IO;
using FPacker.Models;
using FPacker.Utils;

namespace FPacker.Builders; 

public class PboBuilder : IDisposable {
    private readonly Dictionary<string, string> _headers = new ();
    internal PboEntryBuilder? _entryFactory = null;
    
    
    private readonly string _pboPrefix;
    private bool _disposed;

    public PboBuilder(string prefix) {
        _pboPrefix = prefix;
        _headers.Add("prefix", _pboPrefix);
        //_entries.Add(new PBOEntry("%FPACKER%", new MemoryStream(Encoding.UTF8.GetBytes($"PREFIX = {prefix}")), (int) PackingTypeFlags.Uncompressed));
    }
    
    public PboBuilder WithEntries(IEnumerable<PBOEntry> entries) {
        _entryFactory ??= new PboEntryBuilder(_pboPrefix);
        foreach (var entry in entries) WithEntry(entry);
        return this;
    }

    public PboBuilder WithEntry(PBOEntry entry, DayZFileType fileType = DayZFileType.Misc) {
        _entryFactory ??= new PboEntryBuilder(_pboPrefix);
        _entryFactory.WithEntry(entry, fileType);
        return this;
    }

    public PboBuilder WithEntryBuilder(Func<PboEntryBuilder, PboEntryBuilder> entryBuilder) {
        _entryFactory = entryBuilder.Invoke(_entryFactory ?? new PboEntryBuilder(_pboPrefix));
        return this;
    }

    public PboBuilder WithHeader(string name, string value) {
        _headers.Add(name, value);
        return this;
    }

    public PboBuilder WithHeaders(Dictionary<string, string> headers) {
        foreach (var header in headers) _headers.TryAdd(header.Key, header.Value);
        return this;
    }

    public MemoryStream Build() {
        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)0x00);
        writer.WriteAsciiZ("sreV");
        writer.Write(new byte[15]);
            
        foreach (var (k, v) in _headers) {
            writer.WriteAsciiZ(k);
            writer.WriteAsciiZ(v);
        }
            
        writer.Write((byte) 0x00);


        var entries = _entryFactory?.Build().ToList() ?? new List<PBOEntry>();
            
        foreach (var entry in entries) entry.WriteMetaData(writer);
            
        writer.WriteAsciiZ(string.Empty);
        writer.Write((int) 0x00);
        writer.Write((int) 0x00);
        writer.Write((int) 0x00);
        writer.Write((int) 0x00);
        writer.Write((int) 0x00);
            
        foreach (var entry in entries) {
            entry.WriteEntryData(writer);
        }
            
        var checksum = CalculatePBOChecksum(stream);
        writer.Write((byte) 0x0);
        writer.Write(checksum);
        return stream;
    }

    private static byte[] CalculatePBOChecksum(Stream stream) {
        var oldPos = stream.Position;

        stream.Position = 0;
        #pragma warning disable CS0618
        var hash = new SHA1Managed().ComputeHash(stream);
        #pragma warning restore CS0618

        stream.Position = oldPos;

        return hash;
    }
    
   
    
    public void Dispose() {
        if(_disposed) return;
        _disposed = true;
        _entryFactory?.Dispose();
    }
}