using System.Collections;
using Autumn.Storage.StageObjs;

namespace Autumn.Storage;

internal struct Stage : IEnumerable<IStageObj>
{
    public Stage()
        : this("NewStage", 1) { }

    public Stage(string name, byte scenario = 1)
    {
        Name = name;
        Scenario = scenario;
    }

    public string Name { get; set; }
    public byte Scenario { get; set; }

    public List<AreaStageObj>? AreaStageObjs { get; set; }
    public List<CameraAreaStageObj>? CameraAreaStageObjs { get; set; }
    public List<GoalStageObj>? GoalStageObjs { get; set; }
    public List<RegularStageObj>? RegularStageObjs { get; set; }
    public List<StartEventStageObj>? StartEventStageObjs { get; set; }
    public List<StartStageObj>? StartStageObjs { get; set; }

    public List<string>? PreLoadFileList { get; set; }

    public Dictionary<string, object>? AreaIdToLightNameTable { get; set; }
    public Dictionary<string, object>? CameraParam { get; set; }
    public Dictionary<string, object>? FogParam { get; set; }
    public Dictionary<string, object>? LightParam { get; set; }
    public Dictionary<string, object>? ModelToMapLightNameTable { get; set; }
    public Dictionary<string, object>? StageInfo { get; set; }

    public void Add(IStageObj stageObj)
    {
        switch (stageObj)
        {
            case IStageObj obj when obj is AreaStageObj areaObj:
                AreaStageObjs ??= new();
                AreaStageObjs.Add(areaObj);
                break;

            case IStageObj obj when obj is CameraAreaStageObj cameraAreaObj:
                CameraAreaStageObjs ??= new();
                CameraAreaStageObjs.Add(cameraAreaObj);
                break;

            case IStageObj obj when obj is GoalStageObj goalObj:
                GoalStageObjs ??= new();
                GoalStageObjs.Add(goalObj);
                break;

            case IStageObj obj when obj is RegularStageObj regularObj:
                RegularStageObjs ??= new();
                RegularStageObjs.Add(regularObj);
                break;

            case IStageObj obj when obj is StartEventStageObj startEventObj:
                StartEventStageObjs ??= new();
                StartEventStageObjs.Add(startEventObj);
                break;

            case IStageObj obj when obj is StartStageObj startObj:
                StartStageObjs ??= new();
                StartStageObjs.Add(startObj);
                break;

            default:
                throw new ArgumentException("The given stage object is invalid.");
        }
    }

    public void AddRange(IEnumerable<IStageObj> stageObjs)
    {
        foreach (IStageObj obj in stageObjs)
            Add(obj);
    }

    public readonly IEnumerator<IStageObj> GetEnumerator()
    {
        if (AreaStageObjs is not null)
            foreach (AreaStageObj obj in AreaStageObjs)
                yield return obj;

        if (CameraAreaStageObjs is not null)
            foreach (CameraAreaStageObj obj in CameraAreaStageObjs)
                yield return obj;

        if (GoalStageObjs is not null)
            foreach (GoalStageObj obj in GoalStageObjs)
                yield return obj;

        if (RegularStageObjs is not null)
            foreach (RegularStageObj obj in RegularStageObjs)
                yield return obj;

        if (StartEventStageObjs is not null)
            foreach (StartEventStageObj obj in StartEventStageObjs)
                yield return obj;

        if (StartStageObjs is not null)
            foreach (StartStageObj obj in StartStageObjs)
                yield return obj;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
