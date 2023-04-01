using AutumnSceneGL.GUI.Rendering;
using AutumnSceneGL.Utils;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;

namespace AutumnSceneGL.GUI.ImGUIWindows {
    internal class SceneWindow {
        private static bool _persistentMouseDrag = false;
        private static Vector2 _previousMousePos = Vector2.Zero;

        public static void Render(StageEditorContext context, double deltaSeconds) {
            float aspectRatio = 1;

            bool isSceneHovered = false;
            bool isSceneWindowFocused = false;

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0));

            if(!ImGui.Begin("Scene", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                return;

            Vector2 contentAvail = ImGui.GetContentRegionAvail();
            aspectRatio = contentAvail.X / contentAvail.Y;

            if(contentAvail.X > 0 && contentAvail.Y > 0) {
                context.Framebuffer.SetSize((uint) contentAvail.X, (uint) contentAvail.Y);
                context.Framebuffer.Create(context.GL!);

                ImGui.Image(
                    new nint(context.Framebuffer.GetColorTexture(0)),
                    contentAvail,
                    new Vector2(0, 1),
                    new Vector2(1, 0));

                isSceneHovered = ImGui.IsItemHovered();
                isSceneWindowFocused = ImGui.IsWindowFocused();
            }

            ImGui.End();

            ImGui.PopStyleVar();

            #region Input

            Vector2 mousePos = ImGui.GetMousePos();

            if((isSceneHovered || _persistentMouseDrag) && ImGui.IsMouseDragging(ImGuiMouseButton.Right)) {
                Vector2 delta = mousePos - _previousMousePos;

                Vector3 right = Vector3.Transform(Vector3.UnitX, context.Camera.Rotation);
                context.Camera.Rotation = Quaternion.CreateFromAxisAngle(right, -delta.Y * 0.002f) * context.Camera.Rotation;

                context.Camera.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -delta.X * 0.002f) * context.Camera.Rotation;

                _persistentMouseDrag = true;
            }

            if(!ImGui.IsMouseDown(ImGuiMouseButton.Right))
                _persistentMouseDrag = false;

            _previousMousePos = mousePos;

            if(isSceneHovered || isSceneWindowFocused) {
                float camMoveSpeed = (float) (0.4 * deltaSeconds * 60);

                if(context.Keyboard?.IsKeyPressed(Key.W) ?? false)
                    context.Camera.Eye -= Vector3.Transform(Vector3.UnitZ * camMoveSpeed, context.Camera.Rotation);
                if(context.Keyboard?.IsKeyPressed(Key.S) ?? false)
                    context.Camera.Eye += Vector3.Transform(Vector3.UnitZ * camMoveSpeed, context.Camera.Rotation);

                if(context.Keyboard?.IsKeyPressed(Key.A) ?? false)
                    context.Camera.Eye -= Vector3.Transform(Vector3.UnitX * camMoveSpeed, context.Camera.Rotation);
                if(context.Keyboard?.IsKeyPressed(Key.D) ?? false)
                    context.Camera.Eye += Vector3.Transform(Vector3.UnitX * camMoveSpeed, context.Camera.Rotation);

                if(context.Keyboard?.IsKeyPressed(Key.Q) ?? false)
                    context.Camera.Eye -= Vector3.UnitY * camMoveSpeed;
                if(context.Keyboard?.IsKeyPressed(Key.E) ?? false)
                    context.Camera.Eye += Vector3.UnitY * camMoveSpeed;
            }

            #endregion

            context.Camera.Animate(deltaSeconds, out var eyeAnimated, out var rotAnimated);

            context.ViewProjection =
                Matrix4x4.CreateTranslation(-eyeAnimated) *
                Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(rotAnimated)) *
                MatrixUtil.CreatePerspectiveReversedDepth(1f, aspectRatio, 0.1f);

            context.Framebuffer.Use(context.GL!);
            context.GL!.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            InfiniteGrid.Render(context.GL, in context.ViewProjection);

            context.CurrentScene?.Render(context.GL, context.Material!, context.ViewProjection);
        }
    }
}
