using System.Security.Cryptography;
using System.Text;
using FPacker.IO;
using FPacker.Models;
using FPacker.Utils;

namespace FPacker; 

public class PboFactory : IDisposable {
    private readonly List<PBOEntry> _entries = new();
    private readonly Dictionary<string, string> _headers = new ();
    private readonly string _prefix;
    
    
    private bool _obfuscatedIncludes = false;

    private bool _disposed;

    public PboFactory(string prefix) {
        _headers.Add("prefix", prefix);
        _entries.Add(new PBOEntry("%FPACKER%", new MemoryStream(Encoding.UTF8.GetBytes($"PREFIX = {prefix}")), (int) PackingTypeFlags.Compressed));
    }
    
    public PboFactory WithEntries(IEnumerable<PBOEntry> entries) {
        _entries.AddRange(entries);
        return this;
    }

    public PboFactory WithObfuscatedIncludes() {
        _obfuscatedIncludes = true;
        return this;
    }
    
    public PboFactory WithEntry(PBOEntry entry, bool? includeObfuscated = null) {
        if (includeObfuscated is null) includeObfuscated = _obfuscatedIncludes;
        if (includeObfuscated.Value && (!entry.EntryName.StartsWith("%") && !entry.EntryName.EndsWith('%'))) {
            string incPathOne = ObfuscationTools.GenerateObfuscatedPath(),
                incPathTwo = ObfuscationTools.GenerateObfuscatedPath(),
                incPathThree = ObfuscationTools.GenerateObfuscatedPath(),
                incPathFour = ObfuscationTools.GenerateObfuscatedPath();
            _entries.Add(new PBOEntry(entry.EntryName, ObfuscationTools.GenerateIncludeText(_prefix, incPathOne), (int) PackingTypeFlags.Compressed));
            _entries.Add(new PBOEntry(incPathOne, ObfuscationTools.GenerateIncludeText(_prefix, incPathTwo), (int) PackingTypeFlags.Compressed));
            _entries.Add(new PBOEntry(incPathTwo, ObfuscationTools.GenerateIncludeText(_prefix, incPathThree), (int) PackingTypeFlags.Compressed));
            _entries.Add(new PBOEntry(incPathThree, ObfuscationTools.GenerateIncludeText(_prefix, incPathFour), (int) PackingTypeFlags.Compressed));
            _entries.Add(new PBOEntry(incPathFour, entry.EntryData, (int) PackingTypeFlags.Compressed));
        } else {
            _entries.Add(entry);
        }
        return this;
    }

    public PboFactory WithHeader(string name, string value) {
        _headers.Add(name, value);
        return this;
    }

    public PboFactory WithHeaders(Dictionary<string, string> headers) {
        foreach (var header in headers) _headers.TryAdd(header.Key, header.Value);
        return this;
    }

    public MemoryStream Build() {
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream)) {
            writer.Write((byte)0x00);
            writer.WriteAsciiZ("sreV");
            writer.Write(new byte[15]);
            
            foreach (var (k, v) in _headers) {
                writer.WriteAsciiZ(k);
                writer.WriteAsciiZ(v);
            }
            
            writer.Write((byte) 0x00);

            foreach (var entry in _entries) entry.WriteMetaData(writer);

            writer.WriteAsciiZ(string.Empty);
            writer.Write((int) 0x00);
            writer.Write((int) 0x00);
            writer.Write((int) 0x00);
            writer.Write((int) 0x00);
            writer.Write((int) 0x00);

            
            foreach (var entry in _entries) {
                entry.WriteEntryData(writer);
                for (var i = 0; i < 5; i++) {
                    writer.Write("Sunnyvale Sucks. If you're reading this stop snooping in this obfuscated PBO.\n" +
                                 "Ra ra Rasputin lover of the Russian queen.\n");
                }
            }
            
            var checksum = CalculatePBOChecksum(stream);
            writer.Write((byte) 0x0);
            writer.Write(checksum);
        }

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
        _entries.ForEach(e => e.Dispose());
    }
}