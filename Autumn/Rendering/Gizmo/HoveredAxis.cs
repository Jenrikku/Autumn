// Based on: https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/GizmoDrawer.cs
namespace Autumn.Rendering.Gizmo;

[Flags]
internal enum HoveredAxis
{
    NONE = 0,
    X_AXIS = 1,
    Y_AXIS = 2,
    Z_AXIS = 4,
    XY_PLANE = X_AXIS | Y_AXIS,
    XZ_PLANE = X_AXIS | Z_AXIS,
    YZ_PLANE = Y_AXIS | Z_AXIS,
    ALL_AXES = X_AXIS | Y_AXIS | Z_AXIS,
    FREE = ALL_AXES,
    VIEW_AXIS = 8,
    TRACKBALL = 16
}
