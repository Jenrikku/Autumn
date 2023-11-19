using System.Numerics;
using Autumn.Utils;

// Based on: https://github.com/jupahe64/SceneGL/blob/master/SceneGL.Testing/GizmoDrawer.cs
namespace Autumn.Scene.Gizmo;

internal record struct Rect(Vector2 TopLeft, Vector2 BottomRight)
{
    public readonly Vector2 Size => BottomRight - TopLeft;

    public readonly bool Contains(Vector2 pos) =>
        TopLeft.X <= pos.X
        && pos.X <= BottomRight.X
        && TopLeft.Y <= pos.Y
        && pos.Y <= BottomRight.Y;
}

internal record struct CameraState(
    Vector3 Position,
    Vector3 ForwardVector,
    Vector3 UpVector,
    Quaternion Rotation
)
{
    public readonly Vector3 RightVector => Vector3.Cross(ForwardVector, UpVector);
}

internal record struct SceneViewState(
    CameraState CameraState,
    Matrix4x4 ViewProjectionMatrix,
    Rect ViewportRect,
    Vector2 MousePosition,
    Vector3 MouseRayDirection
)
{
    public readonly Vector2 WorldToScreen(Vector3 vec)
    {
        var vec4 = Vector4.Transform(new Vector4(vec, 1), ViewProjectionMatrix);

        var vec2 = new Vector2(vec4.X, vec4.Y) / Math.Max(0, vec4.W);

        vec2.Y *= -1;

        vec2 += Vector2.One;

        return ViewportRect.TopLeft + vec2 * ViewportRect.Size * 0.5f;
    }

    public readonly Vector3 CamUpVector => CameraState.UpVector;
    public readonly Vector3 CamForwardVector => CameraState.ForwardVector;
    public readonly Vector3 CamRightVector => CameraState.RightVector;
    public readonly Vector3 CamPosition => CameraState.Position;
    public readonly Quaternion CamRotation => CameraState.Rotation;

    public readonly Vector3 MouseRayHitOnPlane(Vector3 planeNormal, Vector3 planeOrigin) =>
        MathUtils.IntersectPoint(MouseRayDirection, CamPosition, planeNormal, planeOrigin);
}
