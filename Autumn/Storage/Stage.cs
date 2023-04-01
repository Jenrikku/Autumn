using AutumnSceneGL.IO;
using AutumnStageEditor.Storage.StageObj;
using AutumnStageEditor.Storage.StageObj.Interfaces;
using Jenrikku.BYAML;
using NARCSharp;
using System.Diagnostics;
using System.Text;

namespace AutumnSceneGL.Storage {
    internal class Stage {
        public Stage() { }

        public Stage(string name, byte? scenario = null) {
            Name = name;
            Scenario = scenario;
        }

        public string Name { get; set; } = "Unnamed";
        public byte? Scenario { get; set; }

        //private string? _stagePos;
        //public string StagePosition { 
        //    get {
        //        if(_stagePos is not null)
        //            return _stagePos;

        //        return _stagePos = 
        //    }
        //}

        private List<IBaseObj>? _stageObjs;

        public List<IBaseObj> StageObjs {
            get {
                if(_stageObjs is null)
                    LoadStageObjs();

                return _stageObjs!;
            }
        }

        private void LoadStageObjs() {
            _stageObjs = new();

            byte[] designRaw = RomFSHandler.RequestFile(Path.Join("StageData", Name + "StageDesign" + Scenario));
            byte[] mapRaw = RomFSHandler.RequestFile(Path.Join("StageData", Name + "StageMap" + Scenario));
            byte[] soundRaw = RomFSHandler.RequestFile(Path.Join("StageData", Name + "StageSound" + Scenario));

            if(SZSWrapper.TryReadFile(designRaw, out NARCFileSystem? design) && design is not null)
                LoadStageDesign(design);

            if(SZSWrapper.TryReadFile(mapRaw, out NARCFileSystem? map) && map is not null)
                LoadStageMap(map);

            if(SZSWrapper.TryReadFile(soundRaw, out NARCFileSystem? sound) && sound is not null)
                LoadStageSound(sound);
        }

        private static Encoding ShiftJIS = Encoding.GetEncoding("Shift_JIS");

        private void LoadStageDesign(NARCFileSystem design) {
            byte[] stageData = design.GetFile("StageData.byml");

            BYAML dataByml = BYAMLParser.Read(stageData, ShiftJIS);

            if(stageData.Length > 0)
                if(!LoadStageData(in dataByml))
                    Debug.Fail("StageData (design) for " + Name + " could not be read.");

            // TODO: AreaIdToLightNameTable, ModelToMapLightNameTable
        }

        private void LoadStageMap(NARCFileSystem map) {
            byte[] stageData = map.GetFile("StageData.byml");

            BYAML dataByml = BYAMLParser.Read(stageData, ShiftJIS);

            if(stageData.Length > 0)
                if(!LoadStageData(in dataByml))
                    Debug.Fail("StageData (map) for " + Name + " could not be read.");

            // TODO: CameraParam, StageInfo
        }

        private void LoadStageSound(NARCFileSystem sound) {
            byte[] stageData = sound.GetFile("StageData.byml");

            if(stageData.Length > 0)
                if(!LoadStageData(BYAMLParser.Read(stageData, ShiftJIS)))
                    Debug.Fail("StageData (sound) for " + Name + " could not be read.");
        }

        private bool LoadStageData(in BYAML byml) {
            if(byml.Root is not Dictionary<string, object?> root)
                return false;

            if(!root.TryGetValue("AllInfos", out object? infos) ||
                infos is not Dictionary<string, object?> allInfos)
                return false;

            foreach(KeyValuePair<string, object?> allInfosEntry in allInfos) {
                if(allInfosEntry.Value is not object?[] info)
                    continue;

                foreach(object? entry in info) {
                    dynamic parsed = allInfosEntry.Key switch {
                        "AreaObjInfo" => new AreaObj(),
                        "CameraAreaInfo" => new CameraAreaObj(),
                        "GoalObjInfo" => new GoalObj(),
                        "ObjInfo" => new Obj(),
                        "StartEventObjInfo" => new StartEventObj(),
                        "StartInfo" => new StartObj(),
                        _ => throw new NotSupportedException("Object type " + allInfosEntry.Key + " is not supported.")
                    };

                    if(parsed is not IBaseObj || entry is not Dictionary<string, object?> obj)
                        continue;

                    foreach(KeyValuePair<string, object?> property in obj)
                        if(!parsed.TryParseProperty(property.Key, property.Value))
                            ((IBaseObj) parsed).SetCustomProperty(property.Key, property.Value);

                    _stageObjs!.Add(parsed);
                }
            }

            return true;
        }
    }
}
