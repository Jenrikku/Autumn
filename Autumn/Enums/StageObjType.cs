namespace Autumn.Enums;

internal enum StageObjType : byte
{
    Regular = 0, // ObjInfo
    Area, // AreaObjInfo
    CameraArea, //CameraAreaInfo
    Goal, //GoalObjInfo
    StartEvent, //StartEventObjInfo
    Start, //StartInfo
    DemoScene, //DemoSceneObjInfo
    Rail, // Other
    AreaChild, // Other
    Child // Other
}
