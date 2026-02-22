namespace Autumn.Enums;

internal enum AddRailPointState : byte
{
    None = 0,
    Add = 1,
    Insert = 2,
}
internal enum HoverInfoMode : byte
{
    Disabled = 0,
    Tooltip = 1,
    Highlight = 2,
    Status = 3,
}
internal enum TransformGizmo : byte
{
    None = 0,
    Translate = 1,
    Rotate = 2,
    Scale = 3,
}
internal enum GizmoPosition : byte
{
    Middle = 0,
    First = 1,
    Last = 2,
}
