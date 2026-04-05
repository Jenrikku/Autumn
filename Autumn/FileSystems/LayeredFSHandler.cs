using System.Collections.ObjectModel;
using System.Diagnostics;
using Autumn.Background;
using Autumn.Enums;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using Autumn.Wrappers;
using NARCSharp;
using SPICA.Formats.CtrGfx;
using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.LUT;
using SPICA.Formats.CtrH3D.Model;
using SPICA.Formats.CtrH3D.Model.Material;
using SPICA.Formats.CtrH3D.Model.Mesh;
using SPICA.Formats.CtrH3D.Texture;

namespace Autumn.FileSystems;

/// <summary>
/// A class that simulates a romfs in layers.<br/>
/// When getting specific files or variables through methods we try to
/// find them in the modified FS first, then in the original FS. <br/>
/// For specific filesystem checks we can just access <b> OriginalFS </b> or <b> ModFs </b>
/// </summary>
internal class LayeredFSHandler
{
    /// <summary>
    /// Original, unmodified romfs of the game, used to obtain base assets, but should not be written into.
    /// </summary>
    public RomFSHandler? OriginalFS;

    /// <summary>
    /// Modified romfs, where our project file will be found.
    /// All changes made will be saved here.
    /// </summary>
    public RomFSHandler? ModFS;

    /// <summary>
    /// Creates a new instance of <see cref="LayeredFSHandler"/> where the passed
    /// strings are used to create the RomFSHandlers.
    /// </summary>
    /// <param name="original">Path to the unmodified romfs.</param>
    /// <param name="modified">Path to the modified romfs.</param>
    public LayeredFSHandler(string? original, string? modified = null)
    {
        OriginalFS = string.IsNullOrEmpty(original) ? null : new(original);
        ModFS = string.IsNullOrEmpty(modified) ? null : new(modified);
    }

    public Stage ReadStage(string name, byte scenario)
    {
        if (ModFS is not null && ModFS.ExistsStage(name, scenario))
            return ModFS.ReadStage(name, scenario);
        else if (OriginalFS is not null && OriginalFS.ExistsStage(name, scenario))
            return OriginalFS.ReadStage(name, scenario);

        return new();
    }

    public void ReadActorExtras(string actorName, string className, ActorSceneObj actor, GLTaskScheduler scheduler)
    {
        if (ClassModifiersWrapper.ModifierEntries.ContainsKey(className))
        {
            ClassModifiersWrapper.ModifierEntry act;
            if (ClassModifiersWrapper.ModifierEntries[className].Variants != null && ClassModifiersWrapper.ModifierEntries[className].Variants!.ContainsKey(actorName))
            {
                act = ClassModifiersWrapper.ModifierEntries[className].Variants![actorName]!;
            }
            else if (ClassModifiersWrapper.ModifierEntries[className].Default != null)
            {
                act = ClassModifiersWrapper.ModifierEntries[className].Default!.Value;
            }
            else return;
            
            if (act.ExtraModels != null)
            {
                foreach (string s in act.ExtraModels!.Keys)
                {
                    string ex;
                    RomFSHandler? FS;
                    if (ModFS != null && File.Exists(Path.Join(ModFS.GetPath(FSPath.Actors), s + ".szs")))
                    {
                        FS = ModFS;
                    }
                    else if (OriginalFS != null && File.Exists(Path.Join(OriginalFS.GetPath(FSPath.Actors), s + ".szs")))
                    {
                        FS = OriginalFS;
                    }
                    else continue;
                    ex = Path.Join(FS.GetPath(FSPath.Actors), s + ".szs");
                    if(FS.CachedActors.ContainsKey(s))
                    {
                        actor.SubActors.Add(FS.CachedActors[s]);
                        continue;
                    }
                    FS.ReadActorExtras(s, ex, actor, scheduler);
                }
            }
        }
    }
    public Actor? ReadActorExtrasArg(string subActorName, GLTaskScheduler scheduler)
    {
        string ex;
        RomFSHandler? FS;
        if (ModFS != null && File.Exists(Path.Join(ModFS.GetPath(FSPath.Actors), subActorName + ".szs")))
        {
            FS = ModFS;
        }
        else if (OriginalFS != null && File.Exists(Path.Join(OriginalFS.GetPath(FSPath.Actors), subActorName + ".szs")))
        {
            FS = OriginalFS;
        }
        else return null;
        ex = Path.Join(FS.GetPath(FSPath.Actors), subActorName + ".szs");
        if (FS.CachedActors.ContainsKey(subActorName))
            return FS.CachedActors[subActorName];
        return FS.ReadActorExtrasArg(subActorName, ex, scheduler);
    }

    public Actor ReadActor(string name, GLTaskScheduler scheduler)
    {
        if (ModFS is not null && ModFS.ExistsActor(name))
            return ModFS.ReadActor(name, scheduler);
        else if (OriginalFS is not null && OriginalFS.ExistsActor(name))
            return OriginalFS.ReadActor(name, scheduler);

        return new(name);
    }

    public Actor ReadActorNew(string actorName, string baseModelName, string actorClass, GLTaskScheduler scheduler)
    {
        if (ModFS is not null && ModFS.ExistsActor(baseModelName))
            return ModFS.ReadActorNew(actorName, baseModelName, actorClass, scheduler);
        else if (OriginalFS is not null && OriginalFS.ExistsActor(baseModelName))
            return OriginalFS.ReadActorNew(actorName, baseModelName, actorClass, scheduler);

        return new(actorName);
    }
    public Actor ReadActorNew(string actorName, string? className, GLTaskScheduler scheduler)
    {
        if (className == null) return ReadActor(actorName, scheduler);

        var hasMod = ModFS != null;
        var hasOg = OriginalFS != null;

        bool replacementVariants = ClassModifiersWrapper.ModifierEntries.ContainsKey(className) && ClassModifiersWrapper.ModifierEntries[className].Variants != null && ClassModifiersWrapper.ModifierEntries[className].Variants!.ContainsKey(actorName);
        bool replacementDefault = ClassModifiersWrapper.ModifierEntries.ContainsKey(className) && ClassModifiersWrapper.ModifierEntries[className].Default != null;
        bool replacementDatabase = ClassDatabaseWrapper.DatabaseEntries.ContainsKey(className) && ClassDatabaseWrapper.DatabaseEntries[className].ArchiveName != null;

        if (replacementVariants) // if we specify stuff for this particular type of the actor
        {
            if (ClassModifiersWrapper.ModifierEntries[className].Variants![actorName]!.ModelReplace != null) 
            {
                return ReadActorNew(actorName, ClassModifiersWrapper.ModifierEntries[className].Variants![actorName]!.ModelReplace!, className, scheduler);
            }
        }
        if (replacementDefault) // if we don't specify stuff for this particular case, and we have a default 
        {
            if (ClassModifiersWrapper.ModifierEntries[className].Default!.Value.ModelReplace != null) 
            {
                return ReadActorNew(actorName, ClassModifiersWrapper.ModifierEntries[className].Default!.Value.ModelReplace!, className, scheduler);
            }
        }

        string dbArchiveReplacement = replacementDatabase ? ClassDatabaseWrapper.DatabaseEntries[className].ArchiveName! : "";

        if (hasMod)
        {
            if (!ModFS.ExistsActor(dbArchiveReplacement))
            {
                if (hasOg && OriginalFS.ExistsActor(dbArchiveReplacement))
                    return OriginalFS.ReadActorNew(actorName, dbArchiveReplacement, className, scheduler);
                else
                {
                    if (!ModFS.ExistsActor(actorName))
                    {
                        if (hasOg && OriginalFS.ExistsActor(actorName))
                            return OriginalFS.ReadActorNew(actorName, actorName, className, scheduler);
                        else
                        {
                            if (!ModFS.ExistsActor(className))
                            {
                                if (hasOg && OriginalFS.ExistsActor(className))
                                    return OriginalFS.ReadActorNew(actorName, className, className, scheduler);
                            }
                            else
                                return ModFS.ReadActorNew(actorName, className, className, scheduler);
                        }
                    }
                    else
                        return ModFS.ReadActorNew(actorName, actorName, className, scheduler);
                }
            }
            else
                return ModFS.ReadActorNew(actorName, dbArchiveReplacement, className, scheduler);
        }
        if (hasOg)
        {
            if (!OriginalFS!.ExistsActor(dbArchiveReplacement))
            {
                if (!OriginalFS.ExistsActor(actorName))
                {
                    if (OriginalFS.ExistsActor(className))
                        return OriginalFS.ReadActorNew(actorName, className, className, scheduler);
                }
                else
                    return OriginalFS.ReadActorNew(actorName, actorName, className, scheduler);
            }
            else
                return OriginalFS.ReadActorNew(actorName, dbArchiveReplacement, className, scheduler);
        }
        return new(actorName);
    }

    public bool WriteStage(Stage stage, bool _useClassNames)
    {
        if (ModFS == null) return false;
        var r = ModFS.WriteStage(stage, _useClassNames);
        if ((r && stage.DefaultBgm is not null && ReadBgmTable().StageDefaultBgmList.Where(x => x.Scenario == stage.DefaultBgm.Scenario && x.StageName == stage.DefaultBgm.StageName && x.BgmLabel != stage.DefaultBgm.BgmLabel).Any())
            || stage.RebuildMusicAreas)
        {
            // Overwrite BgmTable in ModFS with the OriginaFS BgmTable modified with this stage's new song.
            ModFS.WriteBgmTable(ReadBgmTable(), stage);
            stage.RebuildMusicAreas = false;
        }
        return r;
    }

    public ReadOnlyDictionary<string, string> ReadCreatorClassNameTable()
    {
        if (ModFS is not null && ModFS.ExistsCreatorClassNameTable())
            return ModFS.ReadCreatorClassNameTable();
        else if (OriginalFS is not null && OriginalFS.ExistsCreatorClassNameTable())
            return OriginalFS.ReadCreatorClassNameTable();

        return new(new Dictionary<string, string>());
    }
    public bool WriteCreatorClassNameTable(Dictionary<string, string> ccnt)
    {
        if (ModFS is not null)
            return ModFS.WriteCCNT(ccnt);

        return true;
    }

    public BgmTable? ReadBgmTable()
    {
        if (ModFS is not null && ModFS.ExistsBgmTable())
            return ModFS.ReadBgmTable();
        else if (OriginalFS is not null && OriginalFS.ExistsBgmTable())
            return OriginalFS.ReadBgmTable();

        return null;
    }

    public SystemDataTable? ReadGameSystemDataTable()
    {
        if (ModFS is not null && ModFS.ExistsGSDT())
            return ModFS.ReadGSDTable();
        else if (OriginalFS is not null && OriginalFS.ExistsGSDT())
            return OriginalFS.ReadGSDTable();

        return null;
    }
    public Dictionary<string, LightArea>? ReadLightAreas()
    {
        if (ModFS != null && ModFS.ExistsLightDataArea())
            return ModFS.GetPossibleLightAreas();
        else if (OriginalFS != null && OriginalFS.ExistsLightDataArea())
            return OriginalFS.GetPossibleLightAreas();
        return null;
    }
    public NARCFileSystem? ReadShaders()
    {
        if (ModFS != null && ModFS.ExistsShaders())
            return ModFS.GetShader();
        else if (OriginalFS != null && OriginalFS.ExistsShaders())
            return OriginalFS.GetShader();
        return null;
    }

    public IEnumerable<string> EnumeratePaths()
    {
        if (ModFS is not null)
            yield return ModFS.Root;
        if (OriginalFS is not null)
            yield return OriginalFS.Root;
    }

    public ReadOnlyDictionary<string, string> TryReadCCNT(string path)
    {
        return RomFSHandler.ReadAnyCreatorClassNameTable(path);
    }
    public Stage? TryReadStage(string path)
    {
        if (ModFS == null || !File.Exists(path))
            return null;
        return ModFS.TryReadStage(path);
    }
}
