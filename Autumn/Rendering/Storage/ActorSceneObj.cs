using System.Numerics;
using System.Text.RegularExpressions;
using Autumn.Background;
using Autumn.Enums;
using Autumn.FileSystems;
using Autumn.Rendering.CtrH3D;
using Autumn.Storage;
using Autumn.Utils;
using Autumn.Wrappers;

namespace Autumn.Rendering.Storage;

internal class ActorSceneObj : IStageSceneObj
{
    public StageObj StageObj { get; }
    public Actor Actor { get; set; }
    public List<Actor> SubActors = new(); // Flagpole top, Arg additions, etc
    public List<Transform> SubActorTransforms = new(); //Transform
    public List<Actor> Shadows = new();
    /// <summary>
    /// Number of subactors this actor has by default, without args
    /// </summary>
    public int BaseSubActorCount = 0; 

    public Matrix4x4 Transform { get; set; }
    public AxisAlignedBoundingBox AABB { get; set; }

    public uint PickingId { get; set; }
    public bool Selected { get; set; }
    public bool Hovering { get; set; }
    public bool IsVisible { get; set; } = true;
    public Vector3 DeltaTranslation;
    public Vector3 DeltaScale = Vector3.One;
    public Vector3 DeltaRotation;


    public ActorSceneObj(StageObj stageObj, Actor actorObj, uint pickingId)
    {
        StageObj = stageObj;
        Actor = actorObj;
        PickingId = pickingId;
        AABB = stageObj.Name.Contains("TransparentWall") ? actorObj.AABB * 4 : actorObj.AABB;

        UpdateTransform();
    }

    public void UpdateTransform()
    {
        Vector3 scale = Actor.IsEmptyModel ? StageObj.Scale : (StageObj.Scale * DeltaScale) * 0.01f;
        Transform = MathUtils.CreateTransformWithDelta(StageObj.Translation * 0.01f, DeltaTranslation * 0.01f, scale , StageObj.Rotation + DeltaRotation);
    }
    public Matrix4x4 GetTransform(Vector3 sc, Vector3 rt, Vector3 tl)
    {
        return MathUtils.CreateTransformWithDelta((StageObj.Translation) * 0.01f, tl * 0.01f, (StageObj.Scale * sc) * 0.01f , StageObj.Rotation + rt + DeltaRotation);
    }

    public void UpdateActor(LayeredFSHandler fsHandler, Scene scn, GLTaskScheduler scheduler)
    {
        string actorName = StageObj.Name;

        if (
            StageObj.Properties.TryGetValue("ModelName", out object? modelName)
            && modelName is string modelNameString
            && !string.IsNullOrEmpty(modelNameString)
        )
            actorName = modelNameString;

        DeltaTranslation = Vector3.Zero;
        DeltaScale = Vector3.One;
        DeltaRotation = Vector3.Zero;
        SubActors.Clear();
        SubActorTransforms.Clear();
        Shadows.Clear();
        BaseSubActorCount = 0;

        fsHandler.ReadCreatorClassNameTable().TryGetValue(actorName, out string? actorClass);

        Actor = fsHandler.ReadActorNew(actorName, actorClass, scheduler);
        AABB = Actor.AABB;

        if (actorClass != null && ClassModifiersWrapper.ModifierEntries.ContainsKey(actorClass)) 
        {
            fsHandler.ReadActorExtras(actorName, actorClass, this, scheduler);
        
            ClassModifiersWrapper.ModifierEntry? act = ClassModifiersWrapper.GetEntry(actorName, actorClass);
            if (act is not null)
            {
                if (act.Value.Translation != null) 
                    DeltaTranslation = act.Value.Translation.Value;
                if (act.Value.Scale != null) 
                    DeltaScale = act.Value.Scale.Value;
                if (act.Value.Rotation != null) 
                    DeltaRotation = act.Value.Rotation.Value;
                if (act.Value.ExtraModels != null)
                {
                    foreach(Actor ac in SubActors)
                    {
                        SubActorTransforms.Add(new());
                        if (act.Value.ExtraModels[ac.Name] != null)
                        {
                            if (act.Value.ExtraModels[ac.Name]!.Value.Translation != null) 
                                SubActorTransforms[BaseSubActorCount].Translate = act.Value.ExtraModels[ac.Name]!.Value.Translation!.Value;
                            if (act.Value.ExtraModels[ac.Name]!.Value.Scale != null) 
                                SubActorTransforms[BaseSubActorCount].Scale = act.Value.ExtraModels[ac.Name]!.Value.Scale!.Value;
                            if (act.Value.ExtraModels[ac.Name]!.Value.Rotation != null) 
                                SubActorTransforms[BaseSubActorCount].Rotate = act.Value.ExtraModels[ac.Name]!.Value.Rotation!.Value;
                        }
                        BaseSubActorCount += 1;
                    }
                }

                if (act.Value.Args != null)
                foreach (string arg in act.Value.Args.Keys)
                {
                    if (StageObj.Properties.ContainsKey(arg))
                    {
                        UpdateActorFromArg(fsHandler, (ClassModifiersWrapper.ModifierEntry)act, arg, scheduler);
                    }
                }
            }
        }
        if (StageObj.Name == "ShadowObj")
        {
            Actor.IsShadowModel = true;
        }
        foreach (ActorShadow shadow in Actor.InitShadow)
        {
            Shadows.Add( fsHandler.ReadActor( shadow.GetShadowVolumeString(), scheduler));
        }

        scheduler.EnqueueGLTask( gl => 
            {
                scn.Opaque.Remove(this);
                scn.Translucent.Remove(this);
                scn.Subtractive.Remove(this);
                scn.Additive.Remove(this);
                scn.Shadows.Remove(this);
                if (Actor.IsEmptyModel) scn.Opaque.Add(this);
                else if (Actor.IsShadowModel) scn.Shadows.Add(this);
                else
                {
                    if (Actor.CountMeshesLayer(H3DMeshLayer.Opaque) > 0) scn.Opaque.Add(this);
                    if (Actor.CountMeshesLayer(H3DMeshLayer.Translucent) > 0) scn.Translucent.Add(this);
                    if (Actor.CountMeshesLayer(H3DMeshLayer.Subtractive) > 0) { scn.Subtractive.Add(this);}
                    if (Actor.CountMeshesLayer(H3DMeshLayer.Additive) > 0) { scn.Additive.Add(this);}
                }
            }
        );
        UpdateTransform();
    }

    public void UpdateActorFromArg(LayeredFSHandler fsHandler, ClassModifiersWrapper.ModifierEntry entry, string arg, GLTaskScheduler scheduler)
    {
        int end;
        switch (entry.Args![arg].ArgType)
        {
            case ArgType.Tower: // Add actors on top or below, if we add below we move the actor upwards
                var twAg = (ClassModifiersWrapper.TowerArg)entry.ArgsRem![arg];
                string baseModel = twAg.RepeatModel ?? Actor.Name;
                Vector3 offset = twAg.Offset;
                bool BottomUp = twAg.CountTop != null && twAg.CountTop!.Value;
                int start = SubActors.Count - BaseSubActorCount;
                end = int.Clamp((int)StageObj.Properties[arg]!, BottomUp ? 1 : 0, 10) - (BottomUp ? 1 : 0); // Default is 3 / -1 is 3, max is 10, min is 0 but let's not


                if (start > end)
                {
                    for (int j = start-1; j >= end; j--)
                    {
                        SubActors.RemoveAt(j);
                        SubActorTransforms.RemoveAt(j);
                        if (!BottomUp)
                            DeltaTranslation = offset * (j);
                    }
                }
                else
                {
                    for (int j = start; j < end; j++)
                    {
                        Actor? SubAct = fsHandler.ReadActorExtrasArg(baseModel, scheduler);
                        if (SubAct is null)
                            continue;
                        SubActors.Add(SubAct);
                        SubActorTransforms.Add(new() { Translate = (BottomUp ? 1 : -1)* offset * (j+1)});
                        if (!BottomUp)
                            DeltaTranslation = offset * (j+1);
                    }
                }
                AABB = Actor.AABB * (end > 0 ? end : 1);
                UpdateTransform();

            break;

            case ArgType.SwingCoreLength:
                var swLn = (ClassModifiersWrapper.SwingingCore)entry.ArgsRem![arg];
                int argval = (int)StageObj.Properties[arg]!;
                if (argval == -1) argval = 800; // Taken from game code
                
                int chainCount = (int)((argval - 250) * 0.01f) - 1; // Taken from game code
                // chainCount = (int)((argval + 30) * 0.016666668f) - 1; // Taken from game code
                if (chainCount < 1) chainCount = 1;

                end = SubActors.Count - 1;
                int oldcount = end-BaseSubActorCount;

                // Check if the chain count changed to keep or remove
                if (chainCount < oldcount)
                    for (int j = end - 1; j >= BaseSubActorCount+ chainCount; j--)
                    {
                        SubActors.RemoveAt(j);
                        SubActorTransforms.RemoveAt(j);
                    }
                else if (chainCount > oldcount)
                    for (int i = int.Max(oldcount, 0); i < chainCount; i++)
                    {
                        Actor? SubAct = fsHandler.ReadActorExtrasArg(swLn.ChainModel, scheduler);
                        if (SubAct is null)
                            continue;
                        if (SubActors.Count > 0)
                        {
                            SubActors.Insert(SubActors.Count-1, SubAct);
                            SubActorTransforms.Insert(SubActors.Count-1, new());
                        }
                        else
                        {
                            SubActors.Add(SubAct);
                            SubActorTransforms.Add(new());
                        }
                    }
                // Update transforms to the current
                for (int i = BaseSubActorCount; i < chainCount + BaseSubActorCount; i++)
                {
                    SubActorTransforms[i].Translate = new Vector3(0,(argval - 300) / (chainCount + 1) * (-i-1) - 50, 0);
                }

                // only once?
                Actor? SubActBall = fsHandler.ReadActorExtrasArg(swLn.HeadModel, scheduler);
                if (SubActBall != null)
                {
                    if (!SubActors.Contains(SubActBall))
                    {
                        SubActors.Add(SubActBall);
                        SubActorTransforms.Add(new() { Translate = new Vector3(0, - argval + 100,0)});
                    }
                    else SubActorTransforms[^1].Translate = new Vector3(0, - argval + 100,0);
                }
                AABB = Actor.AABB * (argval / 100);
                UpdateTransform();
            break;

            case ArgType.AddExtraModel:
                var exMd = (ClassModifiersWrapper.ExtraArgModels)entry.ArgsRem![arg];
                end = SubActors.Count -1 ;
                if ((int)StageObj.Properties[arg]! == -1)
                {
                    for (int i = end; i >= BaseSubActorCount; i--)
                    {
                        SubActors.RemoveAt(i);
                        SubActorTransforms.RemoveAt(i);
                    }
                }
                else
                {
                    foreach (string s in exMd.Models.Keys)
                    {
                        Actor? AddedModl = fsHandler.ReadActorExtrasArg(s, scheduler);
                        if (AddedModl is null) continue;
                        if (!SubActors.Contains(AddedModl))
                        { 
                            SubActors.Add(AddedModl);
                            SubActorTransforms.Add(exMd.Models[s].GetTransform());
                        }
                    }
                }
            break;

            case ArgType.ShadowType:
                Actor? AddedMdl = (int)StageObj.Properties[arg]! switch
                    {
                        1 => fsHandler.ReadActorExtrasArg(ActorShadow.GetShadowVolumeString(ActorShadow.VolumeType.Pyramid), scheduler), // Pyramid
                        2 => fsHandler.ReadActorExtrasArg(ActorShadow.GetShadowVolumeString(ActorShadow.VolumeType.Sphere), scheduler), //Sphere
                        3 => fsHandler.ReadActorExtrasArg(ActorShadow.GetShadowVolumeString(ActorShadow.VolumeType.Cylinder), scheduler), // Cylinder
                        4 => fsHandler.ReadActorExtrasArg(ActorShadow.GetShadowVolumeString(ActorShadow.VolumeType.Cone), scheduler), // Cone
                        _ => fsHandler.ReadActorExtrasArg(ActorShadow.GetShadowVolumeString(ActorShadow.VolumeType.Cube), scheduler), // Cube
                    };
                if (AddedMdl is null) return;
                Actor.ForceModelNotEmpty(); 
                Actor = AddedMdl;
                AABB = Actor.AABB * 0.01f;
                UpdateTransform();
            break;

            case ArgType.ScaleAxis:
                var scAx = (ClassModifiersWrapper.ScaleAxis)entry.ArgsRem![arg];
                switch (scAx.Axis)
                {
                    case "X":
                        DeltaScale.X = (int)StageObj.Properties[arg]!;
                    break;
                    case "Y":
                        DeltaScale.Y = (int)StageObj.Properties[arg]!;
                    break;
                    case "Z":
                        DeltaScale.Z = (int)StageObj.Properties[arg]!;
                    break;
                }
                UpdateTransform();
            break;

            case ArgType.RotateCoreCount:
            case ArgType.RotateCoreSides:
                string sideCoreS = entry.Args![arg].ArgType == ArgType.RotateCoreSides ? arg : entry.ArgsRem!.First(x => x.Value is ClassModifiersWrapper.RotateCoreBars).Key;
                var sideCore = (ClassModifiersWrapper.RotateCoreBars)entry.ArgsRem![sideCoreS];
                int cnt = 1;
                int sides = (int)(StageObj.Properties[sideCoreS] ?? 1);
                if (sideCore.UseArgCount)
                {
                    cnt = (int)StageObj.Properties[entry.Args![arg].ArgType == ArgType.RotateCoreCount ? arg : entry.ArgsRem!.First(x => x.Value is ClassModifiersWrapper.RotateCoreCount).Key]!;
                }
                else
                {
                    cnt = int.Parse(Regex.Match(Actor.Name, @"-?\d+").Value);
                }
                cnt = cnt < 0 ? 4 : cnt;                
                sides = sides < 1 ? 1 : sides;                
                end = SubActors.Count - 1;
                for (int i = end; i >= BaseSubActorCount; i--)
                {
                    SubActors.RemoveAt(i);
                    SubActorTransforms.RemoveAt(i);
                }
                
                for (int i = 0; i < sides; i++)
                {
                    for (int s = 0; s < cnt; s++)
                    {
                        Actor? AddedModl = fsHandler.ReadActorExtrasArg(s < cnt -1 ? sideCore.MiddleModel : sideCore.PointModel, scheduler);
                        if (AddedModl is null) continue;
                        SubActors.Add(AddedModl);
                        SubActorTransforms.Add( new() 
                        {
                            Translate = Vector3.Transform(new Vector3 (100 * (s+1), 50, 0), Matrix4x4.CreateRotationY(360 / sides * i *(float)Math.PI/180)),
                            Rotate = new Vector3(0, 360 / sides * i + 90, 0)
                        }
                        );
                    }
                }
                AABB = Actor.AABB * cnt;
                UpdateTransform();
            break;

            case ArgType.PicketHeight:
                DeltaTranslation.Y = 70 * - ((int)StageObj.Properties[arg]! == -1 ? 0 : (int)StageObj.Properties[arg]!);
                UpdateTransform();
            break;

            default:
            
            break;
        } 
    }
}
