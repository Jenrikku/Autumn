using System.Diagnostics;
using System.Numerics;
using Autumn.Rendering;
using Autumn.Rendering.CtrH3D;
using Autumn.Rendering.Gizmo;
using Autumn.Utils;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;

namespace Autumn.GUI.Editors;

internal class SceneWindow(MainWindowContext window)
{
    private bool isTranslationActive = false;
    private bool isRotationActive = false;
    private bool isScaleActive = false;
    private float actionT = 0.0f;
    private Vector3[] ActiveTransform = [Vector3.Zero, Vector3.Zero]; // Relative / Original
    private Vector3 axisLock = Vector3.One;
    private bool _persistentMouseDrag = false;
    private Vector2 _previousMousePos = Vector2.Zero;
    private Queue<Action<MainWindowContext, Vector4>> _mouseClickActions = new();

    // Should be moved elsewhere
    private Vector3 Floor(Vector3 a)
    {
        a.X = (float)Math.Floor(a.X);
        a.Y = (float)Math.Floor(a.Y);
        a.Z = (float)Math.Floor(a.Z);
        return a;
    } 
    public void AddMouseClickAction(Action<MainWindowContext, Vector4> action)
    {
        _mouseClickActions.Enqueue(action);
    }
    public void GetAxis()
    {
        if (ImGui.IsKeyPressed(ImGuiKey.X, false))
        {
            if (axisLock.Y != 0 || axisLock.Z != 0) axisLock = Vector3.UnitX;
            else axisLock = Vector3.One;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.Z, false))
        {
            if (axisLock.X != 0 || axisLock.Y != 0) axisLock = Vector3.UnitZ;
            else axisLock = Vector3.One;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.Y, false))
        {
            if (axisLock.Z != 0 || axisLock.X != 0) axisLock = Vector3.UnitY;
            else axisLock = Vector3.One;
        }
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

        bool sceneReady = window.CurrentScene?.IsReady ?? false;

        if (sceneReady)
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0));

        if (
            !ImGui.Begin("Scene", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
        )
            return;

        if (!sceneReady)
        {
            ImGui.TextDisabled("The stage is being loaded, please wait...");
            ImGui.End();
            return;
        }

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
            camMoveSpeed *= !ImGui.IsKeyPressed(ImGuiKey.LeftShift)  ? 1 : 10;

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
            if ((window.Keyboard?.IsKeyPressed(Key.Space) ?? false) && window.CurrentScene.SelectedObjects.Count() > 0){
                AxisAlignedBoundingBox aabb = window.CurrentScene.SelectedObjects.First().Actor.AABB * window.CurrentScene.SelectedObjects.First().StageObj.Scale;
                camera.LookFrom(window.CurrentScene.SelectedObjects.First().StageObj.Translation*0.01f, aabb.GetDiagonal()*0.01f );
            }
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
            window.CurrentScene is not null 
        )
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

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)
                && isSceneHovered
                && !isTranslationActive && !isRotationActive && !isScaleActive)
            {
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
                    typeof(SceneObj),
                    !(window.Keyboard?.IsCtrlPressed() ?? false)
                );
            }
            else if ((isSceneHovered && window.CurrentScene.SelectedObjects.Any()) || isTranslationActive || isScaleActive || isRotationActive)
            {
                Vector3 _ndcMousePos3D = new(ndcMousePos.X * sceneImageSize.X/2, ndcMousePos.Y *sceneImageSize.Y/2, (normPickingDepth * 10 - 1)/10f);
                _ndcMousePos3D = Vector3.Transform(_ndcMousePos3D, window.CurrentScene.Camera.Rotation);
                if (ImGui.IsKeyPressed(ImGuiKey.T, false))
                {
                    window.CurrentScene.SelectedObjects.First().StageObj.Translation = 100* new Vector3(worldMousePos.X, worldMousePos.Y, worldMousePos.Z);
                    window.CurrentScene.SelectedObjects.First().UpdateTransform();
                }
                TranslateAction(_ndcMousePos3D);
                RotateAction(ndcMousePos);
                ScaleAction(_ndcMousePos3D);
            }
        }

        GizmoDrawer.EndGizmoDrawing();
        ImGui.End();
    }

    public void TranslateAction(Vector3 _ndcMousePos3D)
    {
        if (isRotationActive || isScaleActive) return;
        float dist = Vector3.Distance(ActiveTransform[1] / 100, window.CurrentScene.Camera.Eye);
        if (isTranslationActive)
        {
            //multiply by distance to camera to make it responsive at different distances
            _ndcMousePos3D *= dist/8;

            GetAxis();

            // by default we move on the camera projection plane

            Vector3 defPos = ActiveTransform[1]-(ActiveTransform[0] + _ndcMousePos3D); // default position
            Vector3 nTr = ActiveTransform[1] - defPos * axisLock;
            window.CurrentScene.SelectedObjects.First().StageObj.Translation = Vector3.Lerp(window.CurrentScene.SelectedObjects.First().StageObj.Translation, nTr, actionT);
            
            if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
            {
                window.CurrentScene.SelectedObjects.First().StageObj.Translation = Floor(window.CurrentScene.SelectedObjects.First().StageObj.Translation / 50) * 50 ; 
            }
            window.CurrentScene.SelectedObjects.First().UpdateTransform();

            actionT += 0.01f;
            if (actionT >1)
            {
                actionT = 1;
            }
        }
        if (ImGui.IsKeyPressed(ImGuiKey.G, false) && !isTranslationActive)
        {   // Start action
            isTranslationActive = true;
            ActiveTransform[1] = window.CurrentScene.SelectedObjects.First().StageObj.Translation; 
            dist = Vector3.Distance(ActiveTransform[1] / 100, window.CurrentScene.Camera.Eye);
            _ndcMousePos3D *= dist/8;
            ActiveTransform[0] = window.CurrentScene.SelectedObjects.First().StageObj.Translation - _ndcMousePos3D;
            actionT = 0f;
        }
        else if ((ImGui.IsKeyPressed(ImGuiKey.G, false) || ImGui.IsKeyPressed(ImGuiKey.MouseLeft, false) || ImGui.IsKeyPressed(ImGuiKey.Enter, false)) && isTranslationActive)
        {   // Apply action
            isTranslationActive = false;
            axisLock = Vector3.One;
            // Add to Undo stack
        }
        else if ((ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.IsKeyPressed(ImGuiKey.Escape, false)) && isTranslationActive)
        {   // Cancel action
            isTranslationActive = false;
            window.CurrentScene.SelectedObjects.First().StageObj.Translation = ActiveTransform[1]; // Reset to default
            window.CurrentScene.SelectedObjects.First().UpdateTransform();
            ActiveTransform = [Vector3.Zero, Vector3.Zero];
            axisLock = Vector3.One;
        }
    }
    public void RotateAction(Vector2 ndcMousePos)
    {
        if (isScaleActive || isTranslationActive) return;
        //Console.WriteLine(ndcMousePos);
        double rot;
        float mDist = Vector2.Distance(Vector2.Zero, ndcMousePos);
        //Console.WriteLine(Math.Asin(ndcMousePos.Y/mDist)*180/Math.PI);
        if (Math.Asin(ndcMousePos.Y / mDist) * 180 / Math.PI < 0)
        {
            if (Math.Acos(ndcMousePos.X / mDist) * 180 / Math.PI > 90) rot = 180.0 - Math.Asin(ndcMousePos.Y / mDist) * 180 / Math.PI;
            else rot = 360.0 + Math.Asin(ndcMousePos.Y / mDist) * 180 / Math.PI;
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
            GetAxis();
            if (axisLock == Vector3.One) axisLock = Vector3.UnitY;

            window.CurrentScene.SelectedObjects.First().StageObj.Rotation = ActiveTransform[1]+ axisLock * (-ActiveTransform[0].X + (float)rot);
            
            if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
            {
                window.CurrentScene.SelectedObjects.First().StageObj.Rotation = Floor(window.CurrentScene.SelectedObjects.First().StageObj.Rotation / 5) * 5 ; 
            }
            window.CurrentScene.SelectedObjects.First().UpdateTransform();
            actionT += 0.01f;
            if (actionT >1)
            {
                actionT = 1;
            }
        }
        if (ImGui.IsKeyPressed(ImGuiKey.R, false) && !isRotationActive)
        {   // Start action
            isRotationActive = true;
            ActiveTransform[1] = window.CurrentScene.SelectedObjects.First().StageObj.Rotation; 
            ActiveTransform[0] = Vector3.UnitX * (float)rot;
            actionT = 0f;
        }
        else if ((ImGui.IsKeyPressed(ImGuiKey.R, false) || ImGui.IsKeyPressed(ImGuiKey.MouseLeft, false) || ImGui.IsKeyPressed(ImGuiKey.Enter, false)) && isRotationActive)
        {   // Apply action
            isRotationActive = false;
            axisLock = Vector3.One;
            // Add to Undo stack
        }
        else if ((ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.IsKeyPressed(ImGuiKey.Escape, false)) && isRotationActive)
        {   // Cancel action
            isRotationActive = false;
            window.CurrentScene.SelectedObjects.First().StageObj.Rotation = ActiveTransform[1];
            window.CurrentScene.SelectedObjects.First().UpdateTransform();
            ActiveTransform = [Vector3.Zero, Vector3.Zero];
            axisLock = Vector3.One;
        }
    }
    public void ScaleAction(Vector3 _ndcMousePos3D)
    { // Get distance to object and if it decreases we scale down, otherwise we increase, by default it scales in all axis, can scale on individual axis
        if (isRotationActive || isTranslationActive) return;
        float dist = Vector3.Distance(window.CurrentScene.SelectedObjects.First().StageObj.Translation / 100, window.CurrentScene.Camera.Eye);
        if (isScaleActive)
        {
            //multiply by distance to camera
            _ndcMousePos3D  *= dist/2;

            GetAxis();

            window.CurrentScene.SelectedObjects.First().StageObj.Scale = Vector3.Lerp(window.CurrentScene.SelectedObjects.First().StageObj.Scale, ActiveTransform[1]*  Vector3.Distance(window.CurrentScene.SelectedObjects.First().StageObj.Translation,ActiveTransform[0]+_ndcMousePos3D)/100, actionT);
            float distA = Vector3.Distance(ActiveTransform[0], window.CurrentScene.SelectedObjects.First().StageObj.Translation);
            float distB = Vector3.Distance(window.CurrentScene.SelectedObjects.First().StageObj.Translation, window.CurrentScene.SelectedObjects.First().StageObj.Translation - _ndcMousePos3D);
            window.CurrentScene.SelectedObjects.First().StageObj.Scale = ActiveTransform[1] + Vector3.One *(distB-distA)/500 * axisLock; // original scale * (distance to selection )   

            if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
            {
                window.CurrentScene.SelectedObjects.First().StageObj.Scale = Floor(window.CurrentScene.SelectedObjects.First().StageObj.Scale * 10) / 10 ; 
            }

            window.CurrentScene.SelectedObjects.First().UpdateTransform();
            
            actionT += 0.01f;
            if (actionT >1)
            {
                actionT = 1;
            }
        }
        if (ImGui.IsKeyPressed(ImGuiKey.F, false) && !isScaleActive)
        {   // Start action
            isScaleActive = true;
            ActiveTransform[1] = window.CurrentScene.SelectedObjects.First().StageObj.Scale; 
            dist = Vector3.Distance(window.CurrentScene.SelectedObjects.First().StageObj.Translation / 100 , window.CurrentScene.Camera.Eye);
            _ndcMousePos3D *= dist/2;
            ActiveTransform[0] = window.CurrentScene.SelectedObjects.First().StageObj.Translation - _ndcMousePos3D;
            actionT = 0f;
        }
        else if ((ImGui.IsKeyPressed(ImGuiKey.F, false) || ImGui.IsKeyPressed(ImGuiKey.MouseLeft, false) || ImGui.IsKeyPressed(ImGuiKey.Enter, false)) && isScaleActive)
        {   // Apply action
            isScaleActive = false;
            axisLock = Vector3.One;
            // Add to Undo stack
        }
        else if ((ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.IsKeyPressed(ImGuiKey.Escape, false)) && isScaleActive)
        {   // Cancel action
            isScaleActive = false;
            window.CurrentScene.SelectedObjects.First().StageObj.Scale = ActiveTransform[1];
            window.CurrentScene.SelectedObjects.First().UpdateTransform();
            ActiveTransform = [Vector3.Zero, Vector3.Zero];
            axisLock = Vector3.One;
        } 
    }

}
