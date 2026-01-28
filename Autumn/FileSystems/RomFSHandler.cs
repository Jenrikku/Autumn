using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Autumn.Background;
using Autumn.Enums;
using Autumn.Storage;
using Autumn.Utils;
using Autumn.Wrappers;
using BYAMLSharp;
using NARCSharp;
using SPICA.Formats.CtrGfx;
using SPICA.Formats.CtrH3D;
using SPICA.Formats.CtrH3D.LUT;
using SPICA.Formats.CtrH3D.Model;
using SPICA.Formats.CtrH3D.Model.Material;
using SPICA.Formats.CtrH3D.Model.Mesh;
using SPICA.Formats.CtrH3D.Texture;
using static Autumn.Storage.BgmTable;

namespace Autumn.FileSystems;

internal partial class RomFSHandler
{
    [GeneratedRegex("(.*)(Design|Map|Sound)(\\d+\\b).szs")]
    private static partial Regex StageFileRegex();

    private static readonly Regex _stagesRegex = StageFileRegex();

    private static readonly Encoding s_byamlEncoding = Encoding.GetEncoding("Shift-JIS");

    public string Root { get; }

    private readonly string _stagesPath;
    public string StagesPath { get { return _stagesPath; } }
    private readonly string _soundPath;
    private readonly string _actorsPath;
    private readonly string _ccntPath;

    private readonly Dictionary<string, Actor> _cachedActors = new();
    private readonly Dictionary<string, DateTime> _cachedActorsTimestamps = new();

    private ReadOnlyDictionary<string, string>? _creatorClassNameTable = null;
    private BgmTable? _bgmTable = null;
    private SystemDataTable? _GSDTable = null;
    private Dictionary<string, LightArea>? _lightAreas = null;

    public RomFSHandler(string path)
    {
        Root = path;

        _stagesPath = Path.Join(Root, "StageData");
        _actorsPath = Path.Join(Root, "ObjectData");
        _soundPath = Path.Join(Root, "SoundData");
        _ccntPath = Path.Join(Root, "SystemData", "CreatorClassNameTable.szs");

        // Only really used in ModFS
        Directory.CreateDirectory(_stagesPath);
        Directory.CreateDirectory(_actorsPath);
        Directory.CreateDirectory(_soundPath);
        Directory.CreateDirectory(Path.Join(Root, "SystemData"));
    }

    public bool ExistsStage(string name, byte scenario)
    {
        string[] paths =
        [
            Path.Join(_stagesPath, $"{name}Design{scenario}.szs"),
            Path.Join(_stagesPath, $"{name}Map{scenario}.szs"),
            Path.Join(_stagesPath, $"{name}Sound{scenario}.szs")
        ];

        foreach (string path in paths)
        {
            if (File.Exists(path))
                return true;
        }

        return false;
    }

    public bool ExistsActor(string name)
    {
        string path = Path.Join(_actorsPath, name + ".szs");
        return File.Exists(path);
    }

    public bool ExistsCreatorClassNameTable() => File.Exists(_ccntPath);

    public bool ExistsBgmTable() => File.Exists(Path.Join(_soundPath, "BgmTable.szs"));

    public bool ExistsGSDT() => File.Exists(Path.Join(_actorsPath, "GameSystemDataTable.szs"));
    public bool ExistsLightDataArea() => File.Exists(Path.Join(_actorsPath, "LightDataArea.szs"));
    public bool ExistsShaders() => File.Exists(Path.Join(_actorsPath, "Shader.szs"));

    public IEnumerable<(string Name, byte Scenario)> EnumerateStages()
    {
        if (!Directory.Exists(_stagesPath))
            yield break;

        HashSet<(string, byte)> stages = new();

        foreach (string file in Directory.EnumerateFiles(_stagesPath))
        {
            string fileName = Path.GetFileName(file);

            Match match = _stagesRegex.Match(fileName);

            if (!match.Success)
                continue;

            string name = match.Groups[1].Value;
            byte scenario = byte.Parse(match.Groups[3].Value);

            var stage = (name, scenario);

            if (stages.Add(stage))
                yield return stage;
        }
    }

    #region Stage Writing

    public Stage? TryReadStage(string pth)
    {
        string stg = Path.GetFileName(pth);
        string dir = Path.GetDirectoryName(pth)!;
        var match = _stagesRegex.Match(stg);
        if (!match.Success)
            return null;
        string name = match.Groups[1].Value;
        byte scenario = byte.Parse(match.Groups[3].Value);

        if (ExistsStage(name, scenario))
            return null;
        return ReadStageFull(dir, name, scenario);
    }

    public Stage ReadStage(string name, byte scenario)
    {
        return ReadStageFull(_stagesPath, name, scenario);
    }

    private Stage ReadStageFull(string dir, string name, byte scenario)
    {

        Stage stage = new(initialize: false) { Name = name, Scenario = scenario };
        stage.UserPath = dir+ Path.DirectorySeparatorChar + name + scenario;
        (string, StageFileType)[] paths =
        [
            (Path.Join(dir, $"{name}Design{scenario}.szs"), StageFileType.Design),
            (Path.Join(dir, $"{name}Map{scenario}.szs"), StageFileType.Map),
            (Path.Join(dir, $"{name}Sound{scenario}.szs"), StageFileType.Sound)
        ];

        foreach (var (path, fileType) in paths)
        {
            if (!File.Exists(path))
                continue;

            NARCFileSystem? narc = SZSWrapper.ReadFile(path);

            if (narc is null)
                continue;

            foreach (var (filename, contents) in narc.EnumerateFiles())
            {
                // Temporary code for reading any files that are not StageData.
                switch (filename)
                {
                    case string s when s.Contains("StageData"): // StageData.byml
                        // Read StageObjs from current stage file:
                        BYAML stageDataByaml = BYAMLParser.Read(contents, s_byamlEncoding);
                        IEnumerable<StageObj> stageObjs = ProcessStageObjs(stageDataByaml, fileType);

                        stage.AddStageObjs(stageObjs);
                        break;

                    case string s when s.Contains("StageInfo"):
                        if (!s.Contains($"{scenario}") && s != "StageInfo.byml")
                        {
                            stage.AddAdditionalFile(fileType, filename, contents);
                            break;
                        }
                        BYAML stageInfoByaml = BYAMLParser.Read(contents, s_byamlEncoding);
                        if (stageInfoByaml.RootNode == null || stageInfoByaml.RootNode.NodeType != BYAMLNodeType.Dictionary) break;

                        // Console.WriteLine(stageInfoByaml.RootNode.Value.GetType());

                        var rootInfoDict = stageInfoByaml.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;
                        rootInfoDict.TryGetValue("StageTimer", out BYAMLNode? timerN);
                        rootInfoDict.TryGetValue("StageTimerRestart", out BYAMLNode? timerRN);
                        rootInfoDict.TryGetValue("PowerUpItemNum", out BYAMLNode? pupN);
                        rootInfoDict.TryGetValue("FootPrint", out BYAMLNode? fprnt);

                        stage.StageParams.Timer = timerN?.GetValueAs<int>() ?? default;
                        stage.StageParams.RestartTimer = timerRN?.GetValueAs<int>() ?? -1;
                        stage.StageParams.MaxPowerUps = pupN?.GetValueAs<int>() ?? -1;

                        if (fprnt is not null)
                        {
                            stage.StageParams.FootPrint = new();
                            var d = fprnt.GetValueAs<BYAMLNode[]>()?[0].GetValueAs<Dictionary<string, BYAMLNode>>();

                            if (d is not null)
                            {
                                d.TryGetValue("AnimName", out BYAMLNode? aN);
                                d.TryGetValue("AnimType", out BYAMLNode? aT);

                                stage.StageParams.FootPrint.Material = d["Material"].GetValueAs<string>()!;
                                stage.StageParams.FootPrint.Model = d["Model"].GetValueAs<string>()!;
                                stage.StageParams.FootPrint.AnimName = aN?.GetValueAs<string>();
                                stage.StageParams.FootPrint.AnimType = aT?.GetValueAs<string>();
                            }
                        }
                        break;
                    case string s when s.Contains("CameraParam"):
                        if (!s.Contains($"{scenario}") && s != "CameraParam.byml")
                        {
                            stage.AddAdditionalFile(fileType, filename, contents);
                            break;
                        }
                        BYAML CamParamByaml = BYAMLParser.Read(contents, s_byamlEncoding);
                        if (CamParamByaml.RootNode == null || CamParamByaml.RootNode.NodeType != BYAMLNodeType.Dictionary) break;

                        // Console.WriteLine(stageInfoByaml.RootNode.Value.GetType());

                        var rootCamDict = CamParamByaml.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;
                        rootCamDict.TryGetValue("CameraParams", out BYAMLNode? cPar);
                        rootCamDict.TryGetValue("VisionParam", out BYAMLNode? vPar);

                        if (vPar is not null)
                        {
                            var vDict = vPar.GetValueAs<Dictionary<string, BYAMLNode>>();
                            stage.CameraParams.VisionParam = new(vDict!);
                        }
                        if (cPar is not null)
                        {
                            var cArr = cPar.GetValueAs<BYAMLNode[]>();
                            foreach (BYAMLNode nd in cArr!)
                            {
                                var cam = nd.GetValueAs<Dictionary<string, BYAMLNode>>();
                                if (cam is null) continue;
                                stage.CameraParams.Cameras.Add(new(cam));
                            }
                        }
                        break;
                    case string s when s.Contains("FogParam"):
                        if (!s.Contains($"{scenario}") && s != "FogParam.byml")
                        {
                            stage.AddAdditionalFile(fileType, filename, contents);
                            break;
                        }
                        // ignored if it's not in a design file
                        if (fileType != StageFileType.Design) break;
                        BYAML fogByaml = BYAMLParser.Read(contents, s_byamlEncoding);
                        if (fogByaml.RootNode == null || fogByaml.RootNode.NodeType != BYAMLNodeType.Dictionary) break;

                        // Console.WriteLine(fogByaml.RootNode.Value.GetType());

                        var rootFogDict = fogByaml.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                        rootFogDict.TryGetValue("ColorB", out BYAMLNode? fB);
                        rootFogDict.TryGetValue("ColorG", out BYAMLNode? fG);
                        rootFogDict.TryGetValue("ColorR", out BYAMLNode? fR);
                        rootFogDict.TryGetValue("Density", out BYAMLNode? fD);
                        rootFogDict.TryGetValue("FogType", out BYAMLNode? fT);
                        rootFogDict.TryGetValue("InterpFrame", out BYAMLNode? fI);
                        rootFogDict.TryGetValue("MaxDepth", out BYAMLNode? fMax);
                        rootFogDict.TryGetValue("MinDepth", out BYAMLNode? fMin);

                        StageFog MainFog = new()
                        {
                            Color = new(fR?.GetValueAs<float>() ?? 1, fG?.GetValueAs<float>() ?? 1, fB?.GetValueAs<float>() ?? 1),
                            Density = fD?.GetValueAs<float>() ?? 0,
                            FogType = Enum.Parse<StageFog.FogTypes>(fT?.GetValueAs<string>() ?? "FOG_UPDATER_TYPE_LINEAR"),
                            InterpFrame = fI?.GetValueAs<int>() ?? 0,
                            MaxDepth = fMax?.GetValueAs<float>() ?? 0,
                            MinDepth = fMin?.GetValueAs<float>() ?? 0,
                        };

                        stage.StageFogs[0] = MainFog;

                        rootFogDict.TryGetValue("FogAreas", out BYAMLNode? fAreas);
                        var fArr = fAreas?.GetValueAs<BYAMLNode[]>()!;

                        if (fArr != null)
                        {
                            int fogIdx = 0;
                            foreach (BYAMLNode node in fArr)
                            {
                                if (node.NodeType != BYAMLNodeType.Dictionary) continue;

                                var fogAreanode = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                                fogAreanode.TryGetValue("Area Id", out BYAMLNode? fId);
                                fogAreanode.TryGetValue("ColorB", out BYAMLNode? fAB);
                                fogAreanode.TryGetValue("ColorG", out BYAMLNode? aG);
                                fogAreanode.TryGetValue("ColorR", out BYAMLNode? fAR);
                                fogAreanode.TryGetValue("Density", out BYAMLNode? fAD);
                                fogAreanode.TryGetValue("FogType", out BYAMLNode? fAT);
                                fogAreanode.TryGetValue("InterpFrame", out BYAMLNode? fAI);
                                fogAreanode.TryGetValue("MaxDepth", out BYAMLNode? fAMax);
                                fogAreanode.TryGetValue("MinDepth", out BYAMLNode? fAMin);

                                stage.StageFogs.Add(new()
                                {
                                    Color = new(fAR?.GetValueAs<float>() ?? 1, aG?.GetValueAs<float>() ?? 1, fAB?.GetValueAs<float>() ?? 1),
                                    Density = fAD?.GetValueAs<float>() ?? 0,
                                    FogType = Enum.Parse<StageFog.FogTypes>(fAT?.GetValueAs<string>() ?? "FOG_UPDATER_TYPE_LINEAR"),
                                    InterpFrame = fAI?.GetValueAs<int>() ?? 0,
                                    MaxDepth = fAMax?.GetValueAs<float>() ?? 0,
                                    MinDepth = fAMin?.GetValueAs<float>() ?? 30000,
                                    AreaId = fId?.GetValueAs<int>() ?? fogIdx
                                });
                                fogIdx += 1;
                            }
                        }

                        break;

                    case string s when s.Contains("LightParam"):
                        if (!s.Contains($"{scenario}") && s != "LightParam.byml")
                        {
                            stage.AddAdditionalFile(fileType, filename, contents);
                            break;
                        }
                        // ignored if it's not in a design file
                        if (fileType != StageFileType.Design) break;
                        BYAML lightByaml = BYAMLParser.Read(contents, s_byamlEncoding);
                        if (lightByaml.RootNode == null || lightByaml.RootNode.NodeType != BYAMLNodeType.Dictionary) break;

                        // Console.WriteLine(lightByaml.RootNode.Value.GetType());

                        var rootLightDict = lightByaml.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                        rootLightDict.TryGetValue("Stage Light", out BYAMLNode? sL);
                        rootLightDict.TryGetValue("Stage Map Light", out BYAMLNode? sML);

                        stage.LightParams = new();

                        if (sML.NodeType == BYAMLNodeType.Dictionary)
                        {
                            var sMdict = sML.GetValueAs<Dictionary<string, BYAMLNode>>()!;
                            stage.LightParams.StageMapLight = new StageLight(sMdict);
                        }
                        var stageLightDict = sL.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                        if (stageLightDict != null)
                        {
                            stageLightDict.TryGetValue("Interpolate Frame", out BYAMLNode iFrame);
                            stageLightDict.TryGetValue("MapObj Light", out BYAMLNode MOL);
                            stageLightDict.TryGetValue("Name", out BYAMLNode lName);
                            stageLightDict.TryGetValue("Obj Light", out BYAMLNode OL);
                            stageLightDict.TryGetValue("Player Light", out BYAMLNode PL);

                            stage.LightParams.InterpolateFrame = iFrame?.GetValueAs<int>() ?? 10;
                            stage.LightParams.Name = lName?.GetValueAs<string>() ?? default!;
                            stage.LightParams.MapObjectLight = new(MOL.GetValueAs<Dictionary<string, BYAMLNode>>()!);
                            stage.LightParams.ObjectLight = new(OL.GetValueAs<Dictionary<string, BYAMLNode>>()!);
                            stage.LightParams.PlayerLight = new(PL.GetValueAs<Dictionary<string, BYAMLNode>>()!);
                        }

                        break;
                    case string s when s.Contains("ModelToMapLightNameTable"):
                        if (!s.Contains($"{scenario}") && s != "ModelToMapLightNameTable.byml")
                        {
                            stage.AddAdditionalFile(fileType, filename, contents);
                            break;
                        }
                        // ignored if it's not in a design file
                        if (fileType != StageFileType.Design) break;
                        BYAML mtm = BYAMLParser.Read(contents, s_byamlEncoding);
                        if (mtm.RootNode == null || mtm.RootNode.NodeType != BYAMLNodeType.Dictionary) break;

                        // Console.WriteLine(mtm.RootNode.Value.GetType());

                        var MTOMAP = mtm.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                        // Console.WriteLine(MTOMAP);
                        break;
                    case string s when s.Contains("AreaIdToLightNameTable"):
                        if (!s.Contains($"{scenario}") && s != "AreaIdToLightNameTable.byml")
                        {
                            stage.AddAdditionalFile(fileType, filename, contents);
                            break;
                        }
                        // ignored if it's not in a design file
                        if (fileType != StageFileType.Design) break;
                        BYAML AID = BYAMLParser.Read(contents, s_byamlEncoding);
                        if (AID.RootNode == null || AID.RootNode.NodeType != BYAMLNodeType.Dictionary) break;

                        // Console.WriteLine(AID.RootNode.Value.GetType());

                        var AIDNT = AID.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                        if (AIDNT.First().Value.Value is Dictionary<string, BYAMLNode>)
                        {
                            var arId = AIDNT.First().Value.GetValueAs<Dictionary<string, BYAMLNode>>();
                            foreach (string str in arId.Keys)
                            {
                                if (arId[str].NodeType is BYAMLNodeType.String)
                                {
                                    if (arId[str].GetValueAs<string>() == null)
                                        continue;
                                    stage.LightAreaNames.Add(int.Parse(str.Substring("LightAreaId ".Length)), arId[str].GetValueAs<string>());
                                }
                            }
                        }

                        // Console.WriteLine(AIDNT);
                        break;
                    default:
                        stage.AddAdditionalFile(fileType, filename, contents);
                        break;
                }
            }
        }
        return stage;
    }


    public Actor ReadActor(string name, GLTaskScheduler scheduler)
    {
        string path = Path.Join(_actorsPath, name + ".szs");

        // Return cached actor if valid (not modified externally)
        if (_cachedActors.TryGetValue(path, out Actor? cachedActor))
        {
            DateTime timestamp = File.GetLastWriteTime(path);

            if (_cachedActorsTimestamps.TryGetValue(path, out DateTime oldTimestamp) && oldTimestamp == timestamp)
                return cachedActor;
        }

        Actor actor = new(name);

        // The actor will result in an empty model if the narc is null (not found or
        // not a narc) or if the narc does not contain the properly-named cgfx file.

        if (!File.Exists(path))
            return actor;

        NARCFileSystem? narc = SZSWrapper.ReadFile(path);

        if (narc is null)
            return actor;

        bool found = narc.TryGetFile(name + ".bcmdl", out byte[] cgfx);

        if (!found)
            return actor;

        H3D h3D;

        try
        {
            using MemoryStream stream = new(cgfx);
            h3D = Gfx.OpenAsH3D(stream);
        }
        catch
        {
            Debug.Write($"The actor's cgfx could not be read ({name})", "Error");
            return actor;
        }

        foreach (H3DTexture texture in h3D.Textures)
        {
            scheduler.EnqueueGLTask(gl => actor.AddTexture(gl, texture));
        }

        foreach (H3DLUT lut in h3D.LUTs)
            foreach (H3DLUTSampler sampler in lut.Samplers)
            {
                scheduler.EnqueueGLTask(gl => actor.AddLUTTexture(gl, lut.Name, sampler));
            }

        foreach (H3DModel model in h3D.Models)
        {
            List<H3DMesh>[] meshLists =
            [
                model.MeshesLayer0, // Opaque layer
                model.MeshesLayer1, // Translucent layer
                model.MeshesLayer2, // Substractive layer
                model.MeshesLayer3 // Additive layer
            ];

            for (int i = 0; i < meshLists.Length; i++)
            {
                foreach (H3DMesh mesh in meshLists[i])
                {
                    actor.ForceModelNotEmpty();

                    // Obtain the mesh's material by its index.
                    int matIdx = mesh.MaterialIndex;
                    H3DMaterial material = model.Materials[matIdx];

                    // Obtain submesh culling.
                    int meshIdx = model.Meshes.IndexOf(mesh);

                    H3DSubMeshCulling? subMeshCulling = null;

                    if (model.SubMeshCullings.Count > meshIdx)
                        subMeshCulling = model.SubMeshCullings[meshIdx];

                    var skeleton = model.Skeleton;

                    H3DMeshLayer meshLayer = (H3DMeshLayer)i;

                    scheduler.EnqueueGLTask(gl =>
                        actor.AddMesh(gl, meshLayer, mesh, subMeshCulling, material, skeleton)
                    );

                    if (mesh.MetaData is not null)
                    {
                        for (int m = 0; m < mesh.MetaData.Count; m++)
                        {
                            if (mesh.MetaData[m].Name == "OBBox")
                                actor.AABB.BoundBox((H3DBoundingBox)mesh.MetaData[m].Values[0]!);
                        }
                    }
                }
            }
        }

        // Cache the actor
        _cachedActors.Add(path, actor);
        _cachedActorsTimestamps.Add(path, File.GetLastWriteTime(path));

        return actor;
    }

    public ReadOnlyDictionary<string, string> ReadCreatorClassNameTable()
    {
        if (_creatorClassNameTable is null)
        {
            _creatorClassNameTable = ReadAnyCreatorClassNameTable(_ccntPath);
        }

        return _creatorClassNameTable;
    }
    public static ReadOnlyDictionary<string, string> ReadAnyCreatorClassNameTable(string path)
    {
        NARCFileSystem? narc = SZSWrapper.ReadFile(path);

        Dictionary<string, string> dict = new();

        do
        {
            if (narc is null)
                break;

            byte[] tableData = narc.GetFile("CreatorClassNameTable.byml");

            if (tableData.Length == 0)
                break;

            BYAML byaml = BYAMLParser.Read(tableData, s_byamlEncoding);

            if (byaml.RootNode.NodeType != BYAMLNodeType.Array)
                break;

            BYAMLNode[] nodes = byaml.RootNode.GetValueAs<BYAMLNode[]>()!;

            foreach (BYAMLNode node in nodes)
            {
                if (node.NodeType != BYAMLNodeType.Dictionary)
                    continue;

                var entry = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                if (
                    !entry.TryGetValue("ObjectName", out BYAMLNode? objectName)
                    || !entry.TryGetValue("ClassName", out BYAMLNode? className)
                    || objectName.NodeType != BYAMLNodeType.String
                    || className.NodeType != BYAMLNodeType.String
                )
                    continue;

                dict.Add(objectName.GetValueAs<string>()!, className.GetValueAs<string>()!);
            }
        } while (false);

        return new(dict);

    }

    //String Dictionary Array
    public bool WriteCCNT(Dictionary<string, string> ccnt)
    {
        try
        {
            List<BYAMLNode> classObjectList = new();
            foreach (string obj in ccnt.Keys)
            {
                classObjectList.Add(new(
                    new Dictionary<string, BYAMLNode>
                    {
                        { "ClassName", new(ccnt[obj])},
                        { "ObjectName", new(obj)}
                    }
                    ));
            }
            BYAML byml = new(new(classObjectList.ToArray()), s_byamlEncoding, default);
            NARCFileSystem narcFS = new(new());
            byte[] bin = BYAMLParser.Write(byml);
            narcFS.AddFileRoot("CreatorClassNameTable.byml", bin);
            byte[] compressedFile = Yaz0Wrapper.Compress(NARCParser.Write(narcFS.ToNARC()));
            File.WriteAllBytes(_ccntPath, compressedFile);
            _creatorClassNameTable = new(ccnt); // Replace old ccnt with edited one
        }
        catch
        {
            return false;
        }
        return true;
    }

    public BgmTable ReadBgmTable()
    {
        if (_bgmTable is null)
        {
            NARCFileSystem? narc = SZSWrapper.ReadFile(Path.Join(_soundPath, "BgmTable.szs"));

            _bgmTable = new();

            if (narc is not null)
            {
                foreach ((string Name, byte[] Contents) f in narc.EnumerateFiles())
                    _bgmTable.AdditionalFiles.Add(f.Name, f.Contents);

                byte[] defaultListData = narc.GetFile("StageDefaultBgmList.byml");
                byte[] listData = narc.GetFile("StageBgmList.byml");
                BYAML defaultListByaml = BYAMLParser.Read(defaultListData, s_byamlEncoding);
                BYAML listByaml = BYAMLParser.Read(listData, s_byamlEncoding);

                if (defaultListByaml.RootNode.NodeType == BYAMLNodeType.Dictionary)
                {
                    var nd = defaultListByaml.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;
                    BYAMLNode[] defaultnodes = nd[nd.Keys.ToList()[0]].GetValueAs<BYAMLNode[]>()!;

                    foreach (BYAMLNode node in defaultnodes)
                    {
                        if (node.NodeType != BYAMLNodeType.Dictionary)
                            continue;

                        var dict = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                        dict.TryGetValue("BgmLabel", out BYAMLNode? lbl);
                        dict.TryGetValue("Scenario", out BYAMLNode? sc);
                        dict.TryGetValue("StageName", out BYAMLNode? name);
                        StageDefaultBgm bgm = new StageDefaultBgm()
                        {
                            Scenario = sc?.GetValueAs<int>() ?? 0,
                            BgmLabel = lbl?.GetValueAs<string>() ?? "",
                            StageName = name?.GetValueAs<string>() ?? "",
                        };
                        if (!_bgmTable.BgmFiles.Contains(bgm.BgmLabel)) _bgmTable.BgmFiles.Add(bgm.BgmLabel);

                        _bgmTable.StageDefaultBgmList.Add(bgm);
                    }
                }

                if (listByaml.RootNode.NodeType == BYAMLNodeType.Dictionary)
                {
                    var nd = listByaml.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                    foreach (string id in nd.Keys)
                    {
                        if (nd[id].NodeType != BYAMLNodeType.Array)
                            continue;

                        BYAMLNode[] nodes = nd[id].GetValueAs<BYAMLNode[]>()!;

                        if (id == "KindNumList")
                        {
                            foreach (BYAMLNode node in nodes)
                            {
                                if (node.NodeType != BYAMLNodeType.Dictionary)
                                    continue;

                                var dict = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                                _bgmTable.BgmTypes.Add(dict["Idx"].GetValueAs<int>(), dict["Kind"].GetValueAs<string>());
                            }
                        }
                        else
                        {
                            foreach (BYAMLNode node in nodes)
                            {
                                if (node.NodeType != BYAMLNodeType.Dictionary)
                                    continue;

                                var dict = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                                dict.TryGetValue("Scenario", out BYAMLNode? sc);
                                dict.TryGetValue("StageName", out BYAMLNode? name);

                                StageBgm bgm =
                                    new()
                                    {
                                        Scenario = sc?.GetValueAs<int>(),
                                        StageName = name?.GetValueAs<string>() ?? "",
                                    };

                                dict.TryGetValue("LineList", out BYAMLNode? list);

                                if (list?.NodeType == BYAMLNodeType.Dictionary)
                                {
                                    var d = list.GetValueAs<Dictionary<string, BYAMLNode>>();
                                    foreach (string item in d!.Keys)
                                    {
                                        List<KindDefine> e = new();

                                        if (d[item].NodeType != BYAMLNodeType.Array)
                                            continue;

                                        foreach (BYAMLNode bnode in d[item].GetValueAs<BYAMLNode[]>()!)
                                        {
                                            if (bnode.NodeType != BYAMLNodeType.Dictionary)
                                                continue;

                                            KindDefine k = new();
                                            k.Kind = bnode.GetValueAs<Dictionary<string, BYAMLNode>>()["Kind"].GetValueAs<string>();
                                            k.Label = bnode.GetValueAs<Dictionary<string, BYAMLNode>>()["Label"].GetValueAs<string>();
                                            if (!_bgmTable.BgmFiles.Contains(k.Label)) _bgmTable.BgmFiles.Add(k.Label);

                                            e.Add(k);
                                        }

                                        bgm.LineList.Add(item, e);
                                    }
                                }

                                _bgmTable.StageBgmList.Add(bgm);
                            }
                        }
                    }
                }
            }

            if (Directory.Exists(Path.Join(_soundPath, "stream")))
            {
                var fls = Directory.EnumerateFiles(Path.Join(_soundPath, "stream")).Select(x => Path.GetFileNameWithoutExtension(x));
                foreach (string sng in fls)
                {
                    if (!_bgmTable.BgmFiles.Contains(sng)) _bgmTable.BgmFiles.Add(sng);
                }
            }
            _bgmTable.BgmFiles.Sort();
        }

        //var query = (from s in _bgmTable.StageDefaultBgmList where s.Scenario == 1 select s).ToList();
        //_bgmTable.StageDefaultBgmList.Where(x => x.StageName == "FirstStage").ToList();
        return _bgmTable;
    }

    /// <summary>
    /// Reads the contents of ObjectData/GameSystemDataTable.szs
    /// </summary>
    /// <returns>SystemDataTable</returns>
    public SystemDataTable ReadGSDTable()
    {
        if (_GSDTable is null)
        {
            NARCFileSystem? narc = SZSWrapper.ReadFile(Path.Join(_actorsPath, "GameSystemDataTable.szs"));

            _GSDTable = new();
            if (narc is not null)
            {
                byte[] courselistData = narc.GetFile("CourseList.byml");
                BYAML courselistByml = BYAMLParser.Read(courselistData, s_byamlEncoding);

                if (courselistByml.RootNode.NodeType == BYAMLNodeType.Dictionary)
                {
                    var nd = courselistByml.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;
                    BYAMLNode[] worldnodes = nd[nd.Keys.ToList()[0]].GetValueAs<BYAMLNode[]>()!;
                    foreach (BYAMLNode wnode in worldnodes)
                    {
                        if (wnode.NodeType != BYAMLNodeType.Dictionary)
                            continue;

                        SystemDataTable.WorldDefine world = new();
                        var wdict = wnode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                        if (wdict.TryGetValue("Type", out BYAMLNode? wtp))
                            world.WorldType = Enum.Parse<SystemDataTable.WorldTypes>(wtp?.GetValueAs<string>()!);

                        BYAMLNode[] stagenodes = wdict["Course"].GetValueAs<BYAMLNode[]>()!;

                        int lvlNum = 1;
                        foreach (BYAMLNode snode in stagenodes)
                        {
                            if (snode.NodeType != BYAMLNodeType.Dictionary)
                                continue;

                            var dict = snode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                            dict.TryGetValue("Type", out BYAMLNode? tp);
                            dict.TryGetValue("Miniature", out BYAMLNode? min);
                            dict.TryGetValue("Scenario", out BYAMLNode? sc);
                            dict.TryGetValue("Stage", out BYAMLNode? st);
                            dict.TryGetValue("CoinCollectNum", out BYAMLNode? ccn);

                            SystemDataTable.StageDefine lvl = new()
                            {
                                StageType = (SystemDataTable.StageTypes)Enum.Parse(typeof(SystemDataTable.StageTypes), tp.GetValueAs<string>()),
                                Scenario = sc?.GetValueAs<int>() ?? -1,
                                Miniature = min?.GetValueAs<string>() ?? "",
                                Stage = st?.GetValueAs<string>() ?? "",
                                CollectCoinNum = ccn?.GetValueAs<int>() ?? -1,
                            };

                            if (lvl.StageType != SystemDataTable.StageTypes.Dokan && lvl.StageType != SystemDataTable.StageTypes.Empty)
                            {
                                lvl.StageNumber = lvlNum;
                                lvlNum += 1;
                            }

                            world.StageList.Add(lvl);
                        }
                        _GSDTable.WorldList.Add(world);
                    }
                }
            }
        }

        return _GSDTable;
    }

    public Dictionary<string, LightArea> GetPossibleLightAreas()
    {
        if (_lightAreas == null)
        {
            NARCFileSystem? narc = SZSWrapper.ReadFile(Path.Join(_actorsPath, "LightDataArea.szs"));
            _lightAreas = new();
            if (narc != null)
            {
                var v = narc.EnumerateFiles("/");
                foreach ((string name, byte[] contents) in v)
                {
                    BYAML LightAreaBYML = BYAMLParser.Read(contents, s_byamlEncoding);
                    var rootLightAreaDict = LightAreaBYML.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                    if (rootLightAreaDict != null)
                    {
                        LightArea lA = new();
                        rootLightAreaDict.TryGetValue("Interpolate Frame", out BYAMLNode iFrame);
                        rootLightAreaDict.TryGetValue("MapObj Light", out BYAMLNode MOL);
                        rootLightAreaDict.TryGetValue("Name", out BYAMLNode lName);
                        rootLightAreaDict.TryGetValue("Obj Light", out BYAMLNode OL);
                        rootLightAreaDict.TryGetValue("Player Light", out BYAMLNode PL);

                        lA.InterpolateFrame = iFrame?.GetValueAs<int>() ?? 10;
                        lA.Name = lName?.GetValueAs<string>() ?? default!;
                        lA.MapObjectLight = new(MOL.GetValueAs<Dictionary<string, BYAMLNode>>()!);
                        lA.ObjectLight = new(OL.GetValueAs<Dictionary<string, BYAMLNode>>()!);
                        lA.PlayerLight = new(PL.GetValueAs<Dictionary<string, BYAMLNode>>()!);
                        _lightAreas.Add(lA.Name, lA);
                    }
                }
            }
        }
        return _lightAreas;
    }
    public NARCFileSystem GetShader()
    {
        return SZSWrapper.ReadFile(Path.Join(_actorsPath, "Shader.szs"))!;
    }
    private static IEnumerable<StageObj> ProcessStageObjs(BYAML byaml, StageFileType fileType)
    {
        if (byaml.RootNode.NodeType is not BYAMLNodeType.Dictionary)
            throw new("The given BYAML was not formatted correctly.");

        var rootDict = byaml.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

        // Rails:

        Dictionary<BYAMLNode, RailObj> processedRails = new();

        BYAMLNode allRailInfosNode = rootDict["AllRailInfos"];
        var allRailInfos = allRailInfosNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

        //Debug.Assert(allRailInfos.Count <= 1);

        allRailInfos.TryGetValue("RailInfo", out BYAMLNode? railInfoNode);
        var railInfos = railInfoNode?.GetValueAs<BYAMLNode[]>() ?? Array.Empty<BYAMLNode>();

        foreach (BYAMLNode node in railInfos)
        {
            if (node.NodeType != BYAMLNodeType.Dictionary)
                continue;

            RailObj railObj = ReadRailObj(node, fileType);

            processedRails.Add(node, railObj);

            yield return railObj;
        }

        // All others:

        BYAMLNode allInfosNode = rootDict["AllInfos"];
        var allInfos = allInfosNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

        foreach (KeyValuePair<string, BYAMLNode> info in allInfos)
        {
            if (info.Value.NodeType != BYAMLNodeType.Array)
                continue;

            BYAMLNode[] array = info.Value.GetValueAs<BYAMLNode[]>()!;

            foreach (BYAMLNode node in array)
            {
                if (node.NodeType != BYAMLNodeType.Dictionary)
                    continue;

                StageObjType objType = info.Key switch
                {
                    "AreaObjInfo" => StageObjType.Area,
                    "CameraAreaInfo" => StageObjType.CameraArea,
                    "GoalObjInfo" => StageObjType.Goal,
                    "ObjInfo" => StageObjType.Regular,
                    "StartEventObjInfo" => StageObjType.StartEvent,
                    "StartInfo" => StageObjType.Start,
                    "DemoSceneObjInfo" => StageObjType.DemoScene,
                    _ => throw new NotSupportedException("Unknown stage obj type found.")
                };

                StageObj stageObj = ReadStageObj(node, processedRails, objType, fileType);

                yield return stageObj;
            }
        }
    }

    private static RailObj ReadRailObj(BYAMLNode node, StageFileType fileType)
    {
        var dict = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

        dict.TryGetValue("l_id", out BYAMLNode? id);
        dict.TryGetValue("name", out BYAMLNode? name);
        dict.TryGetValue("LayerName", out BYAMLNode? layerName);
        dict.TryGetValue("no", out BYAMLNode? railNo);
        dict.TryGetValue("closed", out BYAMLNode? railClosed);
        dict.TryGetValue("type", out BYAMLNode? railType);

        dict.TryGetValue("num_pnt", out BYAMLNode? pointCount);
        dict.TryGetValue("Points", out BYAMLNode? railPointsNode);

        RailPointType pointType = railType?.GetValueAs<string>() switch
        {
            "Bezier" => RailPointType.Bezier,
            "Linear" => RailPointType.Linear,
            _ => throw new NotImplementedException("Unknown rail point type.")
        };

        //Debug.Assert(pointType == RailPointType.Bezier);
        RailObj railObj =
            new()
            {
                PointType = pointType,
                Type = StageObjType.Rail,
                FileType = fileType,
                Name = name?.GetValueAs<string>() ?? "RailStageObj",
                Layer = layerName?.GetValueAs<string>() ?? "共通",
                RailNo = railNo?.GetValueAs<int>() ?? 0,
                Closed = railClosed?.GetValueAs<string>() == "CLOSE",
                Properties = dict.Where(i =>
                        i.Key != "no"
                        && i.Key != "closed"
                        && i.Key != "type"
                        && i.Key != "num_pnt"
                        && i.Key != "Points"
                        && i.Key != "name"
                        && i.Key != "LayerName"
                        && i.Key != "l_id"
                    )
                    .ToDictionary(i => i.Key, i => i.Value.Value)
            };

        // Rail point reading:

        BYAMLNode[] railPointNodes = railPointsNode?.GetValueAs<BYAMLNode[]>() ?? Array.Empty<BYAMLNode>();

        //Debug.Assert(pointCount?.GetValueAs<int>() == railPointNodes.Length);

        for (int i = 0; i < railPointNodes.Length; i++)
        {
            BYAMLNode railPointNode = railPointNodes[i];

            if (railPointNode?.NodeType != BYAMLNodeType.Dictionary)
                continue;

            var railPointDict = railPointNode?.GetValueAs<Dictionary<string, BYAMLNode>>()!;

            railPointDict.TryGetValue("id", out BYAMLNode? pointID);

            railPointDict.TryGetValue("pnt0_x", out BYAMLNode? pnt0X);
            railPointDict.TryGetValue("pnt0_y", out BYAMLNode? pnt0Y);
            railPointDict.TryGetValue("pnt0_z", out BYAMLNode? pnt0Z);

            railPointDict.TryGetValue("pnt1_x", out BYAMLNode? pnt1X);
            railPointDict.TryGetValue("pnt1_y", out BYAMLNode? pnt1Y);
            railPointDict.TryGetValue("pnt1_z", out BYAMLNode? pnt1Z);

            railPointDict.TryGetValue("pnt2_x", out BYAMLNode? pnt2X);
            railPointDict.TryGetValue("pnt2_y", out BYAMLNode? pnt2Y);
            railPointDict.TryGetValue("pnt2_z", out BYAMLNode? pnt2Z);

            switch (pointType)
            {
                case RailPointType.Bezier:
                    railObj.Points.Add(
                        new RailPointBezier()
                        {
                            ID = pointID?.GetValueAs<int>() ?? i,
                            Point0Trans = new(
                                pnt0X?.GetValueAs<float>() ?? 0,
                                pnt0Y?.GetValueAs<float>() ?? 0,
                                pnt0Z?.GetValueAs<float>() ?? 0
                            ),
                            Point1Trans = new(
                                pnt1X?.GetValueAs<float>() ?? 0,
                                pnt1Y?.GetValueAs<float>() ?? 0,
                                pnt1Z?.GetValueAs<float>() ?? 0
                            ),
                            Point2Trans = new(
                                pnt2X?.GetValueAs<float>() ?? 0,
                                pnt2Y?.GetValueAs<float>() ?? 0,
                                pnt2Z?.GetValueAs<float>() ?? 0
                            ),
                            Properties = railPointDict
                                .Where(i =>
                                    i.Key != "pnt0_x"
                                    && i.Key != "pnt0_y"
                                    && i.Key != "pnt0_z"
                                    && i.Key != "pnt1_x"
                                    && i.Key != "pnt1_y"
                                    && i.Key != "pnt1_z"
                                    && i.Key != "pnt2_x"
                                    && i.Key != "pnt2_y"
                                    && i.Key != "pnt2_z"
                                    && i.Key != "id"
                                )
                                .ToDictionary(i => i.Key, i => i.Value.Value)
                        }
                    );
                    break;

                case RailPointType.Linear:
                    railObj.Points.Add(
                        new RailPointLinear()
                        {
                            ID = pointID?.GetValueAs<int>() ?? i,
                            Translation = new(
                                pnt0X?.GetValueAs<float>() ?? 0,
                                pnt0Y?.GetValueAs<float>() ?? 0,
                                pnt0Z?.GetValueAs<float>() ?? 0
                            ),
                            Properties = railPointDict
                                .Where(i =>
                                    i.Key != "pnt0_x" && i.Key != "pnt0_y" && i.Key != "pnt0_z" && i.Key != "id"
                                )
                                .ToDictionary(i => i.Key, i => i.Value.Value)
                        }
                    );
                    break;

                default:
                    throw new NotImplementedException("The given rail point type is not supported.");
            }
        }

        return railObj;
    }

    private static StageObj ReadStageObj(
        BYAMLNode node,
        Dictionary<BYAMLNode, RailObj> processedRails,
        StageObjType objType,
        StageFileType fileType,
        StageObj? parent = null
    )
    {
        var dict = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

        dict.TryGetValue("l_id", out BYAMLNode? id);
        dict.TryGetValue("name", out BYAMLNode? name);
        dict.TryGetValue("ClassName", out BYAMLNode? className);
        dict.TryGetValue("LayerName", out BYAMLNode? layerName);

        dict.TryGetValue("pos_x", out BYAMLNode? posX);
        dict.TryGetValue("pos_y", out BYAMLNode? posY);
        dict.TryGetValue("pos_z", out BYAMLNode? posZ);

        dict.TryGetValue("dir_x", out BYAMLNode? dirX);
        dict.TryGetValue("dir_y", out BYAMLNode? dirY);
        dict.TryGetValue("dir_z", out BYAMLNode? dirZ);

        dict.TryGetValue("scale_x", out BYAMLNode? scaleX);
        dict.TryGetValue("scale_y", out BYAMLNode? scaleY);
        dict.TryGetValue("scale_z", out BYAMLNode? scaleZ);

        dict.TryGetValue("GenerateParent", out BYAMLNode? generateParent);
        dict.TryGetValue("AreaParent", out BYAMLNode? areaParent);

        dict.TryGetValue("Rail", out BYAMLNode? rail);

        dict.TryGetValue("AreaChildren", out BYAMLNode? areaChildren);
        dict.TryGetValue("GenerateChildren", out BYAMLNode? generateChildren);

        dict.TryGetValue("SwitchA", out BYAMLNode? switchA);
        dict.TryGetValue("SwitchB", out BYAMLNode? switchB);
        dict.TryGetValue("SwitchDeadOn", out BYAMLNode? switchDead);
        dict.TryGetValue("SwitchAppear", out BYAMLNode? switchAppear);
        dict.TryGetValue("SwitchKill", out BYAMLNode? switchK);

        dict.TryGetValue("ViewId", out BYAMLNode? vId);
        dict.TryGetValue("CameraId", out BYAMLNode? camId);
        dict.TryGetValue("ClippingGroupId", out BYAMLNode? cgId);

        RailObj? railObj = null;

        if (rail is not null && rail.NodeType == BYAMLNodeType.Dictionary)
        {
            processedRails.TryGetValue(rail, out railObj);
            if (railObj is null)
            {
                var rO = ReadRailObj(rail, fileType);
                foreach (RailObj r in processedRails.Values)
                {
                    if (rO == r)
                        railObj = r;
                }

                if (railObj is null)
                    throw new(
                        "The rail inside "
                            + name?.GetValueAs<string>()
                            + " is not referenced in AllRailInfos, please report this issue."
                    );
            }
        }
        List<StageObj> children = new();

        StageObj stageObj =
            new(parent)
            {
                Type = objType,
                FileType = fileType,
                Translation = new(
                    posX?.GetValueAs<float>() ?? 0,
                    posY?.GetValueAs<float>() ?? 0,
                    posZ?.GetValueAs<float>() ?? 0
                ),
                Rotation = new(
                    dirX?.GetValueAs<float>() ?? 0,
                    dirY?.GetValueAs<float>() ?? 0,
                    dirZ?.GetValueAs<float>() ?? 0
                ),
                Scale = new(
                    scaleX?.GetValueAs<float>() ?? 1,
                    scaleY?.GetValueAs<float>() ?? 1,
                    scaleZ?.GetValueAs<float>() ?? 1
                ),
                Name = name?.GetValueAs<string>() ?? "StageObj",
                ClassName = className?.GetValueAs<string>() ?? null,
                Layer = layerName?.GetValueAs<string>() ?? "共通",
                SwitchA = switchA != null ? switchA.GetValueAs<int>() : -1,
                SwitchB = switchB != null ? switchB.GetValueAs<int>() : -1,
                SwitchAppear = switchAppear != null ? switchAppear.GetValueAs<int>() : -1,
                SwitchDeadOn = switchDead != null ? switchDead.GetValueAs<int>() : -1,
                SwitchKill = switchK != null ? switchK.GetValueAs<int>() : -1,
                ViewId = vId != null ? vId.GetValueAs<int>() : -1,
                CameraId = camId != null ? camId.GetValueAs<int>() : -1,
                ClippingGroupId = cgId != null ? cgId.GetValueAs<int>() : -1,
                Rail = railObj,
                Children = children,
                Properties = dict.Where(i =>
                        i.Key != "pos_x"
                        && i.Key != "pos_y"
                        && i.Key != "pos_z"
                        && i.Key != "dir_x"
                        && i.Key != "dir_y"
                        && i.Key != "dir_z"
                        && i.Key != "scale_x"
                        && i.Key != "scale_y"
                        && i.Key != "scale_z"
                        && i.Key != "name"
                        && i.Key != "LayerName"
                        && i.Key != "l_id"
                        && i.Key != "Rail"
                        && i.Key != "AreaChildren"
                        && i.Key != "GenerateChildren"
                        && i.Key != "GenerateParent"
                        && i.Key != "AreaParent"
                        && i.Key != "CameraId"
                        && i.Key != "ClippingGroupId"
                        && i.Key != "ViewId"
                        && i.Key != "ClassName"
                        && !i.Key.Contains("Switch")
                    )
                    .ToDictionary(i => i.Key, i => i.Value.Value)
            };

        #region Children

        if (areaChildren is not null && areaChildren.NodeType == BYAMLNodeType.Array)
        {
            var childrenArray = areaChildren.GetValueAs<BYAMLNode[]>()!;

            foreach (BYAMLNode childNode in childrenArray)
            {
                if (childNode.NodeType != BYAMLNodeType.Dictionary)
                    continue;

                StageObj child = ReadStageObj(
                    childNode,
                    processedRails,
                    StageObjType.AreaChild,
                    fileType,
                    parent: stageObj
                );

                children.Add(child);
            }
        }

        if (generateChildren is not null && generateChildren.NodeType == BYAMLNodeType.Array)
        {
            var childrenArray = generateChildren.GetValueAs<BYAMLNode[]>()!;

            foreach (BYAMLNode childNode in childrenArray)
            {
                if (childNode.NodeType != BYAMLNodeType.Dictionary)
                    continue;

                StageObj child = ReadStageObj(
                    childNode,
                    processedRails,
                    StageObjType.Child,
                    fileType,
                    parent: stageObj
                );

                children.Add(child);
            }
        }

        #endregion

        return stageObj;
    }

    #endregion

    #region Stage Writing

    public bool WriteStage(Stage stage, bool _useClassNames)
    {
        Console.WriteLine(Path.Join(_stagesPath, $"{stage.Name}Design{stage.Scenario}"));
        int currentId = 0;
        stage.UserPath = _stagesPath + Path.DirectorySeparatorChar + stage.Name + stage.Scenario;
        //bool saveBackup = true;
        // check objects in each stage type (map design sound), then on each type we check each Infos list
        Dictionary<StageFileType, string> paths =
            new()
            {
                { StageFileType.Design, Path.Join(_stagesPath, $"{stage.Name}Design{stage.Scenario}.szs") },
                { StageFileType.Map, Path.Join(_stagesPath, $"{stage.Name}Map{stage.Scenario}.szs") },
                { StageFileType.Sound, Path.Join(_stagesPath, $"{stage.Name}Sound{stage.Scenario}.szs") }
            };

        // check design -> map -> sound
        foreach (StageFileType StageType in Enum.GetValues<StageFileType>())
        {
            Dictionary<string, BYAMLNode> rootdict = new();
            BYAMLNode root = new(rootdict);
            BYAML file = new(root, s_byamlEncoding, default);
            Dictionary<string, BYAMLNode> dict = new();
            StageFile st = stage.GetStageFile(StageType);

            if (st.IsEmpty())
            {
                if (StageType == StageFileType.Sound)
                    continue;
            }
            else
            {
                dict.Add("AllInfos", new(BYAMLNodeType.Dictionary));
                dict.Add("AllRailInfos", new(BYAMLNodeType.Dictionary));
                dict.Add("LayerInfos", new(BYAMLNodeType.Array));

                Dictionary<string, BYAMLNode> allInfosDict = new(); // dictionary of arrays of dictionaries
                                                                    // design , map, sound StageData.byaml
                currentId = 0;
                Dictionary<string, BYAMLNode> allRailDict;
                dict["AllRailInfos"].TryGetValueAs(out allRailDict);

                allRailDict.Add("RailInfo", new(BYAMLNodeType.Array));
                List<BYAMLNode> railInfo = new();//= railDict["RailInfo"].GetValueAs<BYAMLNode[]>();
                Dictionary<RailObj, BYAMLNode> railObjNodes = new();
                foreach (RailObj rail in st.GetRailInfos())
                {
                    var nodeRail = WriteRail(rail, currentId);
                    railInfo.Add(nodeRail);
                    railObjNodes.Add(rail, nodeRail);
                    currentId++;
                }
                allRailDict["RailInfo"].Value = railInfo.ToArray();
                dict["AllRailInfos"].Value = allRailDict;

                //List<BYAMLNode> layerInfos = new();
                //Dictionary<string, BYAMLNode> layerInfosNames = new(); // "Common", BYAML_Array[] -> "ObjInfo", "CameraAreaInfo"...
                Dictionary<string, Dictionary<string, List<BYAMLNode>>> layerInfosList = new(); // "Common", List_Array[] -> "ObjInfo", "CameraAreaInfo"...

                //currentLayer
                currentId = 0;

                foreach (StageObjType Infos in Enum.GetValues<StageObjType>())
                {
                    if (Infos != StageObjType.Rail && Infos != StageObjType.Child && Infos != StageObjType.AreaChild)
                    {
                        /* AllInfos (dictionary)
                (Dict entry) -> "CameraAreaInfo"[] (array)
                            (array entry 0) -> CameraArea (dictionary)
                            (array entry 1) -> CameraArea (dictionary)
                            (array entry 2) -> CameraArea (dictionary)
                (Dict entry) -> "StartInfo"[] (array)
                            (array entry) -> Mario (dictionary)
                        */

                        List<StageObj> objs = st.GetObjInfos(Infos);
                        if (!objs.Any()) continue;
                        List<BYAMLNode> currentInfosList = new();
                        foreach (StageObj currentObj in objs)
                        {
                            if (currentObj.Parent != null) continue;
                            string scenarioLayer = "";
                            int cId = currentId;
                            if (currentObj.Layer.Contains("シナリオ"))
                            {
                                scenarioLayer = "Scenario";
                                if (currentObj.Layer.Contains("＆") || currentObj.Layer.Contains("&"))
                                {
                                    scenarioLayer += currentObj.Layer.ElementAt(4) + "And" + currentObj.Layer.ElementAt(6);
                                }
                                else
                                {
                                    scenarioLayer += currentObj.Layer.ElementAt(4);
                                    // add to ScenarioX
                                }
                            }
                            else if (currentObj.Layer.Contains("共通")) // common / commons
                            {
                                scenarioLayer = "Common";
                                if (currentObj.Layer.Contains("サブ"))
                                {
                                    scenarioLayer += "Sub";
                                    // add to CommonSub
                                }
                            }
                            else if (currentObj.Layer == string.Empty)
                            { // if we have an incorrect layer we default to Common
                                scenarioLayer = "Common";
                                currentObj.Layer = "共通";
                            }
                            else // if the layer is valid but not predefined we let it save as is
                            {
                                scenarioLayer = currentObj.Layer;
                            }
                            var scObj = WriteSceneObjects(currentObj, ref currentId, railObjNodes, useClassNames: _useClassNames);
                            if (scenarioLayer != "")
                            {
                                if (!layerInfosList.Keys.Contains(scenarioLayer))
                                {
                                    BYAMLNode n = MakeNewLayerInfos(scenarioLayer);
                                    layerInfosList.Add(scenarioLayer, new());
                                    layerInfosList[scenarioLayer].Add(InfosToString(Infos), new());
                                }
                                else if (!layerInfosList[scenarioLayer].Keys.Contains(InfosToString(Infos)))
                                {
                                    layerInfosList[scenarioLayer].Add(InfosToString(Infos), new());
                                }
                            }
                            layerInfosList[scenarioLayer][InfosToString(Infos)].Add(scObj);
                            currentInfosList.Add(scObj);
                            currentId += 1;
                        }
                        BYAMLNode currentInfos = new(BYAMLNodeType.Array, currentInfosList.ToArray());
                        string ObjectType = InfosToString(Infos);
                        allInfosDict.Add(ObjectType, currentInfos);
                    }
                }

                dict["AllInfos"].Value = allInfosDict.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary();
                BYAMLNode[] layInfos = GetLayerInfos(layerInfosList.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary());
                dict["LayerInfos"].Value = layInfos;
                rootdict = dict;
                root.Value = rootdict;
                file.RootNode = root;
            }

            NARCFileSystem narcFS = new(new());
            byte[] binFile = BYAMLParser.Write(file);
            SortedDictionary<string, byte[]> files = new(); 
            foreach (var (key, value) in st.EnumerateAdditionalFiles())
            {
                files.Add(key, value);
            }
            if (st.StageFileType == StageFileType.Map)
            {
                var stageInfoBYML = MakeStageInfo(stage);
                if (stageInfoBYML != null)
                    files.Add("StageInfo" + stage.Scenario + ".byml", BYAMLParser.Write((BYAML)stageInfoBYML));                   
                if (stage.CameraParams.Cameras.Count > 0)
                    files.Add("CameraParam.byml", BYAMLParser.Write(MakeCameraParam(stage)));
            }
            if (st.StageFileType == StageFileType.Design)
            {
                if (stage.StageFogs.Count > 0)
                    files.Add("FogParam" + stage.Scenario + ".byml", BYAMLParser.Write(MakeFogParam(stage)));
                if (stage.LightParams != null)
                    files.Add("LightParam" + stage.Scenario + ".byml", BYAMLParser.Write(new(stage.LightParams.GetNodes(), s_byamlEncoding, default)));
                if (stage.LightAreaNames.Count > 0)
                {
                    files.Add("AreaIdToLightNameTable" + stage.Scenario + ".byml", BYAMLParser.Write(MakeLightAreas(stage)));
                }
            }
            if (!st.IsEmpty()) files.Add("StageData.byml", binFile);

            foreach (string f in files.Keys)
            {
                narcFS.AddFileRoot(f, files[f]);
            }
            //if (saveBackup)
            //{
            //    if (File.Exists(paths[StageType]))
            //        File.Copy(paths[StageType], Path.Join(paths[StageType] + ".BACKUP"), true);
            //    if (File.Exists(Path.Join(_stagesPath, $"{stage.Name}{StageType}{stage.Scenario}.narc")))
            //        File.Copy(Path.Join(_stagesPath, $"{stage.Name}{StageType}{stage.Scenario}.narc"), Path.Join(paths[StageType] + ".narc.BACKUP"), true);
            //    if (File.Exists(paths[StageType] + "_StageData.byml"))
            //        File.Copy(paths[StageType] + "_StageData.byml", Path.Join(paths[StageType] + "_StageData.byml" + ".BACKUP"), true);
            //}
            //byte[] uncompressed = NARCParser.Write(narcFS.ToNARC());
            byte[] compressedFile = Yaz0Wrapper.Compress(NARCParser.Write(narcFS.ToNARC()));
            //File.WriteAllBytes(Path.Join(paths[StageType] + "_StageData.byml"), binFile);
            File.WriteAllBytes(paths[StageType], compressedFile);
            //File.WriteAllBytes(Path.Join(_stagesPath, $"{stage.Name}{StageType}{stage.Scenario}.narc"), uncompressed);
        }
        return true;
    }

    private BYAML MakeFogParam(Stage stage)
    {
        BYAMLNode root;
        Dictionary<string, BYAMLNode> rd = new()
        {
            { "ColorB", new(BYAMLNodeType.Float, stage.StageFogs[0].Color.Z) },
            { "ColorG", new(BYAMLNodeType.Float, stage.StageFogs[0].Color.Y) },
            { "ColorR", new(BYAMLNodeType.Float, stage.StageFogs[0].Color.X) },
            { "Density", new(BYAMLNodeType.Float, stage.StageFogs[0].Density) },
            { "InterpFrame", new(BYAMLNodeType.Int, stage.StageFogs[0].InterpFrame) },
            { "MaxDepth", new(BYAMLNodeType.Float, stage.StageFogs[0].MaxDepth) },
            { "MinDepth", new(BYAMLNodeType.Float, stage.StageFogs[0].MinDepth) },
            { "FogType", new(BYAMLNodeType.String, stage.StageFogs[0].FogType.ToString()) }
        };

        if (stage.StageFogs.Count > 1)
        {
            var stagefogs = new List<BYAMLNode>();
            for (int i = 1; i < stage.StageFogs.Count; i++)
            {
                stagefogs.Add(stage.StageFogs[i].GetNodes());
            }
            rd.Add("FogAreas", new(BYAMLNodeType.Array, stagefogs.ToArray()));
        }
        root = new(rd);
        BYAML ret = new(root, s_byamlEncoding, default);
        ret.RootNode = root;
        return ret;
    }
    private BYAML MakeCameraParam(Stage stage)
    {
        BYAMLNode root;
        root = stage.CameraParams.GetNodes();
        BYAML ret = new(root, s_byamlEncoding, default);
        ret.RootNode = root;
        return ret;
    }
    private BYAML? MakeStageInfo(Stage stage)
    {
        BYAMLNode root;
        Dictionary<string, BYAMLNode> rd = new();
        if (stage.StageParams.Timer > 0)
        {
            rd.Add("StageTimer", new(BYAMLNodeType.Int, stage.StageParams.Timer));
        }
        if (stage.StageParams.RestartTimer > 0)
        {
            rd.Add("StageTimerRestart", new(BYAMLNodeType.Int, stage.StageParams.RestartTimer));
        }
        if (stage.StageParams.MaxPowerUps > 0)
        {
            rd.Add("PowerUpItemNum", new(BYAMLNodeType.Int, stage.StageParams.MaxPowerUps));
        }
        if (stage.StageParams.FootPrint != null) //Array of dictionaries for each footprint
        {
            var dc = new Dictionary<string, BYAMLNode>
            {
                {"Material", new(BYAMLNodeType.String, stage.StageParams.FootPrint.Material)},
                {"Model", new(BYAMLNodeType.String, stage.StageParams.FootPrint.Model)},
            };
            if (stage.StageParams.FootPrint.AnimName != null)
                dc.Add("AnimName", new(BYAMLNodeType.String, stage.StageParams.FootPrint.AnimName));
            if (stage.StageParams.FootPrint.AnimType != null)
                dc.Add("AnimType", new(BYAMLNodeType.String, stage.StageParams.FootPrint.AnimType));

            BYAMLNode[] arr = [new(dc)];
            rd.Add("FootPrint", new(BYAMLNodeType.Array, arr, false));
        }
        if (rd.Count == 0)
            return null;

        root = new(rd);
        BYAML ret = new(root, s_byamlEncoding, default);
        ret.RootNode = root;
        return ret;
    }

    private BYAML MakeLightAreas(Stage stage)
    {
        BYAMLNode root;
        Dictionary<string, BYAMLNode> rd = new();
        foreach (int id in stage.LightAreaNames.Keys)
        {
            rd.Add("LightAreaId " + id.ToString("0000"), new(BYAMLNodeType.String, stage.LightAreaNames[id]));
        }

        Dictionary<string, BYAMLNode> rTable = new()
        {
            { "AreaId LightName Table", new(rd) }
        };
        root = new(rTable);
        BYAML ret = new(root, s_byamlEncoding, default);
        ret.RootNode = root;
        return ret;
    }

    private BYAMLNode MakeNewLayerInfos(string layerName, BYAMLNode layerInfosDict = null)
    {
        BYAMLNode kvp0;
        if (layerInfosDict is null)
            kvp0 = new(BYAMLNodeType.Dictionary);
        else
            kvp0 = new(layerInfosDict);
        BYAMLNode kvp1 = new(BYAMLNodeType.String, layerName);
        Dictionary<string, BYAMLNode> newdict = new() { { "Infos", kvp0 }, { "LayerName", kvp1 } };
        return new(newdict);
    }

    private BYAMLNode[] GetLayerInfos(Dictionary<string, Dictionary<string, List<BYAMLNode>>> layerInfosList)
    {
        List<BYAMLNode> retArray = new();
        foreach (string key in layerInfosList.Keys)
        {
            Dictionary<string, BYAMLNode> currentInfo = new();
            List<string> sl = layerInfosList[key].Keys.ToList();

            foreach (string infodict in sl)
            {
                currentInfo.Add(infodict, new(BYAMLNodeType.Array, layerInfosList[key][infodict].ToArray()));
            }

            BYAMLNode kvp0 = new(currentInfo);
            BYAMLNode kvp1 = new(BYAMLNodeType.String, key);
            Dictionary<string, BYAMLNode> newdict = new() { { "Infos", kvp0 }, { "LayerName", kvp1 } };
            retArray.Add(new(newdict));
        }

        return retArray.ToArray();
    }

    public static string InfosToString(StageObjType _infos)
    {
        return _infos switch
        {
            StageObjType.Regular => "ObjInfo",
            StageObjType.Area => "AreaObjInfo",
            StageObjType.CameraArea => "CameraAreaInfo",
            StageObjType.Goal => "GoalObjInfo",
            StageObjType.Start => "StartInfo",
            StageObjType.StartEvent => "StartEventObjInfo",
            StageObjType.DemoScene => "DemoSceneObjInfo",

            StageObjType.Child => "Child Object",
            StageObjType.AreaChild => "Area Child Object",
            StageObjType.Rail => "Rail",
            _ => throw new("Incorrect object type")
        };
    }

    private BYAMLNode WriteRail(RailObj rail, int currentId)
    {
        Dictionary<string, BYAMLNode> currentRailNodes = new();
        // try to get Args
        for (int i = 0; i < 10; i++)
        {
            rail.Properties.TryGetValue("Arg" + i, out object? arg);
            if (arg != null)
                currentRailNodes.Add("Arg" + i, new(BYAMLNodeType.Int, arg));
        }
        // try to get LayerName
        currentRailNodes.Add("LayerName", new(BYAMLNodeType.String, rail.Layer));
        // try to get MultiFileName
        rail.Properties.TryGetValue("MultiFileName", out object? mfn);
        if (mfn != null)
            currentRailNodes.Add("MultiFileName", new(BYAMLNodeType.String, mfn));
        // try to get Points
        //rail.Points;
        List<BYAMLNode> points = new();
        int ptid = 0;
        foreach (RailPoint pt in rail.Points)
        {
            // try to get point Args
            Dictionary<string, BYAMLNode> ptd = new();
            for (int i = 0; i < 10; i++)
            {
                pt.Properties.TryGetValue("Arg" + i, out object? arg);
                if (arg != null)
                    ptd.Add("Arg" + i, new(BYAMLNodeType.Int, arg));
            }
            // try to get point id
            ptd.Add("id", new(BYAMLNodeType.Int, ptid));

            // try to get point positions
            if (rail.PointType == RailPointType.Linear)
            {
                var lpt = (RailPointLinear)pt;
                for (int p = 0; p < 3; p++)
                {
                    ptd.Add("pnt" + p + "_x", new(BYAMLNodeType.Float, lpt.Translation.X));
                    ptd.Add("pnt" + p + "_y", new(BYAMLNodeType.Float, lpt.Translation.Y));
                    ptd.Add("pnt" + p + "_z", new(BYAMLNodeType.Float, lpt.Translation.Z));
                }
            }
            else
            {
                var bpt = (RailPointBezier)pt;
                ptd.Add("pnt0_x", new(BYAMLNodeType.Float, bpt.Point0Trans.X));
                ptd.Add("pnt0_y", new(BYAMLNodeType.Float, bpt.Point0Trans.Y));
                ptd.Add("pnt0_z", new(BYAMLNodeType.Float, bpt.Point0Trans.Z));
                ptd.Add("pnt1_x", new(BYAMLNodeType.Float, bpt.Point1Trans.X));
                ptd.Add("pnt1_y", new(BYAMLNodeType.Float, bpt.Point1Trans.Y));
                ptd.Add("pnt1_z", new(BYAMLNodeType.Float, bpt.Point1Trans.Z));
                ptd.Add("pnt2_x", new(BYAMLNodeType.Float, bpt.Point2Trans.X));
                ptd.Add("pnt2_y", new(BYAMLNodeType.Float, bpt.Point2Trans.Y));
                ptd.Add("pnt2_z", new(BYAMLNodeType.Float, bpt.Point2Trans.Z));
            }
            points.Add(new(ptd));
            ptid += 1;
        }
        // add points to rail
        currentRailNodes.Add("Points", new(BYAMLNodeType.Array, points.ToArray()));
        // try to get closed
        currentRailNodes.Add("closed", new(BYAMLNodeType.String, rail.Closed ? "CLOSE" : "OPEN", false));
        // try to get l_id
        rail.RailNo = currentId;
        currentRailNodes.Add("l_id", new(BYAMLNodeType.Int, currentId));
        // try to get name
        currentRailNodes.Add("name", new(BYAMLNodeType.String, rail.Name));
        currentRailNodes.Add("no", new(BYAMLNodeType.Int, currentId));
        // try to get num_pnt
        currentRailNodes.Add("num_pnt", new(BYAMLNodeType.Int, rail.Points.Count()));
        // try to get type
        currentRailNodes.Add("type", new(BYAMLNodeType.String, rail.PointType.ToString()));
        return new BYAMLNode(currentRailNodes);
    }

    private BYAMLNode WriteSceneObjects(
        StageObj currentObj,
        ref int currentId,
        Dictionary<RailObj, BYAMLNode> railObjNodes,
        int parentId = -1,
        bool useClassNames = false
    )
    {
        // read all information
        Dictionary<string, BYAMLNode> currentObjectNodes = new();
        //WriteSceneObjects(stageObj, currentId, file);
        int m = 10;

        if (currentObj.Type != StageObjType.Regular && currentObj.Type != StageObjType.Child) m = 8;
        for (int i = 0; i < m; i++)
        {
            currentObj.Properties.TryGetValue("Arg" + i, out object? arg);
            if (arg != null) currentObjectNodes.Add("Arg" + i, new(arg));
        }

        foreach (var (name, property) in currentObj.Properties)
        {
            if (name.Contains("Arg"))
                continue;

            if (property is null)
                continue;

            switch (property)
            {
                case object p when p is int:
                    int intBuf = (int)(p ?? -1);
                    currentObjectNodes.Add(name, new(BYAMLNodeType.Int, intBuf));
                    break;

                case object p when p is string:
                    string strBuf = (string)(p ?? string.Empty);
                    currentObjectNodes.Add(name, new(BYAMLNodeType.String, strBuf));
                    break;

                default:
                    throw new NotImplementedException(
                        "The property type " + property?.GetType().FullName ?? "null" + " is not supported."
                    );
            }
        }
        if (currentObj.Type != StageObjType.Start)
            currentObjectNodes.Add("l_id", new(BYAMLNodeType.Int, currentId));

        if (currentObj.Children != null && currentObj.Children.Any())
        {
            List<BYAMLNode> objectArray = new();
            List<BYAMLNode> areaArray = new();
            // recursive function
            int pid = currentId;
            foreach (StageObj child in currentObj.Children)
            {
                currentId += 1;

                if (child.Type == StageObjType.AreaChild) areaArray.Add(WriteSceneObjects(child, ref currentId, railObjNodes, pid, useClassNames: useClassNames));
                else objectArray.Add(WriteSceneObjects(child, ref currentId, railObjNodes, pid, useClassNames: useClassNames));
            }

            BYAMLNode objectArrayNode = new(BYAMLNodeType.Array, objectArray.ToArray());
            BYAMLNode areaArrayNode = new(BYAMLNodeType.Array, areaArray.ToArray());

            if (objectArray.Count != 0)
                currentObjectNodes.Add("GenerateChildren", objectArrayNode);

            if (areaArray.Count != 0)
                currentObjectNodes.Add("AreaChildren", areaArrayNode);
        }

        currentObjectNodes.Add("name", new(BYAMLNodeType.String, currentObj.Name));

        if (useClassNames)
        {
            if (string.IsNullOrEmpty(currentObj.ClassName))
            {
                if (ReadCreatorClassNameTable().ContainsKey(currentObj.Name))
                    currentObj.ClassName = _creatorClassNameTable[currentObj.Name];
                else if (ClassDatabaseWrapper.DatabaseEntries.ContainsKey(currentObj.Name))
                    currentObj.ClassName = ClassDatabaseWrapper.DatabaseEntries[currentObj.Name].ClassName;
            }
            if (!string.IsNullOrEmpty(currentObj.ClassName))
                currentObjectNodes.Add("ClassName", new(BYAMLNodeType.String, currentObj.ClassName));
        }

        currentObjectNodes.Add("LayerName", new(BYAMLNodeType.String, currentObj.Layer));

        // object transforms
        {
            currentObjectNodes.Add("pos_x", new(BYAMLNodeType.Float, currentObj.Translation.X));
            currentObjectNodes.Add("pos_y", new(BYAMLNodeType.Float, currentObj.Translation.Y));
            currentObjectNodes.Add("pos_z", new(BYAMLNodeType.Float, currentObj.Translation.Z));

            currentObjectNodes.Add("dir_x", new(BYAMLNodeType.Float, currentObj.Rotation.X));
            currentObjectNodes.Add("dir_y", new(BYAMLNodeType.Float, currentObj.Rotation.Y));
            currentObjectNodes.Add("dir_z", new(BYAMLNodeType.Float, currentObj.Rotation.Z));

            currentObjectNodes.Add("scale_x", new(BYAMLNodeType.Float, currentObj.Scale.X));
            currentObjectNodes.Add("scale_y", new(BYAMLNodeType.Float, currentObj.Scale.Y));
            currentObjectNodes.Add("scale_z", new(BYAMLNodeType.Float, currentObj.Scale.Z));
        }

        if (currentObj.Type != StageObjType.Start)
        {
            currentObjectNodes.Add("SwitchA", new(BYAMLNodeType.Int, currentObj.SwitchA));
            currentObjectNodes.Add("SwitchAppear", new(BYAMLNodeType.Int, currentObj.SwitchAppear));
            currentObjectNodes.Add("SwitchB", new(BYAMLNodeType.Int, currentObj.SwitchB));
            currentObjectNodes.Add("SwitchDeadOn", new(BYAMLNodeType.Int, currentObj.SwitchDeadOn));
            currentObjectNodes.Add("SwitchKill", new(BYAMLNodeType.Int, currentObj.SwitchKill));
        }

        if (
            currentObj.Type == StageObjType.Regular
            || currentObj.Type == StageObjType.Child
            || currentObj.Type == StageObjType.Goal
        )
        {
            currentObjectNodes.Add("ViewId", new(BYAMLNodeType.Int, currentObj.ViewId));
            currentObjectNodes.Add("ClippingGroupId", new(BYAMLNodeType.Int, currentObj.ClippingGroupId));
        }

        if (
            currentObj.Type == StageObjType.Goal
            || currentObj.Type == StageObjType.Regular
            || currentObj.Type == StageObjType.CameraArea
            || currentObj.Type == StageObjType.Child
        )
        {
            currentObjectNodes.Add("CameraId", new(BYAMLNodeType.Int, currentObj.CameraId));
        }

        if (currentObj.Rail is not null)
        {
            currentObjectNodes.Add("Rail", railObjNodes[currentObj.Rail]);
        }

        if (currentObj.Type == StageObjType.Area || currentObj.Type == StageObjType.AreaChild)
            currentObjectNodes.Add("AreaParent", new(BYAMLNodeType.Int, currentObj.Parent != null ? parentId : -1));
        else if (
            currentObj.Type == StageObjType.Regular
            || currentObj.Type == StageObjType.Goal
            || currentObj.Type == StageObjType.Child
        )
            currentObjectNodes.Add("GenerateParent", new(BYAMLNodeType.Int, currentObj.Parent != null ? parentId : -1));

        return new(currentObjectNodes);
    }

    #endregion

    #region More Writing
    public bool WriteBgmTable(BgmTable? bT, Stage stage)
    {
        if (bT is null) return false;

        string tablePath = Path.Join(_soundPath, "BgmTable.szs");

        Dictionary<string, BYAMLNode> dtop = new();

        List<BYAMLNode> defaultBGMList = new(); //"StageDefaultBgmList"
        int changedBgm = -1;
        foreach (StageDefaultBgm defBgm in bT.StageDefaultBgmList)
        {
            if (defBgm.StageName == stage.Name && defBgm.Scenario == stage.Scenario)
            {
                changedBgm = bT.StageDefaultBgmList.IndexOf(defBgm);
                defaultBGMList.Add(new(
                    new Dictionary<string, BYAMLNode>
                    {
                        { "BgmLabel", new(stage.DefaultBgm.BgmLabel)},
                        { "Scenario", new(stage.DefaultBgm.Scenario)},
                        { "StageName", new(stage.DefaultBgm.StageName)}
                    }
                    ));
            }
            else
                defaultBGMList.Add(new(
                    new Dictionary<string, BYAMLNode>
                    {
                        { "BgmLabel", new(defBgm.BgmLabel)},
                        { "Scenario", new(defBgm.Scenario)},
                        { "StageName", new(defBgm.StageName)}
                    }
                    ));
        }

        dtop.Add("StageDefaultBgmList", new(defaultBGMList.ToArray()));

        if (changedBgm > -1)
            bT.StageDefaultBgmList[changedBgm].BgmLabel = stage.DefaultBgm.BgmLabel;

        Dictionary<string, BYAMLNode> top = new();

        List<BYAMLNode> BGMTypes = new(); //"KindNumList"

        foreach (KeyValuePair<int, string> s in bT.BgmTypes)
        {
            Dictionary<string, BYAMLNode> tp = new()
            {
                { "Idx", new( BYAMLNodeType.Int, s.Key)},
                { "Kind", new( BYAMLNodeType.String, s.Value)}
            };
            BGMTypes.Add(new(tp));
        }
        top.Add("KindNumList", new(BYAMLNodeType.Array, BGMTypes.ToArray()));

        List<BYAMLNode> BGMList = new(); //"StageBgmList"

        foreach (StageBgm bgm in bT.StageBgmList)
        {
            Dictionary<string, BYAMLNode> lineList = new();

            foreach (string s in bgm.LineList.Keys)
            {
                List<BYAMLNode> currentline = new();
                foreach (KindDefine k in bgm.LineList[s])
                {
                    currentline.Add(new(new Dictionary<string, BYAMLNode>()
                    {
                      { "Kind", new(BYAMLNodeType.String, k.Kind)},
                      { "Label", new(BYAMLNodeType.String, k.Label)}
                    }));
                }
                lineList.Add(s, new(currentline.ToArray()));
            }

            Dictionary<string, BYAMLNode> tp = new()
            {
                { "StageName", new( BYAMLNodeType.String, bgm.StageName)},
                { "LineList", new(lineList)}
            };
            if (bgm.Scenario != null)
                tp.Add("Scenario", new(BYAMLNodeType.Int, bgm.Scenario));
            BGMList.Add(new(tp));
        }
        top.Add("StageBgmList", new(BYAMLNodeType.Array, BGMList.ToArray()));



        BYAML defbyml = new(new(dtop), s_byamlEncoding, default);
        BYAML byml = new(new(top), s_byamlEncoding, default);
        NARCFileSystem narcFS = new(new());
        byte[] defbin = BYAMLParser.Write(defbyml);
        byte[] bin = BYAMLParser.Write(byml);
        narcFS.AddFileRoot("StageBgmList.byml", bin);
        narcFS.AddFileRoot("StageDefaultBgmList.byml", defbin);

        foreach (string s in bT.AdditionalFiles.Keys)
        {
            if (s == "StageDefaultBgmList.byml" || s == "StageBgmList.byml") continue;
            narcFS.AddFileRoot(s, bT.AdditionalFiles[s]);
        }
        byte[] compressedFile = Yaz0Wrapper.Compress(NARCParser.Write(narcFS.ToNARC()));
        File.WriteAllBytes(tablePath, compressedFile);
        return true;
    }

    #endregion
}
