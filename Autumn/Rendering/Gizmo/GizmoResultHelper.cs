using System.Runtime.CompilerServices;

// Based on: https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/GizmoDrawer.cs
namespace Autumn.Rendering.Gizmo;

/// <summary>
/// Provides useful methods for evaluating the results of the Gizmo functions in <see cref="GizmoDrawer"/>
/// </summary>
internal static class GizmoResultHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSingleAxis(HoveredAxis hoveredAxis, int axis)
    {
        return (int)hoveredAxis == 1 << axis;
    }

    public static bool IsSingleAxis(HoveredAxis hoveredAxis, out int axis)
    {
        switch (hoveredAxis)
        {
            case HoveredAxis.X_AXIS:
                axis = 0;
                return true;
            case HoveredAxis.Y_AXIS:
                axis = 1;
                return true;
            case HoveredAxis.Z_AXIS:
                axis = 2;
                return true;
            default:
                axis = -1;
                return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPlane(HoveredAxis hoveredAxis, int axisA, int axisB)
    {
        return (int)hoveredAxis == (1 << axisA | 1 << axisB);
    }

    public static bool IsPlane(HoveredAxis hoveredAxis, out int axisA, out int axisB)
    {
        switch (hoveredAxis)
        {
            case HoveredAxis.XY_PLANE:
                axisA = 0;
                axisB = 1;
                return true;
            case HoveredAxis.XZ_PLANE:
                axisA = 0;
                axisB = 2;
                return true;
            case HoveredAxis.YZ_PLANE:
                axisA = 1;
                axisB = 2;
                return true;
            default:
                axisA = -1;
                axisB = -1;
                return false;
        }
    }

    public static bool IsPlane(HoveredAxis hoveredAxis, out int orthogonalAxis)
    {
        switch (hoveredAxis)
        {
            case HoveredAxis.XY_PLANE:
                orthogonalAxis = 2;
                return true;
            case HoveredAxis.XZ_PLANE:
                orthogonalAxis = 1;
                return true;
            case HoveredAxis.YZ_PLANE:
                orthogonalAxis = 0;
                return true;
            default:
                orthogonalAxis = -1;
                return false;
        }
    }
}
