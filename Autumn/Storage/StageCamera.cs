using System.Numerics;
using System.Reflection;
using Autumn.Enums;
using BYAMLSharp;

namespace Autumn.Storage;

internal class CameraParams
{
    public List<StageCamera> Cameras = new();
    public VisionParams VisionParam = new();
    public BYAMLNode GetNodes()
    {
        BYAMLNode[] cams = new BYAMLNode[Cameras.Count];
        for (int i = 0; i < Cameras.Count; i++)
        {
            cams[i] = Cameras[i].GetNodes();
        }
        Dictionary<string, BYAMLNode> rd = new()
        {
            { "CameraParams", new(BYAMLNodeType.Array, cams) },
            { "VisionParam", VisionParam.GetNodes() },
        };

        return new(rd);
    }

    // Editor stuff
    
    public StageCamera? GetCamera(int id, StageCamera.CameraCategory type)
    {
        return Cameras.Where(i => i.UserGroupId == id && i.Category == type).FirstOrDefault();
    }

    public static StageCamera.CameraCategory GetObjectCategory(StageObj stageObj)
    {
        StageCamera.CameraCategory camType;
        if (stageObj.Name == "EntranceCameraObj") camType = StageCamera.CameraCategory.Entrance;
        else if (stageObj.Type == StageObjType.CameraArea) camType = StageCamera.CameraCategory.Map;
        else if (stageObj.Type == StageObjType.DemoScene) camType = StageCamera.CameraCategory.Event;
        else camType = StageCamera.CameraCategory.Object;
        return camType;
    }
    public void AddCamera(StageCamera cam)
    {
        int nid = cam.UserGroupId; 
        while (Cameras.Where(x => x.Category == cam.Category && x.UserGroupId == nid).Count() > 0)
        {
            nid+=1;
        }
        cam.UserGroupId = nid;
        Cameras.Add(cam);
    }
}

internal class VisionParams
{
    public float? FarClipDistance;
    public float? NearClipDistance; // NearClipDistacne // Yes they wrote it wrong
    public float? FovyDegree;
    public float? StereovisionDepth;
    public float? StereovisionDistance;

    public VisionParams() { }
    public VisionParams(Dictionary<string, BYAMLNode> vDict)
    {
        vDict!.TryGetValue("FarClipDistance", out BYAMLNode? vFar);
        vDict!.TryGetValue("NearClipDistacne", out BYAMLNode? vNear);// NearClipDistacne // Yes they wrote it wrong
        vDict!.TryGetValue("FovyDegree", out BYAMLNode? vFovy);
        vDict!.TryGetValue("StereovisionDepth", out BYAMLNode? vStDp);
        vDict!.TryGetValue("StereovisionDistance", out BYAMLNode? vStDs);
        FarClipDistance = vFar?.GetValueAs<float>();
        NearClipDistance = vNear?.GetValueAs<float>();
        FovyDegree = vFovy?.GetValueAs<float>();
        StereovisionDepth = vStDp?.GetValueAs<float>();
        StereovisionDistance = vStDs?.GetValueAs<float>();
    }

    public VisionParams(VisionParams? visionParam)
    {
        if (visionParam == null) return;
        FarClipDistance = visionParam.FarClipDistance;
        NearClipDistance = visionParam.NearClipDistance; 
        FovyDegree = visionParam.FovyDegree;
        StereovisionDepth = visionParam.StereovisionDepth;
        StereovisionDistance = visionParam.StereovisionDistance;
    }

    public BYAMLNode GetNodes()
    {
        Dictionary<string, BYAMLNode> ret = new();
        if (FovyDegree != null) ret.Add("FovyDegree", new((float)FovyDegree));
        if (FarClipDistance != null) ret.Add("FarClipDistance", new((float)FarClipDistance));
        if (NearClipDistance != null) ret.Add("NearClipDistacne", new((float)NearClipDistance));
        if (StereovisionDepth != null) ret.Add("StereovisionDepth", new((float)StereovisionDepth));
        if (StereovisionDistance != null) ret.Add("StereovisionDistance", new((float)StereovisionDistance));
        return new(ret);
    }

}

internal class StageCamera
{
    public CameraCategory Category = CameraCategory.Map;
    public CameraClass Class = CameraClass.Parallel;
    public int UserGroupId = 0; // CameraId
    public string UserName = "CameraArea";

    public CameraProperties CamProperties = new();
    public VisionParams? VisionParam = null;
    public VOff? VelocityOffsetter; // Max offset or MaxOffsetAxisTwo, Offsets the camera as the player moves, MaxOffset only does it on the camera's X axis, while MaxOffsetAxisTwo does it on X and Z (Assuming Y up), horizontal space
    public Rot? Rotator; //AngleMax, IsEnable, Rotates the camera left or right a given angle (AngleMax)
    public VAbsorb? VerticalAbsorber; // IsInvalidate (only useful if TRUE) 
    public DATuner? DashAngleTuner; //AddAngleMax, ZoomOutOffsetMax, Moves and rotates the camera (on X axis) when the player runs
    public bool HasLimitBox = false; // Editor property

    internal class VOff
    {
        public float? MaxOffset;
        public Vector2? MaxOffsetAxisTwo;
        public VOff() {}
        public VOff(Dictionary<string, BYAMLNode> vDict)
        {
            vDict!.TryGetValue("MaxOffset", out BYAMLNode? MaxOff);
            vDict!.TryGetValue("MaxOffsetAxisTwo", out BYAMLNode? MaxOff2);
            MaxOffset = MaxOff?.GetValueAs<float>();
            MaxOffsetAxisTwo = DictToVec2(MaxOff2?.GetValueAs<Dictionary<string, BYAMLNode>>());
        }

        public VOff(VOff? VO)
        {
            if (VO == null) return;
            MaxOffset = VO.MaxOffset;
            MaxOffsetAxisTwo = VO.MaxOffsetAxisTwo;
        }

        public BYAMLNode GetNodes()
        {
            Dictionary<string, BYAMLNode> ret = new();
            if (MaxOffset != null) ret.Add("MaxOffset", new((float)MaxOffset));
            if (MaxOffsetAxisTwo != null) ret.Add("MaxOffsetAxisTwo", new(Vec2ToDict((Vector2)MaxOffsetAxisTwo)));
            return new(ret);
        }

    }
    internal class Rot //AngleMax, IsEnable
    {
        public float? AngleMax;
        public bool? IsEnable;
        public Rot(){}
        public Rot(Dictionary<string, BYAMLNode> vDict)
        {
            vDict!.TryGetValue("AngleMax", out BYAMLNode? Amax);
            vDict!.TryGetValue("IsEnable", out BYAMLNode? Enabl);
            AngleMax = Amax?.GetValueAs<float>();
            IsEnable = Enabl?.GetValueAs<bool>();
        }

        public Rot(Rot? Rt)
        {
            if (Rt == null) return;
            AngleMax = Rt.AngleMax;
            IsEnable = Rt.IsEnable;
        }

        public BYAMLNode GetNodes()
        {
            Dictionary<string, BYAMLNode> ret = new();
            if (AngleMax != null) ret.Add("AngleMax", new((float)AngleMax));
            if (IsEnable != null && IsEnable != false) ret.Add("IsEnable", new((bool)IsEnable));
            return new(ret);
        }

    }
    internal class VAbsorb //IsInvalidate
    {
        public bool IsInvalidate;
        public VAbsorb() {} 
        public VAbsorb(Dictionary<string, BYAMLNode> vDict)
        {
            vDict!.TryGetValue("IsInvalidate", out BYAMLNode? Inv);
            IsInvalidate = (bool)Inv?.GetValueAs<bool>()!;
        }

        public VAbsorb(VAbsorb? vAbsorb)
        {
            if (vAbsorb == null) return;
            IsInvalidate = vAbsorb.IsInvalidate;
        }

        public BYAMLNode GetNodes()
        {
            Dictionary<string, BYAMLNode> ret = new();
            ret.Add("IsInvalidate", new(IsInvalidate));
            return new(ret);
        }

    }
    internal class DATuner
    {
        public float? AddAngleMax;
        public float? ZoomOutOffsetMax;
        public DATuner() {}
        public DATuner(Dictionary<string, BYAMLNode> vDict)
        {
            vDict!.TryGetValue("AddAngleMax", out BYAMLNode? AMax);
            vDict!.TryGetValue("ZoomOutOffsetMax", out BYAMLNode? ZOMax);
            AddAngleMax = AMax?.GetValueAs<float>();
            ZoomOutOffsetMax = ZOMax?.GetValueAs<float>();
        }

        public DATuner(DATuner? DAT)
        {
            if (DAT == null) return;
            AddAngleMax = DAT.AddAngleMax;
            ZoomOutOffsetMax = DAT.ZoomOutOffsetMax;
        }

        public BYAMLNode GetNodes()
        {
            Dictionary<string, BYAMLNode> ret = new();
            if (AddAngleMax != null) ret.Add("AddAngleMax", new((float)AddAngleMax));
            if (ZoomOutOffsetMax != null) ret.Add("ZoomOutOffsetMax", new((float)ZoomOutOffsetMax));
            return new(ret);
        }

    }
    public string CameraName()
    {
        string r = "";
        r += Class;
        r += " " + Category + " Camera ";
        r += UserGroupId;
        return r;
    }

    public class CameraProperties
    {
        // Properties that tend to be available always
        public float? AngleH = 0;
        public float? AngleV = 30;
        public float? Distance = 900;
        // DashAngleTuner
        //public bool DashAngleTuner = false;
        //public float? AddAngleMax;
        //public float? ZoomOutOffsetMax;
        public int? InterpoleFrame;
        public Vector3? LimitBoxMax;
        public Vector3? LimitBoxMin;
        // Rotator
        //public bool Rotator = false;
        //public float? AngleMax;
        //public bool? IsEnable;
        public float? SideDegree;
        public float? SideOffset;
        public float? UpOffset;
        // VelocityOffsetter
        //public bool VelocityOffsetter = false;
        //public float? MaxOffset;
        //public Vector2? MaxOffsetAxisTwo;
        // VerticalAbsorber -> IsInvalidate == false same as not having it at all? 
        //public bool? VerticalAbsorber;
        //public VisionParams? VisionParam;
        public Vector3? CameraPos;
        public Vector3? LookAtPos;
        public bool? IsCalcStartPosUseLookAtPos; // Only used in Rail cameras ?
        public bool? IsLimitAngleFix; // Only used in Parallel cameras ?
        // public int? RailId; // rail's l_id not used in-editor
        public RailObj? Rail;
        // Follow class only?
        public float? HighAngle;
        public float? LowAngle;
        public float? PullDistance;
        public float? PushDistance;
        public float? TargetLookRate;
        public float? TargetRadius;
        public bool? IsDistanceFix; // Only used in Tower cameras ? Sets the distance to be relative to the tower Position, instead of relative to the player
        // Tower Only?
        public float? LimitYMax;
        public float? LimitYMin;
        public Vector3? Position;
        // ParallelVersus Only?
        public float? DistanceMax;
        public float? DistanceMin;
        public float? FovyVersus;
        //public float? DemoTarget;
        public float? CameraOffset;

        public CameraProperties Clone()
        {
            return (CameraProperties)MemberwiseClone();
        }
    }

    public enum CameraClass
    {
        Parallel,
        FixAll,
        FixAllSpot,
        Tower,
        Rail,
        RailTower,
        ParallelTarget,
        FixPos,
        FixPosSpot,
        ParallelVersus,
        DemoTarget,
        TowerTarget,
        Anim,
        Follow
    }
    public enum CameraCategory
    {
        Map,
        Object,
        Entrance,
        Event // DemoOpeningStage1
    }
    public StageCamera() { }
    public StageCamera(StageCamera cam)
    {
        Category = cam.Category;
        UserName = cam.UserName;
        Class = cam.Class;
        UserGroupId = cam.UserGroupId;
        VisionParam = cam.VisionParam != null ? new(cam.VisionParam) : null;
        VelocityOffsetter = cam.VelocityOffsetter != null ? new(cam.VelocityOffsetter) : null;
        Rotator = cam.Rotator != null ? new(cam.Rotator) : null;
        VerticalAbsorber = cam.VerticalAbsorber != null ? new(cam.VerticalAbsorber) : null;
        DashAngleTuner = cam.DashAngleTuner != null ? new(cam.DashAngleTuner) : null;
        CamProperties = cam.CamProperties.Clone();
        HasLimitBox = cam.HasLimitBox;
    }
    public StageCamera(Dictionary<string, BYAMLNode> dict)
    {
        // Always present
        dict.TryGetValue("Category", out BYAMLNode Cat);
        dict.TryGetValue("Class", out BYAMLNode Cls);
        dict.TryGetValue("UserGroupId", out BYAMLNode UsId);
        dict.TryGetValue("UserName", out BYAMLNode UsNm);

        foreach (string s in dict.Keys)
        {
            if (dict[s].NodeType == BYAMLNodeType.Dictionary)
            {
                // // Holder nodes

                var bbb = typeof(CameraProperties).GetField(s);
                switch (s)
                {
                    case "Position":
                    case "CameraPos":
                    case "LookAtPos":
                        if (bbb != null) bbb.SetValue(CamProperties, DictToVec3(dict[s].GetValueAs<Dictionary<string, BYAMLNode>>())!);
                        //Properties.Add(s, DictToVec3(dict[s].GetValueAs<Dictionary<string, BYAMLNode>>())!);
                        break;
                    case "LimitBoxMin":
                    case "LimitBoxMax":
                        HasLimitBox = true;
                        if (bbb != null) bbb.SetValue(CamProperties, DictToVec3(dict[s].GetValueAs<Dictionary<string, BYAMLNode>>())!);
                        //Properties.Add(s, DictToVec3(dict[s].GetValueAs<Dictionary<string, BYAMLNode>>())!);
                        break;
                    case "VisionParam":
                        VisionParam =  new VisionParams(dict[s].GetValueAs<Dictionary<string, BYAMLNode>>()!);
                        break;
                    case "DashAngleTuner":
                        DashAngleTuner =  new DATuner(dict[s].GetValueAs<Dictionary<string, BYAMLNode>>()!);
                        break;
                    case "VelocityOffsetter":
                        VelocityOffsetter =  new VOff(dict[s].GetValueAs<Dictionary<string, BYAMLNode>>()!);
                        break;
                    case "VerticalAbsorber":
                        VerticalAbsorber =  new VAbsorb(dict[s].GetValueAs<Dictionary<string, BYAMLNode>>()!);
                        break;
                    case "Rotator":
                        Rotator =  new Rot(dict[s].GetValueAs<Dictionary<string, BYAMLNode>>()!);
                        break;
                }
            }
            else
                switch (s)
                {
                    case "RailId":
                    CamProperties.Rail = new() { RailNo = (int)dict[s].Value! };
                    break;
                    case "Category":
                    case "Class":
                    case "UserGroupId":
                    case "UserName":
                    break;
                    default:
                    var bbb = typeof(CameraProperties).GetField(s);
                    bbb?.SetValue(CamProperties, dict[s].Value!);
                    break;
                    
                }
        }

        Category = Enum.Parse<CameraCategory>(Cat!.GetValueAs<string>() ?? "Map");
        Class = Enum.Parse<CameraClass>(Cls!.GetValueAs<string>() ?? "Parallel");
        UserGroupId = UsId!.GetValueAs<int>();
        UserName = UsNm!.GetValueAs<string>()!;

    }

    /// <summary>
    /// This dictionary will only contain properties that are specific to some classes
    /// </summary>
    public static Dictionary<string, CameraClass[]> SpecialProperties = new Dictionary<string, CameraClass[]>
    {
        {"CameraPos", [CameraClass.FixAll, CameraClass.FixPos]},
        {"LookAtPos", [CameraClass.FixAll]},
        {"Rail", [CameraClass.Rail, CameraClass.RailTower]},

        {"LimitYMax", [CameraClass.Tower]},
        {"LimitYMin", [CameraClass.Tower]},
        {"Position", [CameraClass.Tower]},
        
        {"DistanceMax", [CameraClass.ParallelVersus]},
        {"DistanceMin", [CameraClass.ParallelVersus]},
        {"FovyVersus", [CameraClass.ParallelVersus]},

        {"HighAngle", [CameraClass.Follow]},
        {"LowAngle", [CameraClass.Follow]},
        {"PullDistance", [CameraClass.Follow, CameraClass.ParallelVersus]},
        {"PushDistance", [CameraClass.Follow, CameraClass.ParallelVersus]}, // W5-Castle Bowser ParallelVersus

        {"CameraOffset", [CameraClass.DemoTarget, CameraClass.ParallelTarget]},
        {"TargetRadius", [CameraClass.DemoTarget, CameraClass.ParallelTarget]},
        {"TargetLookRate", [CameraClass.DemoTarget, CameraClass.ParallelTarget]},

        {"LimitBoxMin", [CameraClass.Parallel, CameraClass.ParallelVersus, CameraClass.ParallelTarget, CameraClass.Follow]},
        {"LimitBoxMax", [CameraClass.Parallel, CameraClass.ParallelVersus, CameraClass.ParallelTarget, CameraClass.Follow]},
    };

    public static Vector3? DictToVec3(Dictionary<string, BYAMLNode>? dict)
    {
        if (dict is null) return null;
        dict.TryGetValue("X", out BYAMLNode x);
        dict.TryGetValue("Y", out BYAMLNode y);
        dict.TryGetValue("Z", out BYAMLNode z);
        return new(float.Round(x?.GetValueAs<float>() ?? 1),
        float.Round(y?.GetValueAs<float>() ?? 1),
        float.Round(z?.GetValueAs<float>() ?? 1));
    }

    public static Vector2? DictToVec2(Dictionary<string, BYAMLNode>? dict)
    {
        if (dict is null) return null;
        dict.TryGetValue("X", out BYAMLNode x);
        dict.TryGetValue("Y", out BYAMLNode y);
        return new(x?.GetValueAs<float>() ?? 1, y?.GetValueAs<float>() ?? 1);
    }
    public static Dictionary<string, BYAMLNode> Vec3ToDict(Vector3 v)
    {
        return new()
        {
            {"X", new(v.X)},
            {"Y", new(v.Y)},
            {"Z", new(v.Z)},
        };
    }

    public static Dictionary<string, BYAMLNode> Vec2ToDict(Vector2 v)
    {
        return new()
        {
            {"X", new(v.X)},
            {"Y", new(v.Y)},
        };
    }

    public BYAMLNode GetNodes()
    {
        Dictionary<string, BYAMLNode> rd = new()
        {
            {"UserGroupId", new(UserGroupId)},
            {"UserName", new(UserName)},
            {"Category", new(Category.ToString())},
            {"Class", new(Class.ToString())},
        };

        TryAdd(rd, "VisionParam", VisionParam?.GetNodes());
        TryAdd(rd, "DashAngleTuner", DashAngleTuner?.GetNodes());
        TryAdd(rd, "Rotator", Rotator?.GetNodes());
        TryAdd(rd, "VelocityOffsetter", VelocityOffsetter?.GetNodes());
        TryAdd(rd, "VerticalAbsorber", VerticalAbsorber?.GetNodes());
        FieldInfo[] StageCameraFields = typeof(CameraProperties).GetFields();
        foreach (var CamField in StageCameraFields)
        {
            if (Class == CameraClass.FixAll || Class == CameraClass.FixAllSpot)
            {
                if (! new List<string> {"LookAtPos", "InterpoleFrame","CameraPos"}.Contains(CamField.Name)) 
                    continue;
            }
            if (Class == CameraClass.ParallelVersus)
            {
                if (new List<string> {"Distance"}.Contains(CamField.Name)) 
                    continue;
            }
            var vl = CamField.GetValue(CamProperties);
            if (vl == null) continue;
            if (CamField.FieldType == typeof(Vector3?)) TryAdd(rd, CamField.Name, new(Vec3ToDict((Vector3)vl)));
            else if (CamField.FieldType == typeof(RailObj)) TryAdd(rd, "RailId", new((vl as RailObj)!.RailNo));
            else TryAdd(rd, CamField.Name, new(vl));
        }

        return new(rd);
    }

    public void TryAdd(Dictionary<string, BYAMLNode> D, string K, BYAMLNode? V)
    {
        if (V != null && V.NodeType != BYAMLNodeType.Null) D.Add(K, V);
    }
}