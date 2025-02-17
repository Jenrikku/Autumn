using System.Diagnostics;
using System.Numerics;
using Autumn.GUI.Windows;
using Autumn.Rendering;
using Autumn.Rendering.Gizmo;
using Autumn.Rendering.Storage;
using Autumn.Utils;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;

namespace Autumn.GUI.Editors;

internal class SceneWindow(MainWindowContext window)
{
    public bool IsTransformActive => isTranslationActive || isRotationActive || isScaleActive;

    public bool isTranslationFromDuplicate = false;
    private bool isTranslationActive = false;
    private bool isRotationActive = false;
    private bool isScaleActive = false;
    private string transformChangeString = "";

    internal static class ActTransform
    {
        public static Dictionary<ISceneObj, Vector3> Relative = new();
        public static Dictionary<ISceneObj, Vector3> Originals = new();
        public static string FullTransformString = "";
    }

    private Vector3 axisLock = Vector3.One;
    private bool _persistentMouseDrag = false;
    private Vector2 _previousMousePos = Vector2.Zero;
    private Queue<Action<MainWindowContext, Vector4>> _mouseClickActions = new();

    private ImGuiMouseButton mouseMoveKey = ImGuiMouseButton.Right;
    private bool isSceneHovered;
    private bool isSceneWindowFocused;

    public void AddMouseClickAction(Action<MainWindowContext, Vector4> action)
    {
        _mouseClickActions.Enqueue(action);
    }

    public void GetAxis()
    {
        if (ImGui.IsKeyPressed(ImGuiKey.X, false))
        {
            if (axisLock.Y != 0 || axisLock.Z != 0)
                axisLock = Vector3.UnitX;
            else
                axisLock = Vector3.One;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.Z, false))
        {
            if (axisLock.X != 0 || axisLock.Y != 0)
                axisLock = Vector3.UnitZ;
            else
                axisLock = Vector3.One;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.Y, false))
        {
            if (axisLock.Z != 0 || axisLock.X != 0)
                axisLock = Vector3.UnitY;
            else
                axisLock = Vector3.One;
        }
    }

    public unsafe void Render(double deltaSeconds)
    {
        if (window.CurrentScene is null)
            return;

        float aspectRatio;

        Vector2 sceneImageRectMin;
        Vector2 sceneImageRectMax;
        Vector2 sceneImageSize;

        bool sceneReady = window.CurrentScene?.IsReady ?? false;

        if (sceneReady)
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0));

        if (!ImGui.Begin("Scene", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            return;

        if (!sceneReady)
        {
            ImGui.TextDisabled("The stage is being loaded, please wait...");
            ImGui.End();
            return;
        }

        Vector2 contentAvail = ImGui.GetContentRegionAvail() - new Vector2(0, 24);
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

        if (window.ContextHandler.SystemSettings.UseMiddleMouse)
            mouseMoveKey = ImGuiMouseButton.Middle;
        else
            mouseMoveKey = ImGuiMouseButton.Right;

        if ((isSceneHovered || _persistentMouseDrag) && ImGui.IsMouseDragging(mouseMoveKey))
        {
            Vector2 delta = mousePos - _previousMousePos;

            if (!ImGui.IsKeyDown(ImGuiKey.ModShift) || window.ContextHandler.SystemSettings.UseWASD)
            {
                Vector3 right = Vector3.Transform(Vector3.UnitX, camera.Rotation);
                camera.Rotation =
                    Quaternion.CreateFromAxisAngle(
                        right,
                        -delta.Y * 0.0001f * window.ContextHandler.SystemSettings.MouseSpeed
                    ) * camera.Rotation;

                camera.Rotation =
                    Quaternion.CreateFromAxisAngle(
                        Vector3.UnitY,
                        -delta.X * 0.0001f * window.ContextHandler.SystemSettings.MouseSpeed
                    ) * camera.Rotation;

                _persistentMouseDrag = true;
            }
            else if (!window.ContextHandler.SystemSettings.UseWASD)
            {
                if (delta != Vector2.Zero)
                {
                    delta *= 0.01f * window.ContextHandler.SystemSettings.MouseSpeed / 7;
                    Vector3 right = Vector3.Transform(new Vector3(-delta.X, delta.Y, 0), camera.Rotation);
                    camera.Eye += right;
                }
            }

            ImGui.SetWindowFocus();
        }

        if (!ImGui.IsMouseDown(mouseMoveKey))
            _persistentMouseDrag = false;

        _previousMousePos = mousePos;

        if (isSceneHovered || isSceneWindowFocused)
        {
            // Camera Movement
            float camMoveSpeed = (float)(0.4 * deltaSeconds * 60);
            camMoveSpeed *=
                window.Keyboard!.IsKeyPressed(Key.ShiftRight) || window.Keyboard.IsKeyPressed(Key.ShiftLeft) ? 6 : 1;

            if (window.ContextHandler.SystemSettings.UseWASD)
            {
                if (!ImGui.IsKeyDown(ImGuiKey.ModCtrl) && !ImGui.IsKeyDown(ImGuiKey.ModSuper))
                {
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
                }
            }

            if (window.Mouse!.ScrollWheels[0].Y != 0 && isSceneHovered)
            {
                camera.Eye -= Vector3.Transform(
                    Vector3.UnitZ * window.Mouse.ScrollWheels[0].Y * 6 * camMoveSpeed,
                    camera.Rotation
                );
            }

            if (window.Keyboard?.IsKeyPressed(Key.Keypad0) ?? false)
                camera.LookAt(camera.Eye, camera.Eye + new Vector3(1, 0, 0));

            if (window.Keyboard?.IsKeyPressed(Key.Keypad2) ?? false)
                camera.LookAt(camera.Eye, camera.Eye + new Vector3(0, 1, 0));

            if (window.Keyboard?.IsKeyPressed(Key.Keypad4) ?? false)
                camera.LookAt(camera.Eye, camera.Eye + new Vector3(0, 0, 1));

            if (window.Keyboard?.IsKeyPressed(Key.Keypad5) ?? false)
                camera.LookAt(camera.Eye, camera.Eye + new Vector3(-1, 0, 0));

            if (window.Keyboard?.IsKeyPressed(Key.Keypad6) ?? false)
                camera.LookAt(camera.Eye, camera.Eye + new Vector3(0, -1, 0));

            if (window.Keyboard?.IsKeyPressed(Key.Keypad7) ?? false)
                camera.LookAt(camera.Eye, camera.Eye + new Vector3(0, 0, -1));

            // if ((window.Keyboard?.IsKeyPressed(Key.Space) ?? false) && window.CurrentScene.SelectedObjects.Count() > 0){
            //     camera.LookAt(camera.Eye, window.CurrentScene.SelectedObjects.First().StageObj.Translation*0.01f);
            // }

            if ((window.Keyboard?.IsKeyPressed(Key.Space) ?? false) && window.CurrentScene.SelectedObjects.Any())
            {
                AxisAlignedBoundingBox aabb =
                    window.CurrentScene.SelectedObjects.First().AABB
                    * window.CurrentScene.SelectedObjects.First().StageObj.Scale;
                camera.LookFrom(
                    window.CurrentScene.SelectedObjects.First().StageObj.Translation * 0.01f,
                    aabb.GetDiagonal() * 0.01f
                );
            }
        }

        #endregion

        camera.Animate(deltaSeconds, out Vector3 eyeAnimated, out Quaternion rotAnimated);

        Matrix4x4 viewMatrix =
            Matrix4x4.CreateTranslation(-eyeAnimated) * Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(rotAnimated));

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
            ((mousePos - sceneWindowRegionMin) / sceneImageSize.Y * 2 - Vector2.One) * new Vector2(1, -1);

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

        GizmoDrawer.BeginGizmoDrawing("sceneWindowGizmos", ImGui.GetWindowDrawList(), in sceneViewState);

        Vector2 upperRightCorner = new(sceneImageRectMax.X, sceneImageRectMin.Y);

        bool orientationCubeHovered = GizmoDrawer.OrientationCube(
            upperRightCorner + new Vector2(-60, 60),
            radius: 45,
            out Vector3 facingDirection
        );

        if (window.CurrentScene is not null)
        {
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

            if (
                ImGui.IsMouseClicked(ImGuiMouseButton.Left)
                && isSceneHovered
                && !isTranslationActive
                && !isRotationActive
                && !isScaleActive
            )
            {
                if (!isSceneWindowFocused)
                    ImGui.SetWindowFocus();
                if (orientationCubeHovered)
                {
                    camera.LookAt(camera.Eye, camera.Eye - facingDirection);
                    return;
                }

                if (_mouseClickActions.TryDequeue(out var action))
                {
                    action(window, worldMousePos);
                    return;
                }

                ChangeHandler.ToggleObjectSelection(
                    window,
                    window.CurrentScene.History,
                    pixel,
                    !(window.Keyboard?.IsShiftPressed() ?? false)
                );
            }
            else if (
                (isSceneHovered && window.CurrentScene.SelectedObjects.Any())
                || isTranslationActive
                || isScaleActive
                || isRotationActive
            )
            {
                Vector3 _ndcMousePos3D =
                    new(
                        ndcMousePos.X * sceneImageSize.X / 2,
                        ndcMousePos.Y * sceneImageSize.Y / 2,
                        (normPickingDepth * 10 - 1) / 10f
                    );

                _ndcMousePos3D = Vector3.Transform(_ndcMousePos3D, window.CurrentScene.Camera.Rotation);

                if (ImGui.IsKeyPressed(ImGuiKey.G, false) && window.Keyboard!.IsShiftPressed())
                {
                    var sobj = window.CurrentScene.SelectedObjects.First();
                    ChangeHandler.ChangeTransform(
                        window.CurrentScene.History,
                        sobj,
                        "Translation",
                        sobj.StageObj.Translation,
                        100 * new Vector3(worldMousePos.X, worldMousePos.Y, worldMousePos.Z)
                    );

                    if (!isSceneWindowFocused)
                        ImGui.SetWindowFocus();
                }
                else
                {
                    TranslateAction(_ndcMousePos3D);
                }

                RotateAction(ndcMousePos);
                ScaleAction(_ndcMousePos3D);
            }
        }

        GizmoDrawer.EndGizmoDrawing();
        ActionPanel(contentAvail);
        ImGui.End();
    }

    private void ActionPanel(Vector2 contentAvail)
    {
        var opos = ImGui.GetCursorPos();
        if (isTranslationActive || isScaleActive || isRotationActive)
        {
            ImGui.SetWindowFontScale(1.0f);
            string s = "";

            if (isTranslationActive)
                s = "Moving ";
            else if (isScaleActive)
                s = "Scaling ";
            else if (isRotationActive)
                s = "Rotating ";

            if (window.CurrentScene!.SelectedObjects.Count() > 1)
                s += "multiple objects";
            else
                s += window.CurrentScene.SelectedObjects.First().StageObj.Name;

            if (axisLock != Vector3.One)
            {
                s += " on the ";

                if (axisLock == Vector3.UnitX)
                    s += "X ";
                else if (axisLock == Vector3.UnitY)
                    s += "Y ";
                else
                    s += "Z ";

                s += "axis";

                if (transformChangeString != "-" && transformChangeString != "" && axisLock != Vector3.One)
                    s += ": " + (transformChangeString != "-" ? transformChangeString : "");
                else
                    s += ": " + ActTransform.FullTransformString;
            }
            else
            {
                s += ": " + ActTransform.FullTransformString;
            }

            ImGui.SetCursorPos(opos + new Vector2(8, -2));
            ImGui.Text(s);
            ImGui.SetWindowFontScale(1.0f);
            ImGui.SetCursorPos(opos);
        }

        ImGui.PushFont(window.FontPointers[1]);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, default));
        ImGui.SetCursorPos(contentAvail - new Vector2(ImGui.CalcTextSize("\uf065").X * 3 + 3, -24));

        if (ImGui.Button("\uf065"))
        {
            ModelRenderer.VisibleAreas = !ModelRenderer.VisibleAreas;
        }

        ImGui.SameLine();

        if (ImGui.Button("\uf083"))
        {
            ModelRenderer.VisibleCameraAreas = !ModelRenderer.VisibleCameraAreas;
        }

        ImGui.PopStyleVar(2);
        ImGui.PopFont();
        ImGui.SetCursorPos(opos);
    }

    public void TranslateAction(Vector3 _ndcMousePos3D)
    {
        if (isRotationActive || isScaleActive)
            return;

        float dist = 0;
        if (isTranslationActive)
        {
            if (!isSceneWindowFocused)
                ImGui.SetWindowFocus();

            //multiply by distance to camera to make it responsive at different distances

            GetAxis();
            if (axisLock != Vector3.One)
            {
                TransformChange();
            }
            else
                transformChangeString = "";

            dist = Vector3.Distance(
                ActTransform.Originals[window.CurrentScene!.SelectedObjects.First()] / 100,
                window.CurrentScene.Camera.Eye
            );

            _ndcMousePos3D *= dist / 8;

            foreach (ISceneObj scobj in window.CurrentScene.SelectedObjects)
            {
                Vector3 defPos = ActTransform.Originals[scobj] - (ActTransform.Relative[scobj] + _ndcMousePos3D); // default position

                if (transformChangeString != string.Empty && transformChangeString != "-")
                {
                    defPos = -Vector3.One * float.Parse(transformChangeString);
                }

                Vector3 nTr = ActTransform.Originals[scobj] - defPos * axisLock;
                scobj.StageObj.Translation = nTr;

                if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
                {
                    scobj.StageObj.Translation = MathUtils.Round(scobj.StageObj.Translation / 50) * 50;
                }

                scobj.UpdateTransform();
            }

            var STR = window.CurrentScene.SelectedObjects.First().StageObj.Translation;

            if (axisLock == Vector3.One)
            {
                ActTransform.FullTransformString = $"X: {STR.X}, Y: {STR.Y}, Z: {STR.Z}";
            }
            else
            {
                if (axisLock == Vector3.UnitX)
                    ActTransform.FullTransformString = $" {STR.X}";

                if (axisLock == Vector3.UnitY)
                    ActTransform.FullTransformString = $" {STR.Y}";

                if (axisLock == Vector3.UnitZ)
                    ActTransform.FullTransformString = $" {STR.Z}";
            }
        }

        if ((ImGui.IsKeyPressed(ImGuiKey.G, false) && !isTranslationActive) || isTranslationFromDuplicate)
        { // Start action
            isTranslationFromDuplicate = false;
            isTranslationActive = true;

            // Only get distance to first object to prevent misalignments
            dist = Vector3.Distance(
                window.CurrentScene!.SelectedObjects.First().StageObj.Translation / 100,
                window.CurrentScene.Camera.Eye
            );

            _ndcMousePos3D *= dist / 8;

            foreach (ISceneObj scobj in window.CurrentScene.SelectedObjects)
            {
                ActTransform.Originals.Add(scobj, scobj.StageObj.Translation);
                ActTransform.Relative[scobj] = scobj.StageObj.Translation - _ndcMousePos3D;
            }
        }
        else if (
            (
                ImGui.IsKeyPressed(ImGuiKey.G, false)
                || ImGui.IsKeyPressed(ImGuiKey.MouseLeft, false)
                || ImGui.IsKeyPressed(ImGuiKey.Enter, false)
            ) && isTranslationActive
        )
        { // Apply action
            isTranslationActive = false;
            axisLock = Vector3.One;

            // Add to Undo stack
            if (window.CurrentScene!.SelectedObjects.Count() == 1)
            {
                var sobj = window.CurrentScene.SelectedObjects.First();
                ChangeHandler.ChangeTransform(
                    window.CurrentScene.History,
                    sobj,
                    "Translation",
                    ActTransform.Originals[sobj],
                    sobj.StageObj.Translation
                );
            }
            else
            {
                ChangeHandler.ChangeMultiTransform(window.CurrentScene.History, ActTransform.Originals, "Translation");
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
            transformChangeString = "";
            window.CurrentScene.IsSaved = false;
        }
        else if (
            (ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            && isTranslationActive
        )
        { // Cancel action
            isTranslationActive = false;

            foreach (ISceneObj scobj in window.CurrentScene!.SelectedObjects)
            {
                scobj.StageObj.Translation = ActTransform.Originals[scobj]; // Reset to what it was
                scobj.UpdateTransform();
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
            axisLock = Vector3.One;
            transformChangeString = "";
        }
    }

    public void RotateAction(Vector2 ndcMousePos)
    {
        if (isScaleActive || isTranslationActive)
            return;

        //Console.WriteLine(ndcMousePos);
        double rot;
        float mDist = Vector2.Distance(Vector2.Zero, ndcMousePos);
        //Console.WriteLine(Math.Asin(ndcMousePos.Y/mDist)*180/Math.PI);

        if (Math.Asin(ndcMousePos.Y / mDist) * 180 / Math.PI < 0)
        {
            if (Math.Acos(ndcMousePos.X / mDist) * 180 / Math.PI > 90)
                rot = 180.0 - Math.Asin(ndcMousePos.Y / mDist) * 180 / Math.PI;
            else
                rot = 360.0 + Math.Asin(ndcMousePos.Y / mDist) * 180 / Math.PI;
        }
        else
        {
            rot = Math.Acos(ndcMousePos.X / mDist) * 180 / Math.PI;
        }

        //Console.WriteLine(rot);
        //Console.WriteLine("---------");

        // We rotate around the center of the screen using a given axis, defaults to Y
        if (isRotationActive)
        {
            if (!isSceneWindowFocused)
                ImGui.SetWindowFocus();

            GetAxis();
            if (axisLock != Vector3.One)
                TransformChange();
            else
                transformChangeString = "";

            if (axisLock == Vector3.One)
                axisLock = Vector3.UnitY;

            foreach (ISceneObj sobj in window.CurrentScene!.SelectedObjects)
            {
                if (transformChangeString != string.Empty && transformChangeString != "-")
                {
                    sobj.StageObj.Rotation =
                        ActTransform.Originals[sobj] + axisLock * float.Parse(transformChangeString);
                }
                else
                {
                    sobj.StageObj.Rotation =
                        ActTransform.Originals[sobj] + axisLock * (-ActTransform.Relative[sobj].X + (float)rot);
                }

                if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
                {
                    sobj.StageObj.Rotation = MathUtils.Round(sobj.StageObj.Rotation / 5) * 5;
                }

                sobj.UpdateTransform();
            }

            var STR = window.CurrentScene.SelectedObjects.First().StageObj.Rotation;

            if (axisLock == Vector3.One)
            {
                ActTransform.FullTransformString = $"X: {STR.X}, Y: {STR.Y}, Z: {STR.Z}";
            }
            else
            {
                if (axisLock == Vector3.UnitX)
                    ActTransform.FullTransformString = $" {STR.X}";

                if (axisLock == Vector3.UnitY)
                    ActTransform.FullTransformString = $" {STR.Y}";

                if (axisLock == Vector3.UnitZ)
                    ActTransform.FullTransformString = $" {STR.Z}";
            }
        }

        if (ImGui.IsKeyPressed(ImGuiKey.R, false) && !isRotationActive)
        { // Start action
            isRotationActive = true;

            foreach (ISceneObj sobj in window.CurrentScene!.SelectedObjects)
            {
                ActTransform.Originals.Add(sobj, sobj.StageObj.Rotation);
                ActTransform.Relative.Add(sobj, Vector3.UnitX * (float)rot);
            }
        }
        else if (
            (
                ImGui.IsKeyPressed(ImGuiKey.R, false)
                || ImGui.IsKeyPressed(ImGuiKey.MouseLeft, false)
                || ImGui.IsKeyPressed(ImGuiKey.Enter, false)
            ) && isRotationActive
        )
        { // Apply action
            isRotationActive = false;
            axisLock = Vector3.One;

            if (window.CurrentScene!.SelectedObjects.Count() == 1)
            {
                var sobj = window.CurrentScene.SelectedObjects.First();
                ChangeHandler.ChangeTransform(
                    window.CurrentScene.History,
                    sobj,
                    "Rotation",
                    ActTransform.Originals[sobj],
                    sobj.StageObj.Rotation
                );
            }
            else
            {
                ChangeHandler.ChangeMultiTransform(window.CurrentScene.History, ActTransform.Originals, "Rotation");
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
            transformChangeString = "";
            window.CurrentScene.IsSaved = false;
            // Add to Undo stack
        }
        else if (
            (ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            && isRotationActive
        )
        { // Cancel action
            isRotationActive = false;

            foreach (ISceneObj sobj in window.CurrentScene!.SelectedObjects)
            {
                sobj.StageObj.Rotation = ActTransform.Originals[sobj];
                sobj.UpdateTransform();
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
            axisLock = Vector3.One;
        }
    }

    public void ScaleAction(Vector3 _ndcMousePos3D)
    { // Get distance to object and if it decreases we scale down, otherwise we increase, by default it scales in all axis, can scale on individual axis
        if (isRotationActive || isTranslationActive)
            return;

        float dist = 0;
        if (isScaleActive)
        {
            if (!isSceneWindowFocused)
                ImGui.SetWindowFocus();
            //multiply by distance to camera

            GetAxis();

            if (axisLock != Vector3.One)
            {
                TransformChange();
            }
            else
                transformChangeString = "";

            dist = Vector3.Distance(
                window.CurrentScene!.SelectedObjects.First().StageObj.Translation / 100,
                window.CurrentScene.Camera.Eye
            );

            _ndcMousePos3D *= dist / 2;

            foreach (ISceneObj sobj in window.CurrentScene.SelectedObjects)
            {
                sobj.StageObj.Scale = ActTransform.Originals[sobj];
                float distA = Vector3.Distance(window.CurrentScene.Camera.Eye, ActTransform.Relative[sobj]);
                float distB = Vector3.Distance(window.CurrentScene.Camera.Eye, _ndcMousePos3D);

                if (transformChangeString != string.Empty && transformChangeString != "-")
                {
                    sobj.StageObj.Scale =
                        ActTransform.Originals[sobj]
                        + Vector3.One * (distB - distA) / 500 * axisLock * float.Parse(transformChangeString); // original scale * (distance to selection from mouse )
                }
                else
                {
                    sobj.StageObj.Scale = ActTransform.Originals[sobj] + Vector3.One * (distB - distA) / 500 * axisLock; // original scale * (distance to selection from mouse )
                }

                if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
                {
                    sobj.StageObj.Scale = MathUtils.Round(sobj.StageObj.Scale * 10) / 10;
                }

                sobj.UpdateTransform();
            }

            var STR = window.CurrentScene.SelectedObjects.First().StageObj.Scale;
            if (axisLock == Vector3.One)
            {
                ActTransform.FullTransformString = $"X: {STR.X}, Y: {STR.Y}, Z: {STR.Z}";
            }
            else
            {
                if (axisLock == Vector3.UnitX)
                    ActTransform.FullTransformString = $" {STR.X}";
                if (axisLock == Vector3.UnitY)
                    ActTransform.FullTransformString = $" {STR.Y}";
                if (axisLock == Vector3.UnitZ)
                    ActTransform.FullTransformString = $" {STR.Z}";
            }
        }

        if (ImGui.IsKeyPressed(ImGuiKey.F, false) && !isScaleActive)
        { // Start action
            isScaleActive = true;
            dist = Vector3.Distance(
                window.CurrentScene!.SelectedObjects.First().StageObj.Translation / 100,
                window.CurrentScene.Camera.Eye
            );

            _ndcMousePos3D *= dist / 2;

            foreach (ISceneObj sobj in window.CurrentScene.SelectedObjects)
            {
                ActTransform.Originals.Add(sobj, sobj.StageObj.Scale);
                ActTransform.Relative.Add(sobj, _ndcMousePos3D);
            }
        }
        else if (
            (
                ImGui.IsKeyPressed(ImGuiKey.F, false)
                || ImGui.IsKeyPressed(ImGuiKey.MouseLeft, false)
                || ImGui.IsKeyPressed(ImGuiKey.Enter, false)
            ) && isScaleActive
        )
        { // Apply action
            isScaleActive = false;
            axisLock = Vector3.One;

            if (window.CurrentScene!.SelectedObjects.Count() == 1)
            {
                var sobj = window.CurrentScene.SelectedObjects.First();
                ChangeHandler.ChangeTransform(
                    window.CurrentScene.History,
                    sobj,
                    "Scale",
                    ActTransform.Originals[sobj],
                    sobj.StageObj.Scale
                );
            }
            else
            {
                ChangeHandler.ChangeMultiTransform(window.CurrentScene.History, ActTransform.Originals, "Scale");
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
            transformChangeString = "";
            window.CurrentScene.IsSaved = false;
            // Add to Undo stack
        }
        else if (
            (ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            && isScaleActive
        )
        { // Cancel action
            isScaleActive = false;

            foreach (ISceneObj sobj in window.CurrentScene!.SelectedObjects)
            {
                sobj.StageObj.Scale = ActTransform.Originals[sobj];
                sobj.UpdateTransform();
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
            axisLock = Vector3.One;
        }
    }

    private void TransformChange()
    {
        bool isPos = !transformChangeString.Contains('-');
        string r = isPos ? transformChangeString.Split('-')[0] : transformChangeString.Split('-')[1];

        if (ImGui.IsKeyPressed(ImGuiKey._0, false))
            r += "0";
        else if (ImGui.IsKeyPressed(ImGuiKey._1, false))
            r += "1";
        else if (ImGui.IsKeyPressed(ImGuiKey._2, false))
            r += "2";
        else if (ImGui.IsKeyPressed(ImGuiKey._3, false))
            r += "3";
        else if (ImGui.IsKeyPressed(ImGuiKey._4, false))
            r += "4";
        else if (ImGui.IsKeyPressed(ImGuiKey._5, false))
            r += "5";
        else if (ImGui.IsKeyPressed(ImGuiKey._6, false))
            r += "6";
        else if (ImGui.IsKeyPressed(ImGuiKey._7, false))
            r += "7";
        else if (ImGui.IsKeyPressed(ImGuiKey._8, false))
            r += "8";
        else if (ImGui.IsKeyPressed(ImGuiKey._9, false))
            r += "9";
        else if (ImGui.IsKeyPressed(ImGuiKey.Period) && !r.Contains('.'))
            r += ".";
        else if (ImGui.IsKeyPressed(ImGuiKey.Minus) || ImGui.IsKeyPressed(ImGuiKey.Apostrophe))
            isPos = !isPos;
        else if (ImGui.IsKeyPressed(ImGuiKey.Backspace) && r.Length > 0)
            r = r[..^1]; // Remove last

        transformChangeString = isPos ? r : "-" + r;
    }
}
