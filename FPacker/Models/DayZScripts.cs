using System.Text.RegularExpressions;
using DZConfigTools.Core.Models;
using DZConfigTools.Core.Models.Declarations;
using DZConfigTools.Core.Models.Statements;
using DZConfigTools.Core.Models.Values;
using FPacker.Utils;

namespace FPacker.Models; 

public class DayZScripts {
        public class DayZScriptDefineClass {
            public string EntryPoint { get; set; } = string.Empty;
            public List<PBOEntry> ScriptEntries { get; set; } = new List<PBOEntry>();

            public void AddScripts(string pboRoot, string pboPrefix, RapClassDeclaration defineClass) {
                foreach (var statement in defineClass.Statements) {
                    if (statement is RapVariableDeclaration entryPointVar) {
                        if(entryPointVar.VariableName.ToLower() != "value") continue;
                        if (entryPointVar.VariableValue is not RapString entryPointValue) continue;
                        EntryPoint = entryPointValue.Value;
                        continue;
                    }
                    if (statement is RapArrayDeclaration filesVar) {
                        if(filesVar.ArrayName.ToLower() != "files") continue;
                        filesVar.ArrayValue.Entries.Where(e => e is RapString).Cast<RapString>().ToList().ForEach(s => GatherScriptsAtPath(s.Value, pboRoot, pboPrefix));
                        continue;   
                    }
                }
            }

            private void GatherScriptsAtPath(string pboPath, string pboRoot, string pboPrefix) {
                var scriptLocation = new Regex(Regex.Escape(pboPrefix)).Replace(pboPath, pboRoot);
                if (new DirectoryInfo(scriptLocation).Exists) {
                    foreach (var script in new DirectoryInfo(scriptLocation).EnumerateFiles("*",
                                 SearchOption.AllDirectories)) {
                        if (script.Extension.ToLower() != ".c" && script.Extension.ToLower() != ".h") continue;
                        ScriptEntries.Add(new PBOEntry(Path.GetRelativePath(pboRoot, script.FullName), new MemoryStream(File.ReadAllBytes(script.FullName)), (int)PackingTypeFlags.Compressed));
                    }
                    return;
                }

                var scriptInfo = new FileInfo(scriptLocation);
                if (!scriptInfo.Exists) throw new Exception($"Failed to find {scriptLocation}, as referenced in config");
                ScriptEntries.Add(new PBOEntry(Path.GetRelativePath(pboRoot, scriptInfo.FullName), new MemoryStream(File.ReadAllBytes(scriptLocation)), (int)PackingTypeFlags.Compressed));
                
            }

            public void ObfuscateScriptNames(string modsClass, int module) {
                var prefix = modsClass + "\\" + new string(' ', module) + "\\ " + this.EntryPoint + " \\";
                foreach (var entry in ScriptEntries) entry.EntryName = ObfuscationTools.GenerateObfuscatedPath(prefix);
            }
        }

        public string CfgModsClass { get; set; } = null!;
        public DayZScriptDefineClass? MissionScripts { get; set; }
        public DayZScriptDefineClass? WorldScripts { get; set; }
        public DayZScriptDefineClass? GameScripts { get; set; }
        public DayZScriptDefineClass? GameLibScripts { get; set; }
        public DayZScriptDefineClass? EngineScripts { get; set; }

        public void ObfuscateScriptNames() {
            if (MissionScripts is not null) MissionScripts.ObfuscateScriptNames(CfgModsClass, 5);
            if (WorldScripts is not null) WorldScripts.ObfuscateScriptNames(CfgModsClass, 4);
            if (GameScripts is not null) GameScripts.ObfuscateScriptNames(CfgModsClass, 3);
            if (GameLibScripts is not null) GameLibScripts.ObfuscateScriptNames(CfgModsClass, 2);
            if (EngineScripts is not null) EngineScripts.ObfuscateScriptNames(CfgModsClass, 1);
        }

        public IEnumerable<ParamFile> GenerateConfigs(string pboPrefix) {
            var cfgs = new List<ParamFile>();
            if(MissionScripts is not null) cfgs.Add(CreateParamFile("missionScriptModule", pboPrefix, MissionScripts));
            if(WorldScripts is not null) cfgs.Add(CreateParamFile("worldScriptModule", pboPrefix, WorldScripts));
            if(GameScripts is not null) cfgs.Add(CreateParamFile("gameScriptModule", pboPrefix, GameScripts));
            if(GameLibScripts is not null) cfgs.Add(CreateParamFile("gameLibScriptModule", pboPrefix, GameLibScripts));
            if(EngineScripts is not null) cfgs.Add(CreateParamFile("engineScriptModule", pboPrefix, EngineScripts));
            return cfgs;
        }

        public IEnumerable<PBOEntry> GetScriptEntries() {
            var scripts = new List<PBOEntry>();
            if(MissionScripts is not null) scripts.AddRange(MissionScripts.ScriptEntries);
            if(WorldScripts is not null) scripts.AddRange(WorldScripts.ScriptEntries);
            if(GameScripts is not null) scripts.AddRange(GameScripts.ScriptEntries);
            if(GameLibScripts is not null) scripts.AddRange(GameLibScripts.ScriptEntries);
            if(EngineScripts is not null) scripts.AddRange(EngineScripts.ScriptEntries);
            return scripts;
        }

        private ParamFile CreateParamFile(string module, string pboPrefix, DayZScriptDefineClass defClass) {

            var obfClazz = CfgModsClass + "_pepega_" + ObfuscationTools.RandomString(8);
            return new ParamFile() {
                Statements = new List<IRapStatement>() {
                    new RapClassDeclaration() {
                        Classname = "CfgPatches",
                        Statements = new List<IRapStatement>() {
                            new RapClassDeclaration() {
                                Classname = obfClazz,
                                Statements = new List<IRapStatement>() {
                                    new RapArrayDeclaration() {
                                        ArrayName = "requiredAddons",
                                        ArrayValue = new RapArray() {
                                            Entries = new List<IRapArrayEntry>() {
                                                new RapString() {
                                                    Value = "DZ_Data"
                                                }
                                            } 
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new RapClassDeclaration() {
                        Classname = "CfgMods",
                        Statements = new List<IRapStatement>() {
                            new RapExternalClassStatement() {
                                Classname = CfgModsClass
                            },
                            new RapClassDeclaration() {
                                Classname = obfClazz,
                                ParentClassname = CfgModsClass,
                                Statements = new List<IRapStatement>() {
                                    new RapClassDeclaration() {
                                        Classname = "defs",
                                        Statements = new List<IRapStatement>() {
                                            new RapClassDeclaration() {
                                                Classname = module,
                                                Statements = new List<IRapStatement>() {
                                                    new RapVariableDeclaration() {
                                                        VariableName = "value",
                                                        VariableValue = new RapString() {
                                                            Value = defClass.EntryPoint
                                                        }
                                                    },
                                                    new RapAppensionStatement() {
                                                        Target = "files",
                                                        Array = new RapArray() {
                                                            Entries = new List<IRapArrayEntry>(
                                                                defClass.ScriptEntries.Select(v => new RapString()
                                                                    { Value = $"{pboPrefix}\\{v.EntryName}" }))
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        public static DayZScripts GatherScripts(RapClassDeclaration cfgModsChild, string pboPrefix, string pboRoot) {
            var output = new DayZScripts();
            output.CfgModsClass = cfgModsChild.Classname;
            foreach (var modContainerChildren in cfgModsChild.Statements.Where(s => s is RapClassDeclaration).Cast<RapClassDeclaration>()) {
                if (!modContainerChildren.Classname.Equals("defs", StringComparison.InvariantCultureIgnoreCase)) continue;
                foreach (var defineClass in modContainerChildren.Statements.Where(s => s is RapClassDeclaration).Cast<RapClassDeclaration>().ToList()) {
                    switch (defineClass.Classname.ToLower()) {
                        case "engine" + "script" + "module":
                            output.LoadScripts(defineClass, pboRoot, pboPrefix, 1);
                            modContainerChildren.Statements.Remove(defineClass); break;
                        case "game" + "lib" + "script" + "module":
                            output.LoadScripts(defineClass, pboRoot, pboPrefix, 2); 
                            modContainerChildren.Statements.Remove(defineClass); break;
                        case "game" + "script" + "module":
                            output.LoadScripts(defineClass, pboRoot, pboPrefix, 3); 
                            modContainerChildren.Statements.Remove(defineClass); break;
                        case "world" + "script" + "module":
                            output.LoadScripts(defineClass, pboRoot, pboPrefix, 4);
                            modContainerChildren.Statements.Remove(defineClass); break;
                        case "mission" + "script" + "module":
                            output.LoadScripts(defineClass, pboRoot, pboPrefix, 5);
                            modContainerChildren.Statements.Remove(defineClass); break;
                        default: break;
                    }
                }
            }

            return output;
        }

        private void LoadScripts(RapClassDeclaration defineClass, string pboRoot, string pboPrefix, int module) {
            switch (module) {
                case 1: {
                    EngineScripts ??= new DayZScriptDefineClass();
                    EngineScripts.AddScripts(pboRoot, pboPrefix, defineClass);
                    break;
                }
                case 2: {
                    GameLibScripts ??= new DayZScriptDefineClass();
                    GameLibScripts.AddScripts(pboRoot, pboPrefix, defineClass);
                    break;
                }
                case 3: {
                    GameScripts ??= new DayZScriptDefineClass();
                    GameScripts.AddScripts(pboRoot, pboPrefix, defineClass);
                    break;
                }
                case 4: {
                    WorldScripts ??= new DayZScriptDefineClass();
                    WorldScripts.AddScripts(pboRoot, pboPrefix, defineClass);
                    break;
                }
                case 5: {
                    MissionScripts ??= new DayZScriptDefineClass();
                    MissionScripts.AddScripts(pboRoot, pboPrefix, defineClass);
                    break;
                }
                default: break;
            }
        }
    }