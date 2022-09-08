using System.Text;
using System.Text.RegularExpressions;
using DZConfigTools.Core.Models;
using DZConfigTools.Core.Models.Declarations;
using DZConfigTools.Core.Models.Values;
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
    private bool _renameScripts { get; set; } = false;

    private bool _cfgProtection { get; set; } = false;
    private bool _junkFiles { get; set; } = false;
    private bool _binarizeCfgs { get; set; } = true;
    
    private bool _directoryHasBeenMapped;
    private bool _disposed;
    
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
    
    public PboEntryBuilder WithRenamedScripts() {
        _renameScripts = true;
        return this;
    }

    public PboEntryBuilder FromDirectory(string pboRoot) {
        if (_directoryHasBeenMapped) throw new Exception("PboEntryBuilder::FromDirectory can only be called once!");
        var readFiles = new List<string>();
        foreach (var file in new DirectoryInfo(pboRoot).EnumerateFiles(@"config.cpp", SearchOption.AllDirectories)) {
            if (readFiles.Contains(file.FullName)) continue;
            var parserResult = ParamFile.OpenStream(File.OpenRead(file.FullName));

            if (!parserResult.IsSuccess) {
                Console.Out.WriteLineAsync($"Failed to parse ParamFile: {file.FullName}.");
                Console.Out.WriteLineAsync(string.Join("\n", parserResult.Errors));
                throw new ParseException(parserResult.Errors);
            } 
            
            
            var paramFile = parserResult.Value;
            Func< (string, string), List<PBOEntry>> GatherScriptsInFolder = inOutPair => {
                var scriptLocation = inOutPair.Item1;
                var outputRoot = inOutPair.Item2;
                string scriptName;

                var entries = new List<PBOEntry>();
                scriptLocation = new Regex(Regex.Escape(_pboPrefix)).Replace(scriptLocation, pboRoot);
                if (new DirectoryInfo(scriptLocation).Exists) {
                    foreach (var script in new DirectoryInfo(scriptLocation).EnumerateFiles("*.c", SearchOption.AllDirectories)) {
                        scriptName = _renameScripts ? ObfuscationTools.GenerateObfuscatedPath(extension:".c") : script.Name;
                        entries.Add(new PBOEntry(Path.Combine(outputRoot, scriptName), new MemoryStream(File.ReadAllBytes(script.FullName)), (int)PackingTypeFlags.Compressed));
                        readFiles.Add(script.FullName);
                    }
                    return entries;
                }
                if (!new FileInfo(scriptLocation).Exists) throw new Exception($"Failed to find {scriptLocation}, as referenced in {file.FullName}");
                scriptName = _renameScripts ? ObfuscationTools.GenerateObfuscatedPath(".c") : new FileInfo(scriptLocation).Name;
                entries.Add(new PBOEntry(Path.Combine(outputRoot, scriptName), new MemoryStream(File.ReadAllBytes(scriptLocation)), (int)PackingTypeFlags.Compressed));
                readFiles.Add(scriptLocation);
                return entries;
            };
            foreach (var rootStatement in paramFile.Statements) {
                if (rootStatement is not RapClassDeclaration rootParamClass) continue;
                if (!rootParamClass.Classname.Equals("CfgMods", StringComparison.InvariantCultureIgnoreCase)) continue;
                foreach (var cfgModsChild in rootParamClass.Statements.Where(s => s is RapClassDeclaration)
                             .Cast<RapClassDeclaration>()) {
                    foreach (var modContainerChildren in cfgModsChild.Statements
                                 .Where(s => s is RapClassDeclaration).Cast<RapClassDeclaration>()) {
                        if (modContainerChildren.Classname.Equals("defs",
                                StringComparison.InvariantCultureIgnoreCase)) {
                            foreach (var defineClass in modContainerChildren.Statements.Where(s => s is RapClassDeclaration)
                                         .Cast<RapClassDeclaration>()) {
                                switch (defineClass.Classname.ToLower()) {
                                    case "engine" + "script" + "module" or
                                        "game" + "lib" + "script" + "module" or
                                        "game" + "script" + "module" or
                                        "world" + "script" + "module" or
                                        "mission" + "script" + "module": {
                                        foreach (var defineArray in defineClass.Statements
                                                     .Where(s => s is RapArrayDeclaration)
                                                     .Cast<RapArrayDeclaration>()) {
                                            if (defineArray.ArrayName.ToLower() != "files") continue;
                                            var scriptType = defineClass.Classname.ToLower() switch {
                                                "engine" + "script" + "module" => DayZFileType.EngineScript,
                                                "game" + "lib" + "script" + "module" => DayZFileType.GameLibScript,
                                                "game" + "script" + "module" => DayZFileType.GameScript,
                                                "world" + "script" + "module" => DayZFileType.WorldScript,
                                                "mission" + "script" + "module" => DayZFileType.MissionScript,
                                                _ => throw new Exception($"Unknown module type: {defineClass.Classname}")
                                            };
                                            foreach (var scriptPath in defineArray.ArrayValue.Entries
                                                         .Where(e => e is RapString).Cast<RapString>()) {
                                                string outPath = scriptPath;
                                                if (_relocateConfigs) outPath = $"__JAPM__\\{ObfuscationTools.RandomString(4, allowableChars: "  ", includeSpaces: true)}\\  \\ {ObfuscationTools.GetRandomIllegalFilename()} \\ {ObfuscationTools.GetRandomIllegalFilename()}\\  \\";
                                                
                                                WithEntries(GatherScriptsInFolder.Invoke((scriptPath, outPath)), scriptType);
                                                scriptPath.Value = $"{_pboPrefix}\\{outPath}";
                                            }
                                        }

                                        break;
                                    }
                                    default: break;
                                }
                            }
                        }
                    }
                }
            }

            WithEntry(
                new PBOEntry(Path.GetRelativePath(pboRoot, file.FullName),
                    paramFile.WriteToStream(),
                    (int)PackingTypeFlags.Compressed), DayZFileType.ParamFile);

            readFiles.Add(file.FullName);
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
            parentFolder = parentFolder.Remove(parentFolder.Length - 1);
            
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
        
        if(_entries.ContainsKey(DayZFileType.Misc)) entries.AddRange(_entries[DayZFileType.Misc]); 
        if(_entries.ContainsKey(DayZFileType.Model)) entries.AddRange(_entries[DayZFileType.Model]); 
        if(_entries.ContainsKey(DayZFileType.EngineScript)) entries.AddRange(_entries[DayZFileType.EngineScript]); 
        if(_entries.ContainsKey(DayZFileType.GameScript)) entries.AddRange(_entries[DayZFileType.GameScript]); 
        if(_entries.ContainsKey(DayZFileType.GameLibScript)) entries.AddRange(_entries[DayZFileType.GameLibScript]); 
        if(_entries.ContainsKey(DayZFileType.MissionScript)) entries.AddRange(_entries[DayZFileType.MissionScript]); 
        if(_entries.ContainsKey(DayZFileType.WorldScript)) entries.AddRange(_entries[DayZFileType.WorldScript]); 

        if(_entries.ContainsKey(DayZFileType.Texture)) entries.AddRange(_entries[DayZFileType.Texture]); 

        
        if (_junkFiles) {
            var newEntries = new List<PBOEntry>();
            newEntries.AddRange(AllEntries.ToList().Select(entry => ObfuscationTools.GenerateJunkEntry(entry.EntryName)));
            entries.ForEach(ent => {
                newEntries.Add(new PBOEntry(ent.EntryName, new MemoryStream(), (int) PackingTypeFlags.Compressed));
                newEntries.Add(new PBOEntry(ent.EntryName, new MemoryStream(), (int) PackingTypeFlags.Uncompressed));
                newEntries.Add(new PBOEntry(ent.EntryName, new MemoryStream(), 1337420));
                newEntries.Add(new PBOEntry(ent.EntryName.Replace(" ", String.Empty), new MemoryStream(), (int) PackingTypeFlags.Compressed));
            });
            newEntries.AddRange(entries);
            for (var i = 0; i < 50; i++) {
                newEntries.Add(new PBOEntry(ObfuscationTools.GenerateObfuscatedPath(), new MemoryStream(), 0));    
                newEntries.Add(new PBOEntry(ObfuscationTools.GenerateObfuscatedPath($"__JAPM__\\{ObfuscationTools.RandomString(4)}\\"), new MemoryStream(), 0));    

            }

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
    MissionScript,
    WorldScript,
    GameScript,
    GameLibScript,
    EngineScript,
    ParamFile,
    Model,
    Texture,
    Misc
}