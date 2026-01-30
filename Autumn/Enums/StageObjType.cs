namespace Autumn.Enums;

internal enum StageObjType : byte
{
    Unknown = 0,
    Area, // AreaObjInfo
    CameraArea, //CameraAreaInfo
    Regular, // ObjInfo
    Goal, //GoalObjInfo
    StartEvent, //StartEventObjInfo
    Start, //StartInfo
    DemoScene, //DemoSceneObjInfo
    Rail, // Other
    AreaChild, // Other
    Child // Other
}
