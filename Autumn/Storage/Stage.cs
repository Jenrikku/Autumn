using Autumn.Enums;

namespace Autumn.Storage;

internal class Stage
{
    public string Name { get; set; } = "NewStage";
    public byte Scenario { get; set; } = 1;

    private StageFile _design = new(StageFileType.Design);
    private StageFile _map = new(StageFileType.Map);
    private StageFile _sound = new(StageFileType.Sound);

    /// <summary>
    /// Creates a new stage.
    /// </summary>
    /// <param name="initialize">If it is set to true then the stage will be filled with default objects.</param>
    public Stage(bool initialize = true)
    {
        if (!initialize)
            return;

        StageObj mario =
            new()
            {
                Name = "Mario",
                Type = StageObjType.Start,
                FileType = StageFileType.Map
            };

        mario.Properties.Add("MarioNo", 0);

        _map.AddStageObj(mario);
    }

    public void AddStageObj(StageObj stageObj)
    {
        StageFile stageFile = stageObj.FileType switch
        {
            StageFileType.Design => _design,
            StageFileType.Map => _map,
            StageFileType.Sound => _sound,
            _ => throw new NotImplementedException("Unknown stage file.")
        };

        stageFile.AddStageObj(stageObj);
    }
    public void RemoveStageObj(StageObj stageObj)
    {
        StageFile stageFile = stageObj.FileType switch
        {
            StageFileType.Design => _design,
            StageFileType.Map => _map,
            StageFileType.Sound => _sound,
            _ => throw new NotImplementedException("Unknown stage file.")
        };

        stageFile.RemoveStageObj(stageObj);
    }

    public void AddStageObjs(IEnumerable<StageObj> stageObjs)
    {
        foreach (StageObj stageObj in stageObjs)
            AddStageObj(stageObj);
    }

    public void AddAdditionalFile(StageFileType fileType, string name, byte[] contents)
    {
        StageFile stageFile = fileType switch
        {
            StageFileType.Design => _design,
            StageFileType.Map => _map,
            StageFileType.Sound => _sound,
            _ => throw new NotImplementedException("Unknown stage file.")
        };

        stageFile.AddAdditionalFile(name, contents);
    }

    public IEnumerable<StageObj> EnumerateStageObjs(StageObjType type)
    {
        foreach (StageObj stageObj in _design.EnumerateStageObjs(type))
            yield return stageObj;

        foreach (StageObj stageObj in _map.EnumerateStageObjs(type))
            yield return stageObj;

        foreach (StageObj stageObj in _sound.EnumerateStageObjs(type))
            yield return stageObj;
    }

    public IEnumerable<StageObj> EnumerateStageObjs(StageObjType type, StageFileType fileType)
    {
        StageFile stageFile = fileType switch
        {
            StageFileType.Design => _design,
            StageFileType.Map => _map,
            StageFileType.Sound => _sound,
            _ => throw new NotImplementedException("Unknown stage file.")
        };

        return stageFile.EnumerateStageObjs(type);
    }

    public IEnumerable<RailObj> EnumerateRails(StageFileType fileType)
    {
        StageFile stageFile = fileType switch
        {
            StageFileType.Design => _design,
            StageFileType.Map => _map,
            StageFileType.Sound => _sound,
            _ => throw new NotImplementedException("Unknown stage file.")
        };

        return stageFile.EnumerateRails();
    }

    public IEnumerable<KeyValuePair<string, byte[]>> EnumerateAdditionalFiles(StageFileType type)
    {
        StageFile stageFile = type switch
        {
            StageFileType.Design => _design,
            StageFileType.Map => _map,
            StageFileType.Sound => _sound,
            _ => throw new NotImplementedException("Unknown stage file.")
        };

        return stageFile.EnumerateAdditionalFiles();
    }
}
