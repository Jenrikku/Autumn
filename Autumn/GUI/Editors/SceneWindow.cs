using Autumn.Scene;
using Autumn.Utils;
using ImGuiNET;
using SceneGL.GLHelpers;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Autumn.GUI.Editors;

internal class SceneWindow
{
    private static bool _persistentMouseDrag = false;
    private static Vector2 _previousMousePos = Vector2.Zero;

    public static unsafe void Render(MainWindowContext context, double deltaSeconds)
    {
        float aspectRatio = 1;

        bool isSceneHovered = false;
        bool isSceneWindowFocused = false;

        Vector2 sceneImageRectMin = new();
        int sceneImageHeight = 0;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0));

        if (
            !ImGui.Begin("Scene", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        )
            return;

        Vector2 contentAvail = ImGui.GetContentRegionAvail();
        aspectRatio = contentAvail.X / contentAvail.Y;

        if (contentAvail.X > 0 && contentAvail.Y > 0)
        {
            sceneImageHeight = (int)contentAvail.Y;

            context.SceneFramebuffer.SetSize((uint)contentAvail.X, (uint)contentAvail.Y);
            context.SceneFramebuffer.Create(context.GL!);

            ImGui.Image(
                new IntPtr(context.SceneFramebuffer.GetColorTexture(0)),
                contentAvail,
                new Vector2(0, 1),
                new Vector2(1, 0)
            );

            isSceneHovered = ImGui.IsItemHovered();
            isSceneWindowFocused = ImGui.IsWindowFocused();
            sceneImageRectMin = ImGui.GetItemRectMin();
        }

        ImGui.End();

        ImGui.PopStyleVar();

        #region Input

        Vector2 mousePos = ImGui.GetMousePos();

        if (
            (isSceneHovered || _persistentMouseDrag)
            && ImGui.IsMouseDragging(ImGuiMouseButton.Right)
        )
        {
            Vector2 delta = mousePos - _previousMousePos;

            Vector3 right = Vector3.Transform(Vector3.UnitX, context.Camera.Rotation);
            context.Camera.Rotation =
                Quaternion.CreateFromAxisAngle(right, -delta.Y * 0.002f) * context.Camera.Rotation;

            context.Camera.Rotation =
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, -delta.X * 0.002f)
                * context.Camera.Rotation;

            _persistentMouseDrag = true;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Right))
            _persistentMouseDrag = false;

        _previousMousePos = mousePos;

        if (isSceneHovered || isSceneWindowFocused)
        {
            float camMoveSpeed = (float)(0.4 * deltaSeconds * 60);

            if (context.Keyboard?.IsKeyPressed(Key.W) ?? false)
                context.Camera.Eye -= Vector3.Transform(
                    Vector3.UnitZ * camMoveSpeed,
                    context.Camera.Rotation
                );
            if (context.Keyboard?.IsKeyPressed(Key.S) ?? false)
                context.Camera.Eye += Vector3.Transform(
                    Vector3.UnitZ * camMoveSpeed,
                    context.Camera.Rotation
                );

            if (context.Keyboard?.IsKeyPressed(Key.A) ?? false)
                context.Camera.Eye -= Vector3.Transform(
                    Vector3.UnitX * camMoveSpeed,
                    context.Camera.Rotation
                );
            if (context.Keyboard?.IsKeyPressed(Key.D) ?? false)
                context.Camera.Eye += Vector3.Transform(
                    Vector3.UnitX * camMoveSpeed,
                    context.Camera.Rotation
                );

            if (context.Keyboard?.IsKeyPressed(Key.Q) ?? false)
                context.Camera.Eye -= Vector3.UnitY * camMoveSpeed;
            if (context.Keyboard?.IsKeyPressed(Key.E) ?? false)
                context.Camera.Eye += Vector3.UnitY * camMoveSpeed;

            if (context.Keyboard?.IsKeyPressed(Key.Keypad1) ?? false)
                context.Camera.LookAt(
                    context.Camera.Eye,
                    context.Camera.Eye + new Vector3(0, 0, -1)
                );
            if (context.Keyboard?.IsKeyPressed(Key.Keypad3) ?? false)
                context.Camera.LookAt(
                    context.Camera.Eye,
                    context.Camera.Eye + new Vector3(1, 0, 0)
                );
            if (context.Keyboard?.IsKeyPressed(Key.Keypad7) ?? false)
                context.Camera.LookAt(
                    context.Camera.Eye,
                    context.Camera.Eye + new Vector3(0, -1, 0)
                );

            if (context.Keyboard?.IsKeyPressed(Key.Pause) ?? false)
                context.Camera.LookAt(new(0, 1, 5), context.Camera.Eye + new Vector3(0, 0, -1));
        }

        #endregion

        context.Camera.Animate(deltaSeconds, out Vector3 eyeAnimated, out Quaternion rotAnimated);

        Matrix4x4 viewMatrix =
            Matrix4x4.CreateTranslation(-eyeAnimated)
            * Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(rotAnimated));

        //Matrix4x4 projectionMatrix = MathUtils.CreatePerspectiveReversedDepth((float)(Math.PI * 0.25), aspectRatio, 1f);
        Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
            (float)(Math.PI * 0.25),
            aspectRatio,
            1,
            100000f
        );
        //Matrix4x4 projectionMatrix = MathUtils.CreatePerspectiveFieldOfView((float)(Math.PI * 0.25), aspectRatio, 1f, 100000f);

        //Vector3 dir = - MathUtils.MultiplyQuaternionAndVector3(rotAnimated, Vector3.UnitZ);
        //Vector3 up = MathUtils.MultiplyQuartenionAndVector3(rotAnimated, Vector3.UnitY);

        //Console.WriteLine(dir);

        //Matrix3X4<float> lookAtMatrix = MathUtils.CreateLookAtMatrix(eyeAnimated, eyeAnimated + dir, Vector3.UnitY);

        context.SceneFramebuffer.Use(context.GL!);
        context.GL!.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        context.CurrentScene?.Render(context.GL, viewMatrix, projectionMatrix);

        if (
            ImGui.IsMouseClicked(ImGuiMouseButton.Left)
            && isSceneHovered
            && context.CurrentScene is not null
        )
        {
            Vector2 pixelPos = mousePos - sceneImageRectMin;

            context.GL.BindBuffer(BufferTargetARB.PixelPackBuffer, 0);
            context.GL.ReadBuffer(ReadBufferMode.ColorAttachment1);

            context.GL.ReadPixels(
                (int)pixelPos.X,
                sceneImageHeight - (int)pixelPos.Y,
                1,
                1,
                PixelFormat.RedInteger,
                PixelType.UnsignedInt,
                out uint pixel
            );

            if (
                !(context.Keyboard?.IsKeyPressed(Key.ControlLeft) ?? false)
                || !(context.Keyboard?.IsKeyPressed(Key.ControlRight) ?? false)
            )
                context.CurrentScene.UnselectAllObjects();

            context.CurrentScene.ToggleObjectSelection(pixel);
        }

        InfiniteGrid.Render(context.GL, viewMatrix * projectionMatrix);
    }
}
