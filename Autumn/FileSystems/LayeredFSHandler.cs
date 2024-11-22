using System.Collections.ObjectModel;
using Autumn.Storage;
using Silk.NET.OpenGL;

namespace Autumn.FileSystems;

/// <summary>
/// A class that simulates a romfs in layers.<br/>
/// When reading, if a file exists in more than one filesystem,
/// the file from the filesystem that comes first in the list will be read.<br/>
/// When writing, the file will be written to the first filesystem in the list.
/// </summary>
internal class LayeredFSHandler
{
    private readonly List<RomFSHandler> _romFSHandlers;

    /// <summary>
    /// Creates a new instance of <see cref="LayeredFSHandler"/> where the passed
    /// strings are used to create the RomFSHandlers.
    /// </summary>
    /// <param name="paths">The strings used to create the RomFSHandlers.</param>
    public LayeredFSHandler(params string[] paths)
    {
        _romFSHandlers = new(paths.Length);
        SetPaths(paths);
    }

    public Stage ReadStage(string name, byte scenario)
    {
        foreach (RomFSHandler romFSHandler in _romFSHandlers)
        {
            if (!romFSHandler.ExistsStage(name, scenario))
                continue;

            return romFSHandler.ReadStage(name, scenario);
        }

        return new();
    }

    public Actor ReadActor(string name, GL gl)
    {
        foreach (RomFSHandler romFSHandler in _romFSHandlers)
        {
            if (!romFSHandler.ExistsActor(name))
                continue;

            return romFSHandler.ReadActor(name, gl);
        }

        return new(name, gl);
    }

    public bool WriteStage(Stage stage)
    {
        if (_romFSHandlers.Count < 1)
            return false;

        return _romFSHandlers[0].WriteStage(stage);
    }

    public ReadOnlyDictionary<string, string> ReadCreatorClassNameTable()
    {
        foreach (RomFSHandler romFSHandler in _romFSHandlers)
        {
            if (!romFSHandler.ExistsCreatorClassNameTable())
                continue;

            return romFSHandler.ReadCreatorClassNameTable();
        }

        return new(new Dictionary<string, string>());
    }

    public IEnumerable<string> EnuemeratePaths()
    {
        foreach (RomFSHandler handler in _romFSHandlers)
            yield return handler.Root;
    }

    public string[] GetPaths()
    {
        string[] result = new string[_romFSHandlers.Count];

        for (int i = 0; i < _romFSHandlers.Count; i++)
            result[i] = _romFSHandlers[i].Root;

        return result;
    }

    public void SetPaths(params string[] paths)
    {
        _romFSHandlers.Clear();

        foreach (string path in paths)
        {
            RomFSHandler handler = new(path);
            _romFSHandlers.Add(handler);
        }
    }
}
