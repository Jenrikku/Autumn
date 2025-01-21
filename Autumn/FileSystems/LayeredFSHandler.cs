using System.Collections.ObjectModel;
using Autumn.Background;
using Autumn.Storage;

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
        if (ModFS != null && ModFS.ExistsStage(name, scenario))
            return ModFS.ReadStage(name, scenario);
        else if (OriginalFS != null && OriginalFS.ExistsStage(name, scenario))
            return OriginalFS.ReadStage(name, scenario);
        return new();
    }

    public Actor ReadActor(string name, GLTaskScheduler scheduler)
    {
        if (ModFS != null && ModFS.ExistsActor(name))
            return ModFS.ReadActor(name, scheduler);
        else if (OriginalFS != null && OriginalFS.ExistsActor(name))
            return OriginalFS.ReadActor(name, scheduler);
        return new(name);
    }

    public Actor ReadActor(string name, string? fallback, GLTaskScheduler scheduler)
    {
        if (fallback == null) return ReadActor(name, scheduler);

        if (ModFS != null)
        {
            if (!ModFS.ExistsActor(name))
            {
                if (ModFS.ExistsActor(fallback))
                    return ModFS.ReadActor(fallback, scheduler);
            }
            else
                return ModFS.ReadActor(name, scheduler);
        }
        if (OriginalFS != null)
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

    public bool WriteStage(Stage stage)
    {
        if (ModFS == null) return false;
        return ModFS.WriteStage(stage);
    }

    public ReadOnlyDictionary<string, string> ReadCreatorClassNameTable()
    {
        if (ModFS != null && ModFS.ExistsCreatorClassNameTable())
            return ModFS.ReadCreatorClassNameTable();
        else if (OriginalFS != null && OriginalFS.ExistsCreatorClassNameTable())
            return OriginalFS.ReadCreatorClassNameTable();
        return new(new Dictionary<string, string>());
    }

    public IEnumerable<string> EnumeratePaths()
    {
        if (ModFS != null)
            yield return ModFS.Root;
        if (OriginalFS != null)
            yield return OriginalFS.Root;
    }
}
