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

    private bool _cfgProtection { get; set; } = false;
    private bool _junkFiles { get; set; } = false;
    private bool _renameScripts { get; set; } = false;
    
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
    
    public PboEntryBuilder WithJunkFiles() {
        _junkFiles = true;
        return this;
    }

    public PboEntryBuilder RenameScripts() {
        _renameScripts = true;
        return this;
    }

    public PboEntryBuilder FromDirectory(string pboRoot) {
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
            Func<string, List<PBOEntry>> GatherScriptsInFolder = scriptLocation => {
                var entries = new List<PBOEntry>();
                scriptLocation = new Regex(Regex.Escape(_pboPrefix)).Replace(scriptLocation, pboRoot);
                if (new DirectoryInfo(scriptLocation).Exists) {
                    foreach (var script in new DirectoryInfo(scriptLocation).EnumerateFiles("*.c", SearchOption.AllDirectories)) {
                        entries.Add(new PBOEntry(Path.GetRelativePath(pboRoot, script.FullName), new MemoryStream(File.ReadAllBytes(script.FullName)), (int)PackingTypeFlags.Compressed));
                        readFiles.Add(script.FullName);
                    }
                    return entries;
                }
                if (!new FileInfo(scriptLocation).Exists) throw new Exception($"Failed to find {scriptLocation}, as referenced in {file.FullName}");
                    
                entries.Add(new PBOEntry(Path.GetRelativePath(pboRoot, scriptLocation), new MemoryStream(File.ReadAllBytes(scriptLocation)), (int)PackingTypeFlags.Compressed));
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

                                            foreach (var scriptPath in defineArray.ArrayValue.Entries
                                                         .Where(e => e is RapString).Cast<RapString>()) {
                                                WithEntries(GatherScriptsInFolder.Invoke(scriptPath),
                                                    DayZFileType.Script);
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

        return this;
    }

    private string CfgProtection_RecursiveSweep(RapClassDeclaration classDeclaration, ref List<PBOEntry> entries) {
        var retValue = "#include \"\0\"";
        var fileName = ObfuscationTools.GenerateObfuscatedPath();
        retValue = retValue.Replace("\0", fileName);
        StringBuilder builder = new StringBuilder("class ").Append(classDeclaration.Classname);
        if (classDeclaration.ParentClassname is not null) builder.Append(": ").Append(classDeclaration.ParentClassname);
        builder.Append(" {\n");
        foreach (var cStatement in classDeclaration.Statements) {
            switch (cStatement) {
                case RapClassDeclaration clazz: {
                    builder.Append(CfgProtection_RecursiveSweep(clazz, ref entries)).Append('\n');
                    break;
                }
                default: {
                    var incName = ObfuscationTools.GenerateObfuscatedPath();
                    entries.Add(new PBOEntry(incName, new MemoryStream(Encoding.UTF8.GetBytes(cStatement.ToParseTree())), (int) PackingTypeFlags.Compressed));
                    builder.Append("#include \"").Append(incName).Append('\"').Append('\n');
                    break;
                }
            }
        }
        builder.Append("\n\n};");
        entries.Add(new PBOEntry(fileName, new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString())), (int) PackingTypeFlags.Compressed));
        return retValue;
    }


    public IEnumerable<PBOEntry> Build() {
        var entries = new List<PBOEntry>();

        foreach (var entry in _entries[DayZFileType.ParamFile]) {
            if (!_cfgProtection) {
                entries.Add(entry);
                continue;
            }

            ParamFile paramFile = ParamFile.OpenStream(new MemoryStream(entry.EntryData.ToArray()));
            var newCfgBuilder = new StringBuilder();
            foreach (var statement in paramFile.Statements) {
                switch (statement) {
                    case RapClassDeclaration clazz: {
                        newCfgBuilder.Append(CfgProtection_RecursiveSweep(clazz, ref entries)).Append('\n');
                        break;
                    }
                    default: {
                        var incName = ObfuscationTools.GenerateObfuscatedPath();
                        entries.Add(new PBOEntry(incName, new MemoryStream(Encoding.UTF8.GetBytes(statement.ToParseTree())), (int) PackingTypeFlags.Compressed));
                        newCfgBuilder.Append("#include \"").Append(incName).Append('\"').Append('\n');
                        break;
                    }
                }
            }
            entries.Add(new PBOEntry(entry.EntryName, new MemoryStream(Encoding.UTF8.GetBytes(newCfgBuilder.ToString())), (int) PackingTypeFlags.Compressed));
        }
        
        if (_renameScripts)
            _entries[DayZFileType.Script].ForEach(e =>
                e.EntryName = e.EntryName.Replace(Path.GetFileName(e.EntryName),
                    ObfuscationTools.GenerateObfuscatedPath(extension: ".c")));
        if(_entries.ContainsKey(DayZFileType.Misc)) entries.AddRange(_entries[DayZFileType.Misc]); 
        if(_entries.ContainsKey(DayZFileType.Model)) entries.AddRange(_entries[DayZFileType.Model]); 
        if(_entries.ContainsKey(DayZFileType.Script)) entries.AddRange(_entries[DayZFileType.Script]); 
        if(_entries.ContainsKey(DayZFileType.Texture)) entries.AddRange(_entries[DayZFileType.Texture]); 

        
        if (_junkFiles) {
            entries.AddRange(AllEntries.ToList().Select(entry => ObfuscationTools.GenerateJunkEntry(entry.EntryName)));
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
    Script,
    ParamFile,
    Model,
    Texture,
    Misc
}