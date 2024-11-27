using Autumn.Enums;

namespace Autumn.Storage;

internal class StageFile
{
    public StageFileType StageFileType { get; }

    private readonly List<RailObj> _railInfo = new();

    private readonly List<StageObj> _objInfo = new();
    private readonly List<StageObj> _areaObjInfo = new();
    private readonly List<StageObj> _cameraAreaInfo = new();
    private readonly List<StageObj> _goalObjInfo = new();
    private readonly List<StageObj> _startEventObjInfo = new();
    private readonly List<StageObj> _startInfo = new();
    private readonly List<StageObj> _demoSceneObjInfo = new();

    private readonly Dictionary<string, byte[]> _additionalFiles = new();

    public StageFile(StageFileType type)
    {
        StageFileType = type;
    }

    public void AddStageObj(StageObj stageObj)
    {
        if (stageObj is RailObj railObj)
        {
            _railInfo.Add(railObj);
            return;
        }

        List<StageObj> list = stageObj.Type switch
        {
            StageObjType.Regular => _objInfo,
            StageObjType.Area => _areaObjInfo,
            StageObjType.CameraArea => _cameraAreaInfo,
            StageObjType.Goal => _goalObjInfo,
            StageObjType.StartEvent => _startEventObjInfo,
            StageObjType.Start => _startInfo,
            StageObjType.DemoScene => _demoSceneObjInfo,
            _ => throw new NotImplementedException("Incorrect object type read.")
        };

        list.Add(stageObj);
    }

    public void AddAdditionalFile(string name, byte[] contents)
    {
        if (!_additionalFiles.TryAdd(name, contents))
            throw new ArgumentException(
                "There already is an additional file with the name " + name
            );
    }

    public IEnumerable<StageObj> EnumerateStageObjs(StageObjType type)
    {
        List<StageObj> list = type switch
        {
            StageObjType.Regular => _objInfo,
            StageObjType.Area => _areaObjInfo,
            StageObjType.CameraArea => _cameraAreaInfo,
            StageObjType.Goal => _goalObjInfo,
            StageObjType.StartEvent => _startEventObjInfo,
            StageObjType.Start => _startInfo,
            StageObjType.DemoScene => _demoSceneObjInfo,
            _ => throw new NotImplementedException("Incorrect object type read.")
        };

        foreach (StageObj stageObj in list)
            yield return stageObj;
    }

    public IEnumerable<RailObj> EnumerateRails()
    {
        foreach (RailObj railObj in _railInfo)
            yield return railObj;
    }

    public IEnumerable<KeyValuePair<string, byte[]>> EnumerateAdditionalFiles()
    {
        foreach (var keyValue in _additionalFiles)
            yield return keyValue;
    }
}