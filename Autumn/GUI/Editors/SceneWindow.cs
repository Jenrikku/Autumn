using System.Diagnostics;
using System.Numerics;
using Autumn.Rendering;
using Autumn.Rendering.Gizmo;
using Autumn.Utils;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;

namespace Autumn.GUI.Editors;

internal class SceneWindow(MainWindowContext window)
{
    private bool _persistentMouseDrag = false;
    private Vector2 _previousMousePos = Vector2.Zero;
    private Queue<Action<MainWindowContext, Vector4>> _mouseClickActions = new();

    public void AddMouseClickAction(Action<MainWindowContext, Vector4> action)
    {
        _mouseClickActions.Enqueue(action);
    }

    public unsafe void Render(double deltaSeconds)
    {
        if (window.CurrentScene is null)
            return;

        float aspectRatio;

        bool isSceneHovered;
        bool isSceneWindowFocused;

        Vector2 sceneImageRectMin;
        Vector2 sceneImageRectMax;
        Vector2 sceneImageSize;

        if (
            !ImGui.Begin("Scene", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        )
            return;

        Vector2 contentAvail = ImGui.GetContentRegionAvail();
        aspectRatio = contentAvail.X / contentAvail.Y;

        Vector2 sceneWindowRegionMin = ImGui.GetWindowContentRegionMin();
        Vector2 sceneWindowRegionMax = ImGui.GetWindowContentRegionMax();

        if (contentAvail.X < 0 || contentAvail.Y < 0)
        {
            ImGui.End();
            return;
        }

        window.SceneFramebuffer.SetSize((uint)contentAvail.X, (uint)contentAvail.Y);
        window.SceneFramebuffer.Create(window.GL!);

        ImGui.Image(
            new IntPtr(window.SceneFramebuffer.GetColorTexture(0)),
            contentAvail,
            new Vector2(0, 1),
            new Vector2(1, 0)
        );

        isSceneHovered = ImGui.IsItemHovered();
        isSceneWindowFocused = ImGui.IsWindowFocused();
        sceneImageRectMin = ImGui.GetItemRectMin();
        sceneImageRectMax = ImGui.GetItemRectMax();
        sceneImageSize = contentAvail;

        ImGui.PopStyleVar();

        Camera camera = window.CurrentScene!.Camera;

        #region Input

        Vector2 mousePos = ImGui.GetMousePos();

        if (
            (isSceneHovered || _persistentMouseDrag)
            && ImGui.IsMouseDragging(ImGuiMouseButton.Right)
        )
        {
            Vector2 delta = mousePos - _previousMousePos;

            Vector3 right = Vector3.Transform(Vector3.UnitX, camera.Rotation);
            camera.Rotation =
                Quaternion.CreateFromAxisAngle(right, -delta.Y * 0.002f) * camera.Rotation;

            camera.Rotation =
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, -delta.X * 0.002f) * camera.Rotation;

            _persistentMouseDrag = true;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Right))
            _persistentMouseDrag = false;

        _previousMousePos = mousePos;

        if (isSceneHovered || isSceneWindowFocused)
        {
            float camMoveSpeed = (float)(0.4 * deltaSeconds * 60);

            if (window.Keyboard?.IsKeyPressed(Key.W) ?? false)
                camera.Eye -= Vector3.Transform(Vector3.UnitZ * camMoveSpeed, camera.Rotation);
            if (window.Keyboard?.IsKeyPressed(Key.S) ?? false)
                camera.Eye += Vector3.Transform(Vector3.UnitZ * camMoveSpeed, camera.Rotation);

            if (window.Keyboard?.IsKeyPressed(Key.A) ?? false)
                camera.Eye -= Vector3.Transform(Vector3.UnitX * camMoveSpeed, camera.Rotation);
            if (window.Keyboard?.IsKeyPressed(Key.D) ?? false)
                camera.Eye += Vector3.Transform(Vector3.UnitX * camMoveSpeed, camera.Rotation);

            if (window.Keyboard?.IsKeyPressed(Key.Q) ?? false)
                camera.Eye -= Vector3.UnitY * camMoveSpeed;
            if (window.Keyboard?.IsKeyPressed(Key.E) ?? false)
                camera.Eye += Vector3.UnitY * camMoveSpeed;

            if (window.Keyboard?.IsKeyPressed(Key.Keypad1) ?? false)
                camera.LookAt(camera.Eye, camera.Eye + new Vector3(0, 0, -1));
            if (window.Keyboard?.IsKeyPressed(Key.Keypad3) ?? false)
                camera.LookAt(camera.Eye, camera.Eye + new Vector3(1, 0, 0));
            if (window.Keyboard?.IsKeyPressed(Key.Keypad7) ?? false)
                camera.LookAt(camera.Eye, camera.Eye + new Vector3(0, -1, 0));

            if (window.Keyboard?.IsKeyPressed(Key.Pause) ?? false)
                camera.LookAt(new(0, 1, 5), camera.Eye + new Vector3(0, 0, -1));
        }

        #endregion

        camera.Animate(deltaSeconds, out Vector3 eyeAnimated, out Quaternion rotAnimated);

        Matrix4x4 viewMatrix =
            Matrix4x4.CreateTranslation(-eyeAnimated)
            * Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(rotAnimated));

        Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
            (float)(Math.PI * 0.25),
            aspectRatio,
            1,
            100000f
        );

        Matrix4x4 viewProjection = viewMatrix * projectionMatrix;

        float yScale = 1.0f / (float)Math.Tan(0.5f);
        float xScale = yScale / aspectRatio;

        Vector2 ndcMousePos =
            ((mousePos - sceneWindowRegionMin) / sceneImageSize.Y * 2 - Vector2.One)
            * new Vector2(1, -1);

        Vector3 mouseRayDirection = Vector3.Transform(
            Vector3.Normalize(new(ndcMousePos.X / xScale, ndcMousePos.Y / yScale, -1)),
            rotAnimated
        );

        window.SceneFramebuffer.Use(window.GL!);
        window.GL!.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        window.CurrentScene?.Render(window.GL, viewMatrix, projectionMatrix);

        InfiniteGrid.Render(window.GL, viewProjection);

        CameraState cameraState =
            new(
                eyeAnimated,
                Vector3.Transform(-Vector3.UnitZ, rotAnimated),
                Vector3.Transform(Vector3.UnitY, rotAnimated),
                rotAnimated
            );

        SceneViewState sceneViewState =
            new(
                cameraState,
                viewProjection,
                new Rect(sceneWindowRegionMin, sceneWindowRegionMax),
                mousePos,
                mouseRayDirection
            );

        GizmoDrawer.BeginGizmoDrawing(
            "sceneWindowGizmos",
            ImGui.GetWindowDrawList(),
            in sceneViewState
        );

        Vector2 upperRightCorner = new(sceneImageRectMax.X, sceneImageRectMin.Y);

        bool orientationCubeHovered = GizmoDrawer.OrientationCube(
            upperRightCorner + new Vector2(-90, 80),
            radius: 70,
            out Vector3 facingDirection
        );

        if (
            ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && isSceneHovered
            && window.CurrentScene is not null
        )
        {
            if (orientationCubeHovered)
            {
                camera.LookAt(camera.Eye, camera.Eye - facingDirection);
                return;
            }

            Vector2 pixelPos = mousePos - sceneImageRectMin;

            window.GL.BindBuffer(BufferTargetARB.PixelPackBuffer, 0);
            window.GL.ReadBuffer(ReadBufferMode.ColorAttachment1);

            window.GL.ReadPixels(
                (int)pixelPos.X,
                (int)(sceneImageSize.Y - pixelPos.Y),
                1,
                1,
                PixelFormat.RedInteger,
                PixelType.UnsignedInt,
                out uint pixel
            );

            window.GL.ReadPixels(
                (int)pixelPos.X,
                (int)(sceneImageSize.Y - pixelPos.Y),
                1,
                1,
                PixelFormat.DepthComponent,
                PixelType.Float,
                out float normPickingDepth
            );

            // 3D mouse position calculation
            Vector2 windowMousePos = mousePos - sceneImageRectMin;
            ndcMousePos = (windowMousePos / sceneImageSize * 2 - Vector2.One) * new Vector2(1, -1);
            Vector4 ndcMousePos3D = new(ndcMousePos, normPickingDepth * 2 - 1, 1.0f);

            bool canInvert = Matrix4x4.Invert(viewProjection, out Matrix4x4 inverseViewProjection);
            Debug.Assert(canInvert);

            Vector4 worldMousePos = Vector4.Transform(ndcMousePos3D, inverseViewProjection);
            worldMousePos /= worldMousePos.W;

            if (_mouseClickActions.TryDequeue(out var action))
            {
                action(window, worldMousePos);
                return;
            }

            ChangeHandler.ToggleObjectSelection(
                window,
                window.CurrentScene.History,
                pixel,
                !(window.Keyboard?.IsCtrlPressed() ?? false)
            );
        }

        GizmoDrawer.EndGizmoDrawing();
        ImGui.End();
    }
}
