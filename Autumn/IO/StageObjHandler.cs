using Autumn.Storage.StageObjs;
using BYAMLSharp;

namespace Autumn.IO;

internal static class StageObjHandler
{
    public static IStageObj[] ProcessStageObjs(BYAML byaml, StageObjFileType fileType)
    {
        List<IStageObj> list = new();

        if (byaml.RootNode.NodeType is not BYAMLNodeType.Dictionary)
            throw new("The given BYAML was not formatted correctly.");

        var rootDict = byaml.RootNode.GetValueAs<Dictionary<string, BYAMLNode>>()!;

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

                var dict = node.GetValueAs<Dictionary<string, BYAMLNode>>()!;

                IStageObj stageObj;

                // IStageObj stageObj = info.Key switch
                // {
                //     AreaStageObj.InfoName => new AreaStageObj(),
                //     CameraAreaStageObj.InfoName => new CameraAreaStageObj(),
                //     GoalStageObj.InfoName => new GoalStageObj(),
                //     RegularStageObj.InfoName => new RegularStageObj(),
                //     StartEventStageObj.InfoName => new StartEventStageObj(),
                //     StartStageObj.InfoName => new StartStageObj(),
                //     _ => throw new NotSupportedException("Unknown stage object type found.")
                // };

                switch (info.Key)
                {
                    case AreaStageObj.InfoName:
                        AreaStageObj areaStageObj = new();

                        foreach (KeyValuePair<string, BYAMLNode> property in dict)
                            ParseProperty(ref areaStageObj, property.Key, property.Value.Value);

                        stageObj = areaStageObj;
                        break;

                    case CameraAreaStageObj.InfoName:
                        CameraAreaStageObj cameraAreaStageObj = new();

                        foreach (KeyValuePair<string, BYAMLNode> property in dict)
                            ParseProperty(
                                ref cameraAreaStageObj,
                                property.Key,
                                property.Value.Value
                            );

                        stageObj = cameraAreaStageObj;
                        break;

                    case GoalStageObj.InfoName:
                        GoalStageObj goalStageObj = new();

                        foreach (KeyValuePair<string, BYAMLNode> property in dict)
                            ParseProperty(ref goalStageObj, property.Key, property.Value.Value);

                        stageObj = goalStageObj;
                        break;

                    case RegularStageObj.InfoName:
                        RegularStageObj regularStageObj = new();

                        foreach (KeyValuePair<string, BYAMLNode> property in dict)
                            ParseProperty(ref regularStageObj, property.Key, property.Value.Value);

                        stageObj = regularStageObj;
                        break;

                    case StartEventStageObj.InfoName:
                        StartEventStageObj startEventStageObj = new();

                        foreach (KeyValuePair<string, BYAMLNode> property in dict)
                            ParseProperty(
                                ref startEventStageObj,
                                property.Key,
                                property.Value.Value
                            );

                        stageObj = startEventStageObj;
                        break;

                    case StartStageObj.InfoName:
                        StartStageObj startStageObj = new();

                        foreach (KeyValuePair<string, BYAMLNode> property in dict)
                            ParseProperty(ref startStageObj, property.Key, property.Value.Value);

                        stageObj = startStageObj;
                        break;

                    default:
                        throw new NotSupportedException("Unknown stage object type found.");
                }

                stageObj.FileType = fileType;

                list.Add(stageObj);
            }
        }

        return list.ToArray();
    }

    private static bool TryParseCommonProperty<T>(ref T stageObj, string key, object? property)
        where T : IStageObj
    {
        switch ((key, property))
        {
            case ("name", string str):
                stageObj.Name = str;
                return true;

            case ("LayerName", string str):
                stageObj.LayerName = str;
                return true;

            case ("dir_x", float num):
                stageObj.Rotation = stageObj.Rotation with { X = num };
                return true;

            case ("dir_y", float num):
                stageObj.Rotation = stageObj.Rotation with { Y = num };
                return true;

            case ("dir_z", float num):
                stageObj.Rotation = stageObj.Rotation with { Z = num };
                return true;

            case ("pos_x", float num):
                stageObj.Translation = stageObj.Translation with { X = num };
                return true;

            case ("pos_y", float num):
                stageObj.Translation = stageObj.Translation with { Y = num };
                return true;

            case ("pos_z", float num):
                stageObj.Translation = stageObj.Translation with { Z = num };
                return true;

            case ("scale_x", float num):
                stageObj.Scale = stageObj.Scale with { X = num };
                return true;

            case ("scale_y", float num):
                stageObj.Scale = stageObj.Scale with { Y = num };
                return true;

            case ("scale_z", float num):
                stageObj.Scale = stageObj.Scale with { Z = num };
                return true;
        }

        return false;
    }

    private static void ParseProperty(ref AreaStageObj stageObj, string key, object? property)
    {
        if (TryParseCommonProperty(ref stageObj, key, property))
            return;

        if (
            key.StartsWith("Arg")
            && byte.TryParse(key[3..], out byte argNum)
            && argNum < stageObj.Args.Length
            && property is int arg
        )
        {
            stageObj.Args[argNum] = arg;
            return;
        }

        switch ((key, property))
        {
            case ("AreaParent", int num):
                stageObj.AreaParent = num;
                return;
            case ("Priority", int num):
                stageObj.Priority = num;
                return;
            case ("ShapeModelNo", int num):
                stageObj.ShapeModelNo = num;
                return;
            case ("SwitchA", int num):
                stageObj.SwitchA = num;
                return;
            case ("SwitchAppear", int num):
                stageObj.SwitchAppear = num;
                return;
            case ("SwitchB", int num):
                stageObj.SwitchB = num;
                return;
            case ("SwitchKill", int num):
                stageObj.SwitchKill = num;
                return;
        }

        stageObj.OtherProperties.Add(key, property);
    }

    private static void ParseProperty(ref CameraAreaStageObj stageObj, string key, object? property)
    {
        if (TryParseCommonProperty(ref stageObj, key, property))
            return;

        switch ((key, property))
        {
            case ("CameraId", int num):
                stageObj.CameraId = num;
                return;
            case ("Priority", int num):
                stageObj.Priority = num;
                return;
            case ("ShapeModelNo", int num):
                stageObj.ShapeModelNo = num;
                return;
            case ("SwitchAppear", int num):
                stageObj.SwitchAppear = num;
                return;
            case ("SwitchKill", int num):
                stageObj.SwitchKill = num;
                return;
        }

        stageObj.OtherProperties.Add(key, property);
    }

    private static void ParseProperty(ref GoalStageObj stageObj, string key, object? property)
    {
        if (TryParseCommonProperty(ref stageObj, key, property))
            return;

        if (
            key.StartsWith("Arg")
            && byte.TryParse(key[3..], out byte argNum)
            && argNum < stageObj.Args.Length
            && property is int arg
        )
        {
            stageObj.Args[argNum] = arg;
            return;
        }

        //if (key == "Rail") { }

        switch ((key, property))
        {
            case ("CameraId", int num):
                stageObj.CameraId = num;
                return;
            case ("ClippingGroupId", int num):
                stageObj.ClippingGroupId = num;
                return;
            case ("ShapeModelNo", int num):
                stageObj.ShapeModelNo = num;
                return;
            case ("SwitchA", int num):
                stageObj.SwitchA = num;
                return;
            case ("SwitchAppear", int num):
                stageObj.SwitchAppear = num;
                return;
            case ("SwitchB", int num):
                stageObj.SwitchB = num;
                return;
            case ("SwitchDeadOn", int num):
                stageObj.SwitchDeadOn = num;
                return;
            case ("ViewId", int num):
                stageObj.ViewId = num;
                return;
        }

        stageObj.OtherProperties.Add(key, property);
    }

    private static void ParseProperty(ref RegularStageObj stageObj, string key, object? property)
    {
        if (TryParseCommonProperty(ref stageObj, key, property))
            return;

        if (
            key.StartsWith("Arg")
            && byte.TryParse(key[3..], out byte argNum)
            && argNum < stageObj.Args.Length
            && property is int arg
        )
        {
            stageObj.Args[argNum] = arg;
            return;
        }

        //if (key == "Rail") { }

        switch ((key, property))
        {
            case ("CameraId", int num):
                stageObj.CameraId = num;
                return;
            case ("ClippingGroupId", int num):
                stageObj.ClippingGroupId = num;
                return;
            case ("GenerateParent", int num):
                stageObj.GenerateParent = num;
                return;
            case ("ShapeModelNo", int num):
                stageObj.ShapeModelNo = num;
                return;
            case ("SwitchA", int num):
                stageObj.SwitchA = num;
                return;
            case ("SwitchAppear", int num):
                stageObj.SwitchAppear = num;
                return;
            case ("SwitchB", int num):
                stageObj.SwitchB = num;
                return;
            case ("SwitchDeadOn", int num):
                stageObj.SwitchDeadOn = num;
                return;
            case ("SwitchKill", int num):
                stageObj.SwitchKill = num;
                return;
            case ("ViewId", int num):
                stageObj.ViewId = num;
                return;
        }

        stageObj.OtherProperties.Add(key, property);
    }

    private static void ParseProperty(ref StartEventStageObj stageObj, string key, object? property)
    {
        if (TryParseCommonProperty(ref stageObj, key, property))
            return;

        if (
            key.StartsWith("Arg")
            && byte.TryParse(key[3..], out byte argNum)
            && argNum < stageObj.Args.Length
            && property is int arg
        )
        {
            stageObj.Args[argNum] = arg;
            return;
        }

        stageObj.OtherProperties.Add(key, property);
    }

    private static void ParseProperty(ref StartStageObj stageObj, string key, object? property)
    {
        if (key == "MarioNo" && property is int num)
        {
            stageObj.MarioNo = num;
            return;
        }

        if (TryParseCommonProperty(ref stageObj, key, property))
            return;

        stageObj.OtherProperties.Add(key, property);
    }
}
