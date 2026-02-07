using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Autumn.Background;
using Autumn.Enums;
using Autumn.FileSystems;
using Autumn.History;
using Autumn.Rendering.Area;
using Autumn.Rendering.Rail;
using Autumn.Rendering.Storage;
using Autumn.Storage;
using Autumn.Wrappers;
using Silk.NET.OpenGL;

namespace Autumn.Rendering;

internal class Scene
{
    public Stage Stage { get; }

    public bool IsSaved { get; set; } = false;
    public uint SaveUndoCount { get; set; } = 0;

    public ChangeHistory History { get; } = new();

    private readonly List<ISceneObj> _selectedObjects = new();
    public IEnumerable<ISceneObj> SelectedObjects => _selectedObjects;

    public ISceneObj? HoveringObject { get; private set; }

    public Camera Camera { get; } = new(new Vector3(-10, 7, 10), Vector3.Zero);
    public Camera PreviewCamera { get; } = new(new Vector3(-10, 7, 10), Vector3.Zero);
    public bool CanPreviewLights {get; set;} = false;
    public bool PreviewOneLight {get; set;} = false;
    public bool UseLightArea {get; set;} = false;
    public StageLight? PreviewLight { get; set; } = null;
    public LightArea? PreviewLightAreas { get; set; } = null;
    public int SelectedCam { get; set; } = -1;

    /// <summary>
    /// Specifies whether the scene is ready to be rendered.
    /// </summary>
    public bool IsReady { get; set; } = false;

    private readonly List<IStageSceneObj> _stageSceneObjs = new();
    private readonly List<RailSceneObj> _railObjs = new();

    private uint _lastPickingId = 0;
    private readonly Dictionary<uint, ISceneObj> _pickableObjs = new();

    private Dictionary<int, List<IStageSceneObj>> _stageSwitches = new();
    private bool _rebuildSwitchCount = true;
    private Dictionary<int, int> _stageSwitchCount = new();

    public StageFog MainStageFog { get; private set; } = new();
    private Dictionary<StageFog, List<ISceneObj>> _stageFogList = new();
    private Dictionary<StageFog, int> _stageFogCount = new();

    public Scene(Stage stage, LayeredFSHandler fsHandler, GLTaskScheduler scheduler, ref string status)
    {
        Stage = stage;
        GenerateSceneObjects(fsHandler, scheduler, ref status);
        scheduler.EnqueueGLTask(gl => IsReady = true);
        GenerateFog();
    }

    public void Render(GL gl, in Matrix4x4 view, in Matrix4x4 projection, in Quaternion cameraRot, in Vector3 cameraEye)
    {
        ModelRenderer.UpdateSceneParams(view, projection, cameraRot, cameraEye);

        foreach (ISceneObj obj in EnumerateSceneObjs())
            ModelRenderer.Draw(gl, obj, this);
    }

    #region Object selection

    public bool IsObjectSelected(uint id)
    {
        TryGetPickableObj(id, out ISceneObj? sceneObj);
        return sceneObj?.Selected ?? false;
    }

    public void SetObjectSelected(uint id, bool value)
    {
        if (!TryGetPickableObj(id, out ISceneObj? sceneObj))
            return;

        sceneObj.Selected = value;

        if (sceneObj.Selected)
        {
            if (sceneObj is IStageSceneObj stageSceneObj && stageSceneObj.StageObj.CameraId > -1) 
            {
                var camType = CameraParams.GetObjectCategory(stageSceneObj.StageObj);
                var sl = Stage.CameraParams.GetCamera(stageSceneObj.StageObj.CameraId, camType);
                if (sl == null) SelectedCam = -1;
                else SelectedCam = Stage.CameraParams.Cameras.IndexOf(sl);
            }
            _selectedObjects.Add(sceneObj);
        }
        else
            _selectedObjects.Remove(sceneObj);
    }

    public void UnselectAllObjects()
    {
        foreach (ISceneObj sceneObj in _selectedObjects)
            sceneObj.Selected = false;

        _selectedObjects.Clear();
    }

    public void SetSelectedObjects(IEnumerable<ISceneObj> objs)
    {
        UnselectAllObjects();

        foreach (ISceneObj sceneObj in objs)
        {
            sceneObj.Selected = true;
            _selectedObjects.Add(sceneObj);
        }
    }

    public IEnumerable<ISceneObj> EnumerateSceneObjs()
    {
        foreach (IStageSceneObj obj in _stageSceneObjs)
            yield return obj;

        foreach (RailSceneObj obj in _railObjs)
            yield return obj;
    }

    public IEnumerable<IStageSceneObj> EnumerateStageSceneObjs()
    {
        foreach (IStageSceneObj obj in _stageSceneObjs)
            yield return obj;
    }

    public IEnumerable<RailSceneObj> EnumerateRailSceneObjs()
    {
        foreach (RailSceneObj obj in _railObjs)
            yield return obj;
    }

    public int CountSceneObjs()
    {
        return _stageSceneObjs.Count + _railObjs.Count * 4; // Rail point Handles
    }
    public uint GetNextRailId()
    {
        var r = SelectedObjects.FirstOrDefault(x=> x is RailSceneObj);
        if (r == null) return 0;
        return _railObjs[(_railObjs.IndexOf(r as RailSceneObj) + 1) < _railObjs.Count ? _railObjs.IndexOf(r as RailSceneObj) + 1 : _railObjs.Count-1].PickingId;
    }
    public uint GetPreviousRailId()
    {
        var r = SelectedObjects.FirstOrDefault(x=> x is RailSceneObj);
        if (r == null) return 0;
        return _railObjs[(_railObjs.IndexOf(r as RailSceneObj) - 1) > -1 ? _railObjs.IndexOf(r as RailSceneObj) - 1 : 0].PickingId;
    }
    public RailSceneObj? GetRailSceneObj(RailObj rail)
    {
        return _railObjs.FirstOrDefault(x => x.RailObj == rail);
    }

    #endregion

    #region Object hovering

    public void SetHoveringObject(uint id)
    {
        if (HoveringObject is not null && HoveringObject.PickingId == id) return;

        if (TryGetPickableObj(id, out ISceneObj? sceneObj))
            sceneObj.Hovering = true;

        if (HoveringObject is not null) HoveringObject.Hovering = false;

        HoveringObject = sceneObj;
    }

    #endregion

    #region Params

    public StageLight? GetPreviewLight(ActorLight.LightType t)
    {
        if (!UseLightArea)
        {
            if (Stage.LightParams == null) return null;
            return t switch
            {
                ActorLight.LightType.Player => Stage.LightParams.PlayerLight,
                ActorLight.LightType.Character => Stage.LightParams.ObjectLight,
                ActorLight.LightType.Terrain_Object => Stage.LightParams.MapObjectLight,
                _ => Stage.LightParams.StageMapLight,
            };
        }
        else
        {
            if (PreviewLightAreas == null) return null;
            return t switch
            {
                ActorLight.LightType.Player => PreviewLightAreas.PlayerLight,
                ActorLight.LightType.Character => PreviewLightAreas.ObjectLight,
                ActorLight.LightType.Terrain_Object => PreviewLightAreas.MapObjectLight,
                _ => Stage.LightParams != null ? Stage.LightParams.StageMapLight : null,
            };
        }
    }

    public void AddLight()
    {
        int _i = 0;
        for (int i = 0; i < 9999; i++)
        {
            if (!Stage.LightAreaNames.Any(x => x.Key == i))
            {
                _i = i;
                break;
            }
        }
        Stage.LightAreaNames.Add(_i, "[汎用]デフォルトライト");
    }
    public void AddFog(StageFog fog)
    {
        for (int i = 0; i < 9999; i++)
        {
            if (!Stage.StageFogs.Any(x => x.AreaId == i))
            {
                fog.AreaId = i;
                break;
            }
        }
        Stage.StageFogs.Add(fog);
        _stageFogList.Add(fog, new());
    }
    public void DuplicateFog(int f)
    {
        var fog = new StageFog(Stage.StageFogs[f]);
        for (int i = 0; i < 9999; i++)
        {
            if (!Stage.StageFogs.Where(x => x.AreaId == i).Any())
            {
                fog.AreaId = i;
                break;
            }
        }
        Stage.StageFogs.Add(fog);
        _stageFogList.Add(fog, new());
    }
    public void RemoveFogAt(int i)
    {
        if (Stage.StageFogs.Count < i || i < 0) return;
        _stageFogList.Remove(Stage.StageFogs[i]);
        Stage.StageFogs.RemoveAt(i);
    }
    public IEnumerable<int> GetFogs()
    {
        yield return 1; // Main fog
        foreach (StageFog k in _stageFogList.Keys)
            yield return _stageFogList[k].Count;
    }
    public int GetFogCount(int i)
    {
        if (i == 0) return 1; //Main stage fog 
        return _stageFogList.Values.ToList()[i - 1].Count;
    }
    public int CountFogs()
    {
        return Stage.StageFogs.Count;
    }
    internal void UpdateFog(int selectedfog, int oldAreaId)
    {
        if (_stageFogList.Keys.Count(x => x.AreaId == Stage.StageFogs[selectedfog].AreaId) > 1)
        {
            Stage.StageFogs[selectedfog].AreaId = oldAreaId;
            return;
        }
        _stageFogList.Remove(Stage.StageFogs[selectedfog]);
        var fogAreas = EnumerateStageSceneObjs().Where(x => x.StageObj.Name.Contains("FogArea") && x.StageObj.FileType == StageFileType.Design);
        if (!_stageFogList.ContainsKey(Stage.StageFogs[selectedfog]))
            _stageFogList.Add(Stage.StageFogs[selectedfog], new());
        foreach (IStageSceneObj sobj in fogAreas.Cast<IStageSceneObj>())
        {
            if (!sobj.StageObj.Properties.ContainsKey("Arg0") || sobj.StageObj.Properties["Arg0"]?.GetType() != typeof(int) || (int)sobj.StageObj.Properties["Arg0"]! != Stage.StageFogs[selectedfog].AreaId)
                continue;

            _stageFogList[Stage.StageFogs[selectedfog]].Add(sobj);
        }
    }

    public Dictionary<int, int> GetSwitches()
    {
        if (_rebuildSwitchCount)
        {
            foreach (int k in _stageSwitches.Keys)
            {
                if (_stageSwitchCount.ContainsKey(k) && _stageSwitchCount[k] == _stageSwitches[k].Count)
                    continue;
                if (!_stageSwitchCount.ContainsKey(k))
                    _stageSwitchCount.Add(k, _stageSwitches[k].Count);
                else
                    _stageSwitchCount[k] = _stageSwitches[k].Count;
            }
            _rebuildSwitchCount = false;
        }
        return _stageSwitchCount;
    }
    public List<IStageSceneObj>? GetObjectsFromSwitch(int i)
    {
        if (_stageSwitches.ContainsKey(i))
            return _stageSwitches[i];
        return null;
    }
    public void AddSwitch(int i, IStageSceneObj sO)
    {
        if (_stageSwitches.ContainsKey(i))
            _stageSwitches[i].Add(sO);
        else
            _stageSwitches.Add(i, [sO]);
        _rebuildSwitchCount = true;
    }
    public void AddSwitchFromStageObj(StageObj stO, IStageSceneObj scO)
    {
        if (stO.SwitchA > -1) AddSwitch(stO.SwitchA, scO);
        if (stO.SwitchAppear > -1) AddSwitch(stO.SwitchAppear, scO);
        if (stO.SwitchB > -1) AddSwitch(stO.SwitchB, scO);
        if (stO.SwitchKill > -1) AddSwitch(stO.SwitchKill, scO);
        if (stO.SwitchDeadOn > -1) AddSwitch(stO.SwitchDeadOn, scO);
    }
    public void RemoveSwitchFromStageObj(StageObj stO, IStageSceneObj scO)
    {
        if (stO.SwitchA > -1) ChangeSwitch(-1, stO.SwitchA,  scO);
        if (stO.SwitchAppear > -1) ChangeSwitch(-1, stO.SwitchAppear, scO);
        if (stO.SwitchB > -1) ChangeSwitch(-1, stO.SwitchB, scO);
        if (stO.SwitchKill > -1) ChangeSwitch(-1, stO.SwitchKill, scO);
        if (stO.SwitchDeadOn > -1) ChangeSwitch(-1, stO.SwitchDeadOn, scO);
    }
    public void ChangeSwitch(int next, int prev, IStageSceneObj sO)
    {
        if (prev > -1 && _stageSwitches.ContainsKey(prev) && _stageSwitches[prev].Contains(sO))
        {
            _stageSwitches[prev].Remove(sO);
            if (_stageSwitches[prev].Count == 0)
            {
                RemoveSwitch(prev);
            }
        }
        if (next > -1)
            AddSwitch(next, sO);
    }
    public void RemoveSwitch(int i)
    {
        _stageSwitches.Remove(i);
        _stageSwitchCount.Remove(i);
        _rebuildSwitchCount = true;
    }

    #endregion

    public void AddObject(StageObj stageObj, LayeredFSHandler fsHandler, GLTaskScheduler scheduler)
    {
        Stage.AddStageObj(stageObj);
        GenerateSceneObject(stageObj, fsHandler, scheduler);
    }

    public void ReAddObject(StageObj stageObj, LayeredFSHandler fsHandler, GLTaskScheduler scheduler)
    {
        // There is no need to check for rails as we already use stageObj Debug.Assert(stageObj is not null); // We need to handle object readding for rails and othet objects. See ChangeHandler.ChangeRemove

        if (stageObj.Parent != null)
        {
            StageObj parent = stageObj.Parent;
            // Find the parent on the stage file
            StageObj? newParent = Stage.GetStageFile(stageObj.FileType).GetObjInfos(stageObj.Parent.Type).FirstOrDefault(x => StageObj.Compare(x, stageObj.Parent));
            if (newParent != null) Stage.GetStageFile(stageObj.FileType).SetChild(stageObj, newParent);
            else stageObj.Parent = null;
    
        }
        else
            Stage.AddStageObj(stageObj);
        if (stageObj.Children != null)
        {
            var cnt = new List<StageObj>(stageObj.Children);
            stageObj.Children.Clear();
            foreach (var ch in cnt)
            {
                StageObjType chtype = ch.Type == StageObjType.Child ? StageObjType.Regular : StageObjType.Area;
                // Find the children on the stage file
                StageObj? nch = Stage.GetStageFile(stageObj.FileType).GetObjInfos(chtype).FirstOrDefault(x => StageObj.Compare(x, ch));
                if (nch != null) Stage.GetStageFile(stageObj.FileType).SetChild(nch, stageObj);
            }
        }
        GenerateSceneObject(stageObj, fsHandler, scheduler);
    }

    public uint DuplicateObj(StageObj clonedObj, LayeredFSHandler fsHandler, GLTaskScheduler scheduler)
    {
        if (clonedObj.Type == StageObjType.Child || clonedObj.Type == StageObjType.AreaChild)
        {
            clonedObj.Parent?.Children?.Add(clonedObj);
            return DuplicateChild(clonedObj, fsHandler, scheduler);
        }

        Stage.AddStageObj(clonedObj);
        GenerateSceneObject(clonedObj, fsHandler, scheduler);

        uint pickingId = _lastPickingId - 1;

        if (clonedObj.Children != null && clonedObj.Children.Count > 0)
        {
            foreach (StageObj ch in clonedObj.Children)
            {
                pickingId = DuplicateChild(ch, fsHandler, scheduler);
            }
        }

        return pickingId;
    }

    public uint DuplicateChild(StageObj clonedObj, LayeredFSHandler fsHandler, GLTaskScheduler scheduler)
    {
        GenerateSceneObject(clonedObj, fsHandler, scheduler);
        uint pickingId = _lastPickingId - 1;
        if (clonedObj.Children != null && clonedObj.Children.Count > 0)
        {
            foreach (StageObj ch in clonedObj.Children)
            {
                pickingId = DuplicateChild(ch, fsHandler, scheduler);
            }
        }
        return pickingId;
    }

    public void RemoveObject(ISceneObj sceneObj)
    {
        if (sceneObj is IStageSceneObj stageSceneObj)
        {
            RemoveSwitchFromStageObj(stageSceneObj.StageObj, stageSceneObj);
            Stage.RemoveStageObj(stageSceneObj.StageObj);
        }
        else if (sceneObj is RailSceneObj railSceneObj)
        {
            Stage.RemoveStageObj(railSceneObj.RailObj);
        }
        DestroySceneObject(sceneObj);
    }

    public void ResetCamera()
    {
        // Find the first Mario and set the camera to its position.
        StageObj? mario = _stageSceneObjs
            .Find(
                sceneObj =>
                    sceneObj.StageObj.Type == StageObjType.Start
                    && sceneObj.StageObj.Properties.TryGetValue("MarioNo", out object? marioNoObj)
                    && marioNoObj is int marioNo
                    && marioNo == 0
            )
            ?.StageObj;

        if (mario is null)
        {
            Camera.LookAt(new Vector3(-10, 7, 10), Vector3.Zero);
            return;
        }

        float rotX = 0;
        float rotY = (float)(mario.Rotation.Y * (Math.PI / 180f)); // Horizontal rotation
        float rotZ = (float)(25 * (Math.PI / 180f)); // Vertical rotation

        Quaternion rotation = Quaternion.CreateFromYawPitchRoll(rotY, rotZ, rotX);
        rotation = Quaternion.Normalize(rotation);

        Vector3 cameraDistance = Vector3.Transform(Vector3.UnitZ, rotation) * 13;
        Vector3 marioPos = mario.Translation / 100f;
        Vector3 eye = marioPos - cameraDistance;

        Camera.LookAt(eye, marioPos);
    }

    private void GenerateFog()
    {
        var fogAreas = _stageSceneObjs.Where(x => x.StageObj.Name.Contains("FogArea") && x.StageObj.FileType == StageFileType.Design);

        for (int i = 0; i < Stage.StageFogs.Count; i++)
        {
            if (i == 0)
            {
                // First fog is always the main fog
                MainStageFog = Stage.StageFogs[0];
                continue;
            }

            if (!_stageFogList.ContainsKey(Stage.StageFogs[i]))
                _stageFogList.Add(Stage.StageFogs[i], new());
            foreach (IStageSceneObj sobj in fogAreas)
            {
                if (!sobj.StageObj.Properties.ContainsKey("Arg0") || sobj.StageObj.Properties["Arg0"]?.GetType() != typeof(int) || (int)sobj.StageObj.Properties["Arg0"]! != Stage.StageFogs[i].AreaId)
                    continue;

                _stageFogList[Stage.StageFogs[i]].Add(sobj);
            }
        }
    }

    private void GenerateSceneObjects(
        LayeredFSHandler fsHandler,
        GLTaskScheduler scheduler,
        ref string status
    )
    {
        _stageSceneObjs.Clear();
        _railObjs.Clear();
        _selectedObjects.Clear();

        if (Stage is null)
            return;

        IEnumerable<StageObjType> types = Enum.GetValues<StageObjType>()
            .Where(t => t != StageObjType.Rail && t != StageObjType.AreaChild && t != StageObjType.Child);

        foreach (StageObjType objType in types)
            GenerateSceneObjects(Stage.EnumerateStageObjs(objType), fsHandler, scheduler, ref status);

        GenerateSceneObjects(Stage.EnumerateRails(), fsHandler, scheduler, ref status);
    }

    private void GenerateSceneObjects(
        IEnumerable<StageObj>? stageObjs,
        LayeredFSHandler fsHandler,
        GLTaskScheduler scheduler,
        ref string status
    )
    {
        if (stageObjs is null)
            return;

        foreach (StageObj stageObj in stageObjs)
        {
            status = $"Loading model for {stageObj.Name}";

            GenerateSceneObject(stageObj, fsHandler, scheduler);
            GenerateSceneObjects(stageObj.Children, fsHandler, scheduler, ref status);
        }

        status = string.Empty;
    }

    private void GenerateSceneObject(StageObj stageObj, LayeredFSHandler fsHandler, GLTaskScheduler scheduler)
    {
        // Areas:
        if (stageObj.IsArea())
        {
            Vector4 color = AreaMaterial.GetAreaColor(stageObj.Name);
            CommonMaterialParameters matParams = new(color, new());

            BasicSceneObj areaSceneObj = new(stageObj, matParams, 20f, _lastPickingId);
            AddSwitchFromStageObj(stageObj, areaSceneObj);
            _stageSceneObjs.Add(areaSceneObj);
            _pickableObjs.Add(_lastPickingId++, areaSceneObj);
            return;
        }

        // Rails:
        if (stageObj.Type == StageObjType.Rail && stageObj is RailObj rail)
        {
            RailModel model = new(rail);
            RailSceneObj railSceneObj = new(rail, model, ref _lastPickingId);

            scheduler.EnqueueGLTask(model.Initialize);

            _railObjs.Add(railSceneObj);
            _pickableObjs.Add(railSceneObj.PickingId, railSceneObj);
            return;
        }

        // Actors:
        string actorName = stageObj.Name;

        if (
            stageObj.Properties.TryGetValue("ModelName", out object? modelName)
            && modelName is string modelNameString
            && !string.IsNullOrEmpty(modelNameString)
        )
            actorName = modelNameString;

        fsHandler.ReadCreatorClassNameTable().TryGetValue(actorName, out string? fallback);

        Actor actor;
        if (fallback != null && ClassDatabaseWrapper.DatabaseEntries.ContainsKey(fallback) && ClassDatabaseWrapper.DatabaseEntries[fallback].ArchiveName != null)
        {
            actor = fsHandler.ReadActor(actorName, ClassDatabaseWrapper.DatabaseEntries[fallback].ArchiveName, fallback, scheduler);
        }
        else if (ClassDatabaseWrapper.DatabaseEntries.ContainsKey(actorName) && ClassDatabaseWrapper.DatabaseEntries[actorName].ArchiveName != null)
            actor = fsHandler.ReadActor(actorName, ClassDatabaseWrapper.DatabaseEntries[actorName].ArchiveName, scheduler);
        else
            actor = fsHandler.ReadActor(actorName, fallback, scheduler);

        ActorSceneObj actorSceneObj = new(stageObj, actor, _lastPickingId);
        AddSwitchFromStageObj(stageObj, actorSceneObj);

        _stageSceneObjs.Add(actorSceneObj);
        _pickableObjs.Add(_lastPickingId++, actorSceneObj);
    }

    #region Point Editing

    public void AddPointRail(uint id, Vector3? position)
    {
        RailSceneObj? rl = EnumerateRailSceneObjs().FirstOrDefault(x => x.PickingId == id);
        if (rl == null) return;
        AddPointRail(rl, position);
    }

    public void AddPointRail(RailSceneObj rl, Vector3? position)
    {
        if (rl == null) return;
        uint oldId = _lastPickingId;
        RailPoint point;
        if (rl.RailObj.PointType == Enums.RailPointType.Linear)
        {
            point = new() { Point0Trans = position ?? Vector3.Zero };
            point.SetPointLinear();
        }
        else
        {
            point = new() { Point0Trans = position ?? Vector3.Zero, Point1Trans = position ?? Vector3.Zero, Point2Trans = position ?? Vector3.Zero};
        }
        for (int b = 0; b < 8; b++)
            point.Properties.Add($"Arg{b}", -1);
        rl.RailPoints.Add(new RailPointSceneObj(point, rl, ref _lastPickingId));
        rl.RailObj.Points.Add(point);
        rl.UpdateModel();
        for (uint i = oldId; i < _lastPickingId; i++)
            rl.AddToHash(i);
    }
    public void AddPointRail(RailSceneObj rl, Vector3 pos0, Vector3 pos1, Vector3 pos2)
    {
        if (rl == null) return;
        uint oldId = _lastPickingId;
        RailPoint point;
        if (rl.RailObj.PointType == Enums.RailPointType.Linear)
        {
            point = new() { Point0Trans = pos0 };
            point.SetPointLinear();
        }
        else
        {
            point = new() { Point0Trans = pos0, Point1Trans = pos1, Point2Trans = pos2};
        }
        rl.RailPoints.Add(new RailPointSceneObj(point, rl, ref _lastPickingId));
        rl.RailObj.Points.Add(point);
        rl.UpdateModel();
        for (uint i = oldId; i < _lastPickingId; i++)
            rl.AddToHash(i);
    }
    public uint InsertPointRail(RailSceneObj rl, int pos,  Vector3 position)
    {
        if (rl == null) return 0;
        uint oldId = _lastPickingId;
        RailPoint point;
        if (rl.RailObj.PointType == Enums.RailPointType.Linear)
        {
            point = new() { Point0Trans = position };
            point.SetPointLinear();
        }
        else
        {
            point = new() { Point0Trans = position, Point1Trans = position, Point2Trans = position};
        }
        for (int b = 0; b < 8; b++)
            point.Properties.Add($"Arg{b}", -1);
        rl.RailPoints.Insert(pos, new RailPointSceneObj(point, rl, ref _lastPickingId));
        rl.RailObj.Points.Insert(pos, point);
        rl.UpdateModel();
        for (uint i = oldId; i < _lastPickingId; i++)
            rl.AddToHash(i);
        return _lastPickingId-3;
    }
    public uint InsertPointRail(RailSceneObj rl, int pos, Vector3 pos0, Vector3 pos1, Vector3 pos2)
    {
        if (rl == null) return 0;
        uint oldId = _lastPickingId;
        RailPoint point;
        if (rl.RailObj.PointType == Enums.RailPointType.Linear)
        {
            point = new() { Point0Trans = pos0 };
            point.SetPointLinear();
        }
        else
        {
            point = new() { Point0Trans = pos0, Point1Trans = pos1, Point2Trans = pos2};
        }
        for (int b = 0; b < 8; b++)
            point.Properties.Add($"Arg{b}", -1);
        rl.RailPoints.Insert(pos, new RailPointSceneObj(point, rl, ref _lastPickingId));
        rl.RailObj.Points.Insert(pos, point);
        rl.UpdateModel();
        for (uint i = oldId; i < _lastPickingId; i++)
            rl.AddToHash(i);
        return _lastPickingId-3;
    }
    public void RemovePointRail(RailSceneObj rl, uint id)
    {
        if (rl == null) return;
        var p = rl.GetObjectByPickingId(id);
        // if (p is RailHandleSceneObj) // Return to parent position 
        if (p is RailPointSceneObj)
        {
            rl.RailPoints.Remove((RailPointSceneObj)p);
            rl.RailObj.Points.Remove(((RailPointSceneObj)p).RailPoint);
            rl.RemoveFromHash(p.PickingId);
            if (((RailPointSceneObj)p).PointType == RailPointType.Bezier) { rl.RemoveFromHash(p.PickingId+1); rl.RemoveFromHash(p.PickingId+2);}  
        }
        rl.UpdateModel();
    }

    #endregion

    private void DestroySceneObject(ISceneObj sceneObj)
    {
        switch (sceneObj)
        {
            case ISceneObj x when x is IStageSceneObj y:
                _stageSceneObjs.Remove(y);
                break;

            case ISceneObj x when x is RailSceneObj y:
                _railObjs.Remove(y);
                break;
        }

        _pickableObjs.Remove(sceneObj.PickingId);
    }

    internal ISceneObj GetSceneObjFromStageObj(StageObj obj)
    {
        return _stageSceneObjs.First(x => x.StageObj == obj);
    }

    internal bool TryGetPickableObj(uint id, [MaybeNullWhen(false)] out ISceneObj obj)
    {
        if (_pickableObjs.TryGetValue(id, out obj))
            return true;

        foreach (RailSceneObj railObj in _railObjs)
        {
            ISceneObj? res = railObj.GetObjectByPickingId(id);
            if (res is not null)
            {
                obj = res;
                return true;
            }
        }

        return false;
    }
}
