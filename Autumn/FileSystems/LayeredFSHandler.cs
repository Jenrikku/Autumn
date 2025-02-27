using System.Collections.ObjectModel;
using Autumn.Background;
using Autumn.Storage;
using NARCSharp;

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

    public Actor ReadActor(string name, GLTaskScheduler scheduler)
    {
        if (ModFS is not null && ModFS.ExistsActor(name))
            return ModFS.ReadActor(name, scheduler);
        else if (OriginalFS is not null && OriginalFS.ExistsActor(name))
            return OriginalFS.ReadActor(name, scheduler);

        return new(name);
    }

    public Actor ReadActor(string name, string? fallback, GLTaskScheduler scheduler)
    {
        if (fallback is null)
            return ReadActor(name, scheduler);

        var hasMod = ModFS != null;
        var hasOg = OriginalFS != null;
        if (hasMod)
        {
            if (!ModFS.ExistsActor(name))
            {
                if (hasOg && OriginalFS.ExistsActor(name))
                    return OriginalFS.ReadActor(name, scheduler);
                else
                {
                    if (ModFS.ExistsActor(fallback))
                        return ModFS.ReadActor(fallback, scheduler);
                    else if (hasOg && OriginalFS.ExistsActor(fallback))
                        return OriginalFS.ReadActor(fallback, scheduler);
                }
            }
            else
                return ModFS.ReadActor(name, scheduler);
        }

        if (hasOg)
        {
            if (!OriginalFS.ExistsActor(name))
            {
                if (OriginalFS.ExistsActor(fallback))
                    return OriginalFS.ReadActor(fallback, scheduler);
            }
            else
                return OriginalFS.ReadActor(name, scheduler);
        }

        return new(name);
    }
    public Actor ReadActor(string name, string? dbArchive, string? fallback, GLTaskScheduler scheduler)
    {
        if (dbArchive == null && fallback == null) return ReadActor(name, scheduler);
        else if (dbArchive == null) return ReadActor(name, fallback, scheduler);
        else if (fallback == null) return ReadActor(name, dbArchive, scheduler);


        var hasMod = ModFS != null;
        var hasOg = OriginalFS != null;

        if (hasMod)
        {
            if (!ModFS.ExistsActor(dbArchive))
            {
                if (hasOg && OriginalFS.ExistsActor(dbArchive))
                    return OriginalFS.ReadActor(dbArchive, scheduler);
                else
                {
                    if (!ModFS.ExistsActor(name))
                    {
                        if (hasOg && OriginalFS.ExistsActor(name))
                            return OriginalFS.ReadActor(name, scheduler);
                        else
                        {
                            if (!ModFS.ExistsActor(fallback))
                            {
                                if (hasOg && OriginalFS.ExistsActor(fallback))
                                    return OriginalFS.ReadActor(fallback, scheduler);
                            }
                            else
                                return ModFS.ReadActor(fallback, scheduler);
                        }
                    }
                    else
                        return ModFS.ReadActor(name, scheduler);
                }
            }
            else
                return ModFS.ReadActor(dbArchive, scheduler);
        }
        if (hasOg)
        {
            if (!OriginalFS!.ExistsActor(dbArchive))
            {
                if (!OriginalFS.ExistsActor(name))
                {
                    if (OriginalFS.ExistsActor(fallback))
                        return OriginalFS.ReadActor(fallback, scheduler);
                }
                else
                    return OriginalFS.ReadActor(name, scheduler);
            }
            else
                return OriginalFS.ReadActor(dbArchive, scheduler);
        }
        return new(name);
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

    public ReadOnlyDictionary<string,string> TryReadCCNT(string path)
    {
        return RomFSHandler.ReadAnyCreatorClassNameTable(path);
    }
}
