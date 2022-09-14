using System.Text;
using DZConfigTools.Core.Models;
using DZConfigTools.Core.Models.Declarations;
using FPacker.Exceptions;
using FPacker.Models;
using FPacker.Utils;

namespace FPacker.Builders; 

public class PboEntryBuilder : IDisposable {
    private readonly string _pboPrefix;
    private readonly Dictionary<DayZFileType, List<PBOEntry>> _entries = new Dictionary<DayZFileType, List<PBOEntry>>();

    private IEnumerable<PBOEntry> AllEntries => _entries.Select(s => s.Value).SelectMany(x => x);

    private bool _relocateConfigs { get; set; } = false;
    private bool _relocateScripts { get; set; } = false;

    private bool _cfgProtection { get; set; } = false;
    private bool _junkFiles { get; set; } = false;
    private bool _binarizeCfgs { get; set; } = true;
    
    private bool _directoryHasBeenMapped;
    private bool _disposed;
    private List<DayZScripts> _scripts = new();

    public PboEntryBuilder(string pboPrefix) =>_pboPrefix = pboPrefix;

    public PboEntryBuilder WithEntry(PBOEntry entry, DayZFileType fileType) {
        if(!_entries.ContainsKey(fileType)) _entries.Add(fileType, new List<PBOEntry>());
        _entries[fileType].Add(entry);
        return this;
    }
    
    
    private PboEntryBuilder WithEntries(IEnumerable<PBOEntry> entries, DayZFileType fileType) {
        if(!_entries.ContainsKey(fileType)) _entries.Add(fileType, new List<PBOEntry>());
        _entries[fileType].AddRange(entries);
        return this;
    }

    public PboEntryBuilder WithConfigProtection() {
        _cfgProtection = true;
        return this;
    }
    
    public PboEntryBuilder WithoutBinarizedConfigs() {
        _binarizeCfgs = false;
        return this;
    }
    
    public PboEntryBuilder WithRelocatedConfigs() {
        _relocateConfigs = true;
        return this;
    }
    
    public PboEntryBuilder WithJunkFiles() {
        _junkFiles = true;
        return this;
    }

    public PboEntryBuilder WithRelocatedScripts() {
        _relocateScripts = true;
        return this;
    }
    
    public PboEntryBuilder FromDirectory(string pboRoot) {
        if (_directoryHasBeenMapped) throw new Exception("PboEntryBuilder::FromDirectory can only be called once!");
        var readFiles = new List<string>();
        foreach (var file in new DirectoryInfo(pboRoot).EnumerateFiles(@"config.*", SearchOption.AllDirectories)) {
            if(file.Extension != ".bin" && file.Extension != ".cpp") continue;
            if (readFiles.Contains(file.FullName.ToLower())) continue;
            var parserResult = ParamFile.OpenStream(File.OpenRead(file.FullName));

            if (!parserResult.IsSuccess) {
                Console.Out.WriteLineAsync($"Failed to parse ParamFile: {file.FullName}.");
                Console.Out.WriteLineAsync(string.Join("\n", parserResult.Errors));
                throw new ParseException(parserResult.Errors);
            } 
            
            
            var paramFile = parserResult.Value;
            foreach (var rootStatement in paramFile.Statements) {
                if (rootStatement is not RapClassDeclaration rootParamClass) continue;
                if (!rootParamClass.Classname.Equals("CfgMods", StringComparison.InvariantCultureIgnoreCase)) continue;
                foreach (var cfgModsChild in rootParamClass.Statements.Where(s => s is RapClassDeclaration).Cast<RapClassDeclaration>()) _scripts.Add(DayZScripts.GatherScripts(cfgModsChild, _pboPrefix, pboRoot));
            }

            WithEntry(new PBOEntry(Path.GetRelativePath(pboRoot, file.FullName),
                    paramFile.WriteToStream(),
                    (int)PackingTypeFlags.Compressed), DayZFileType.ParamFile);

            readFiles.Add(file.FullName.ToLower());
        }

        foreach (var file in new DirectoryInfo(pboRoot).EnumerateFiles("*", SearchOption.AllDirectories)) {
            if (readFiles.Contains(file.FullName.ToLower())) continue;
            if (file.Extension.ToLower() == ".h" || file.Extension.ToLower() == ".c") continue;
            var scriptType = file.Extension switch {
                ".p3d" => DayZFileType.Model,
                ".paa" => DayZFileType.Texture,
                ".rvmat" => DayZFileType.RVMat,
                _ => DayZFileType.Misc
            };
            WithEntry(
                new PBOEntry(Path.GetRelativePath(pboRoot, file.FullName),
                    new MemoryStream(File.ReadAllBytes(file.FullName)),
                    (int)PackingTypeFlags.Compressed), scriptType);

            readFiles.Add(file.FullName.ToLower());
        }
        _directoryHasBeenMapped = true;
        return this;
    }

    private string CfgProtection_RecursiveSweep(RapClassDeclaration classDeclaration, ref List<PBOEntry> entries, string parentFolder) {
        var retValue = "#include \"\0\"";
        var fileName = ObfuscationTools.GenerateSimpleObfuscatedPath(out var fName, parentFolder);
        retValue = retValue.Replace("\0", fName);
        StringBuilder builder = new StringBuilder("class ").Append(classDeclaration.Classname);
        if (classDeclaration.ParentClassname is not null) builder.Append(": ").Append(classDeclaration.ParentClassname);
        builder.Append(" {\n");
        foreach (var cStatement in classDeclaration.Statements) {
            if(_junkFiles) entries.Add(new PBOEntry(ObfuscationTools.GenerateSimpleObfuscatedPath(out var neverUsed, parentFolder), new MemoryStream(), (int) PackingTypeFlags.Compressed));
            switch (cStatement) {
                case RapClassDeclaration clazz: {
                    builder.Append(CfgProtection_RecursiveSweep(clazz, ref entries, parentFolder)).Append('\n');
                    break;
                }
                default: {
                    var incName = ObfuscationTools.GenerateSimpleObfuscatedPath(out var fName2, parentFolder);
                    entries.Add(new PBOEntry(incName, new MemoryStream(Encoding.UTF8.GetBytes(cStatement.ToParseTree())), (int) PackingTypeFlags.Compressed));
                    builder.Append("#include \"").Append(fName2).Append('\"').Append('\n');
                    break;
                }
            }
        }
        builder.Append("};");
        entries.Add(new PBOEntry(fileName, new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString())), (int) PackingTypeFlags.Compressed));
        return retValue;
    }
    
    public IEnumerable<PBOEntry> Build() {
        var entries = new List<PBOEntry>();

        if(_entries.ContainsKey(DayZFileType.Misc)) entries.AddRange(_entries[DayZFileType.Misc]); 
        if(_entries.ContainsKey(DayZFileType.Model)) entries.AddRange(_entries[DayZFileType.Model]);
        if(_entries.ContainsKey(DayZFileType.Texture)) entries.AddRange(_entries[DayZFileType.Texture]);

        foreach (var scriptCtx in _scripts) {
            if(_relocateScripts) scriptCtx.ObfuscateScriptNames();
            if(!_entries.ContainsKey(DayZFileType.ParamFile)) _entries.Add(DayZFileType.ParamFile, new List<PBOEntry>());
            _entries[DayZFileType.ParamFile].AddRange(scriptCtx.GenerateConfigs(_pboPrefix).Select(s =>
                new PBOEntry(ObfuscationTools.GenerateObfuscatedPath() + "\\config.cpp",
                    s.WriteToStream(true), (int)PackingTypeFlags.Uncompressed)));
            entries.AddRange(scriptCtx.GetScriptEntries());
        }
        
        
        
        foreach (var entry in _entries[DayZFileType.ParamFile]) {
            entry.EntryName = entry.EntryName.Replace("config.bin", "config.cpp");
            if (_relocateConfigs) entry.EntryName = ObfuscationTools.GenerateObfuscatedPath() + "\\config.cpp";
            ParamFile paramFile = ParamFile.OpenStream(new MemoryStream(entry.EntryData.ToArray()));

            
            if (!_cfgProtection) {
                if (_binarizeCfgs) {
                    entry.EntryName = entry.EntryName.Replace("config.cpp", "config.bin");
                    entry.EntryData = paramFile.WriteToStream();
                    entries.Add(entry);
                    continue;
                }

                entry.EntryData = paramFile.WriteToStream(false);
                entries.Add(entry);
                continue;
            }

            #region Segment Config With Includes
            var parentFolder = entry.EntryName.Replace("config.cpp", string.Empty);
            if(parentFolder.Length != 0) parentFolder = parentFolder.Remove(parentFolder.Length - 1);
            
            var newCfgBuilder = new StringBuilder();
            foreach (var statement in paramFile.Statements) {
                switch (statement) {
                    case RapClassDeclaration clazz: {
                        newCfgBuilder.Append(CfgProtection_RecursiveSweep(clazz, ref entries, parentFolder)).Append('\n');
                        break;
                    }
                    default: {
                        var incName = ObfuscationTools.GenerateSimpleObfuscatedPath(out var fName, parentFolder);
                        entries.Add(new PBOEntry(incName, new MemoryStream(Encoding.UTF8.GetBytes(statement.ToParseTree())), (int) PackingTypeFlags.Compressed));
                        newCfgBuilder.Append("#include \"").Append(fName).Append('\"').Append('\n');
                        break;
                    }
                }
                if(_junkFiles) entries.Add(new PBOEntry(ObfuscationTools.GenerateSimpleObfuscatedPath(out var neverUsed, parentFolder), new MemoryStream(), (int) PackingTypeFlags.Compressed));
            }
            entries.Add(new PBOEntry(entry.EntryName, new MemoryStream(Encoding.UTF8.GetBytes(newCfgBuilder.ToString())), (int) PackingTypeFlags.Compressed));
            #endregion
            
        }
        
        if (_junkFiles) {
            var newEntries = new List<PBOEntry>();
            newEntries.AddRange(AllEntries.ToList().Select(entry => ObfuscationTools.GenerateJunkEntry(entry.EntryName)));
            entries.ForEach(ent => {
                newEntries.Add(new PBOEntry(ent.EntryName + "\\*.*", new MemoryStream(), (int) PackingTypeFlags.Compressed));
            });
            for (var i = 0; i < 50; i++) {
                newEntries.Add(new PBOEntry(ObfuscationTools.GenerateObfuscatedPath(), new MemoryStream(), 0));    
                newEntries.Add(new PBOEntry(ObfuscationTools.GenerateObfuscatedPath($"__JAPM__\\{ObfuscationTools.RandomString(16, allowableChars: "!@#$%^&*()<>.<~:;?+=-_", includeSpaces: true)}\\"), new MemoryStream(), 0));
            }
            newEntries.AddRange(entries);

            newEntries.Add(new PBOEntry("*.*", new MemoryStream(), (int) PackingTypeFlags.Compressed));

            entries = newEntries;
        }
        
        return entries;
    }



    public void Dispose() {
        if(_disposed) return;
        _disposed = true;
        _entries.ToList().ForEach(v => v.Value.ForEach(e => e.Dispose()));
    }
}

public enum DayZFileType {
    ParamFile,
    RVMat,
    Model,
    Texture,
    Misc
}