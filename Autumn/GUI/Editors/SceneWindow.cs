using System.Numerics;
using Autumn.GUI.Windows;
using Autumn.Enums;
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
    public bool IsWindowFocused => _isSceneWindowFocused;
    public bool IsSceneHovered => _isSceneHovered;
    public bool IsTransformActive => IsTranslationActive || IsRotationActive || IsScaleActive;
    public bool IsTransformFromGizmo = false;
    private byte _transformGizmoPosMulti = 1;
    public bool IsTranslationActive = false;
    public bool IsTranslationFromDuplicate = false;
    public bool TranslationStarted = false;
    public bool TranslateToPoint = false;

    public bool IsRotationActive = false;
    public bool RotationStarted = false;

    public bool IsScaleActive = false;
    public bool ScaleStarted = false;

    public bool FinishTransform = false;

    private string _transformChangeString = "";

    internal static class ActTransform
    {
        public static Dictionary<ISceneObj, Vector3> Relative = new();
        public static Dictionary<ISceneObj, Vector3> Originals = new();
        public static Dictionary<ISceneObj, Vector3> Finals = new();
        public static string FullTransformString = "";
    }

    public bool CamToObj = false;
    public ISceneObj? CamSceneObj;
    public AddRailPointState AddRailPoint = AddRailPointState.None; // 0 false, 1 to rail, 2 to point

    private int _transformGizmo = 0; // 0 none, 1 move, 2 rotate, 3 scale
    public int TransformGizmo
    {
        get { return _transformGizmo; }
        set
        {
            _transformGizmo = value > 3 ? 0 : (value < 0 ? 3 : value);
        }
    }

    private Vector3 _axisLock = Vector3.One;
    private bool _persistentMouseDrag = false;
    private Vector2 _previousMousePos = Vector2.Zero;
    private Queue<Action<MainWindowContext, Vector4>> _mouseClickActions = new();
    public int MouseClickActionsCount => _mouseClickActions.Count;
    private bool _isObjectOptionsEnabled = false;
    private Vector2 _objectOptionsPos = Vector2.Zero;
    private double _objectOptionsTime = 0;
    private bool _selCantParent = false;
    private bool _selCantChild = false;
    private bool _selNotSame = false;
    private ISceneObj? _pickObject;

    private ImGuiMouseButton _mouseMoveKey = ImGuiMouseButton.Right;
    private ImGuiKey _scaleKey = ImGuiKey.S;
    private bool _isSceneHovered;
    private bool _isSceneWindowFocused;

    private Vector2 _viewportSize;
    private Vector2 _innerPadding = new Vector2(-40, 40);
    private float _cubeSize = 45;
    private Matrix4x4 tl = Matrix4x4.Identity;
    private Matrix4x4 rt = Matrix4x4.Identity;
    private Vector3 trls = Vector3.Zero;
    private Vector3 rots = Vector3.Zero;
    // private Vector3 scls = Vector3.Zero;
    public void AddMouseClickAction(Action<MainWindowContext, Vector4> action)
    {
        _mouseClickActions.Enqueue(action);
    }

    public void GetAxis()
    {
        if (ImGui.IsKeyPressed(ImGuiKey.X, false))
        {
            if (ImGui.IsKeyDown(ImGuiKey.ModShift))
                _axisLock = Vector3.One - Vector3.UnitX;
            else if (_axisLock.Y != 0 || _axisLock.Z != 0)
                _axisLock = Vector3.UnitX;
            else
                _axisLock = Vector3.One;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.Z, false))
        {
            if (ImGui.IsKeyDown(ImGuiKey.ModShift))
                _axisLock = Vector3.One - Vector3.UnitZ;
            else if (_axisLock.X != 0 || _axisLock.Y != 0)
                _axisLock = Vector3.UnitZ;
            else
                _axisLock = Vector3.One;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.Y, false))
        {
            if (ImGui.IsKeyDown(ImGuiKey.ModShift))
                _axisLock = Vector3.One - Vector3.UnitY;
            else if (_axisLock.Z != 0 || _axisLock.X != 0)
                _axisLock = Vector3.UnitY;
            else
                _axisLock = Vector3.One;
        }
    }

    public void GetAxisText(Vector3 STR)
    {
        if (IsTranslationActive)
            ActTransform.FullTransformString = "Moving ";
        else if (IsScaleActive)
            ActTransform.FullTransformString = "Scaling ";
        else if (IsRotationActive)
            ActTransform.FullTransformString = "Rotating ";

        if (window.CurrentScene!.SelectedObjects.Count() > 1)
            ActTransform.FullTransformString += "multiple objects";
        else
        {
            var fs = window.CurrentScene.SelectedObjects.First();
            if (fs is IStageSceneObj) ActTransform.FullTransformString+= (fs as IStageSceneObj)!.StageObj.Name; 
            else if (fs is RailSceneObj) ActTransform.FullTransformString+= (fs as RailSceneObj)!.RailObj.Name;
            else if (fs is RailPointSceneObj) ActTransform.FullTransformString += $"{(fs as RailPointSceneObj)!.ParentRail.RailObj.Name} Point {(fs as RailPointSceneObj)!.ParentRail.RailPoints.IndexOf((fs as RailPointSceneObj)!)}";
            else if (fs is RailHandleSceneObj) ActTransform.FullTransformString += $"{(fs as RailHandleSceneObj)!.ParentPoint.ParentRail.RailObj.Name} Point {(fs as RailHandleSceneObj)!.ParentPoint.ParentRail.RailPoints.IndexOf((fs as RailHandleSceneObj)!.ParentPoint)} Handle";
        }

        if (_axisLock == Vector3.UnitX || _axisLock == Vector3.UnitY || _axisLock == Vector3.UnitZ)
        {
            ActTransform.FullTransformString += " on the ";

            if (_axisLock == Vector3.UnitX)
                ActTransform.FullTransformString += "X ";
            else if (_axisLock == Vector3.UnitY)
                ActTransform.FullTransformString += "Y ";
            else if (_axisLock == Vector3.UnitZ)
                ActTransform.FullTransformString += "Z ";


            ActTransform.FullTransformString += "axis";

            if (_transformChangeString != "-" && _transformChangeString != "" && _axisLock != Vector3.One)
                ActTransform.FullTransformString += ": " + (_transformChangeString != "-" ? _transformChangeString : "");
            else
                ActTransform.FullTransformString += ": ";



            if (_axisLock == Vector3.UnitX)
                ActTransform.FullTransformString += $" {STR.X:0.00}";

            if (_axisLock == Vector3.UnitY)
                ActTransform.FullTransformString += $" {STR.Y:0.00}";

            if (_axisLock == Vector3.UnitZ)
                ActTransform.FullTransformString += $" {STR.Z:0.00}";
        }
        else
        {
            ActTransform.FullTransformString += ": " + $"X: {STR.X:0.00}, Y: {STR.Y:0.00}, Z: {STR.Z:0.00}";
        }
    }

    ImGuiWindowClass windowClass = new() { DockNodeFlagsOverrideSet = ImGuiDockNodeFlags.AutoHideTabBar | ImGuiWidgets.NO_WINDOW_MENU_BUTTON}; // | ImGuiDockNodeFlags.NoUndocking };
    public unsafe void Render(double deltaSeconds)
    {
        if (window.CurrentScene is null)
            return;

        float aspectRatio;

        Vector2 sceneImageRectMin;
        Vector2 sceneImageRectMax;
        Vector2 sceneImageSize;

        bool sceneReady = window.CurrentScene?.IsReady ?? false;

        ImGui.PushStyleColor(ImGuiCol.Button, 0x00000000);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0x00000000);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0x00000000);
        if (sceneReady)
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0));

        unsafe
        {
            fixed (ImGuiWindowClass* tmp = &windowClass)
            ImGui.SetNextWindowClass(new ImGuiWindowClassPtr(tmp));
        }
        if (!ImGui.Begin("Scene", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (sceneReady)
                ImGui.PopStyleVar();
            ImGui.PopStyleColor(3);
            return;
        }
        ImGui.PopStyleColor(3);

        if (!sceneReady)
        {
            ImGui.TextDisabled("The stage is being loaded, please wait...");
            ImGui.End();
            return;
        }

        Vector2 contentAvail = ImGui.GetContentRegionAvail() - new Vector2(0, 24 * window.ScalingFactor);
        aspectRatio = contentAvail.X / contentAvail.Y;
        Vector2 sceneWindowRegionMin = ImGui.GetCursorScreenPos();
        Vector2 sceneWindowRegionMax = ImGui.GetCursorScreenPos() + contentAvail;

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

        _viewportSize = ImGui.GetItemRectSize();

        _isSceneHovered = ImGui.IsItemHovered();
        _isSceneWindowFocused = ImGui.IsWindowFocused();
        sceneImageRectMin = ImGui.GetItemRectMin();
        sceneImageRectMax = ImGui.GetItemRectMax();
        sceneImageSize = contentAvail;
        ImGui.PopStyleVar();

        Camera camera = window.CurrentScene!.Camera;

        #region Input


        _scaleKey = window.ContextHandler.SystemSettings.UseWASD ? ImGuiKey.F : ImGuiKey.S;

        Vector2 mousePos = ImGui.GetMousePos();

        _mouseMoveKey = window.ContextHandler.SystemSettings.UseMiddleMouse ? ImGuiMouseButton.Middle : ImGuiMouseButton.Right;

        if ((_isSceneHovered || _persistentMouseDrag) && ImGui.IsMouseDragging(_mouseMoveKey))
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

        if (!ImGui.IsMouseDown(_mouseMoveKey))
            _persistentMouseDrag = false;

        _previousMousePos = mousePos;

        // Camera Movement
        float camMoveSpeed = (float)(0.4 * deltaSeconds * 60);
        camMoveSpeed *= window.Keyboard!.IsKeyPressed(Key.ShiftRight) || window.Keyboard.IsKeyPressed(Key.ShiftLeft) ? 6 : 1;
        if ((_isSceneHovered || _isSceneWindowFocused) && !ImGui.GetIO().WantTextInput)
        {
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
        }

            // if ((window.Keyboard?.IsKeyP ressed(Key.Space) ?? false) && window.CurrentScene.SelectedObjects.Count() > 0){
            //     camera.LookAt(camera.Eye, window.CurrentScene.SelectedObjects.First().StageObj.Translation*0.01f);
            // }
        if (window.CurrentScene.SelectedObjects.Any() || CamToObj)
        {
            CamToObj = CamToObj ? CamToObj : (window.Keyboard?.IsKeyPressed(Key.Space) ?? false);
            if (!ImGui.GetIO().WantTextInput)
            {
                if (window.Keyboard?.IsKeyPressed(Key.Keypad1) ?? false)
                {
                    camera.LookAt(camera.Eye, camera.Eye + new Vector3(1, 0, 0));
                    CamToObj = true;
                }
                if (window.Keyboard?.IsKeyPressed(Key.Keypad2) ?? false)
                {
                    camera.LookAt(camera.Eye, camera.Eye + new Vector3(0, 1, 0));
                    CamToObj = true;

                }
                if (window.Keyboard?.IsKeyPressed(Key.Keypad3) ?? false)
                {
                    camera.LookAt(camera.Eye, camera.Eye + new Vector3(0, 0, 1));
                    CamToObj = true;

                }
                if (window.Keyboard?.IsKeyPressed(Key.Keypad4) ?? false)
                {
                    camera.LookAt(camera.Eye, camera.Eye + new Vector3(-1, 0, 0));
                    CamToObj = true;

                }
                if (window.Keyboard?.IsKeyPressed(Key.Keypad5) ?? false)
                {
                    camera.LookAt(camera.Eye, camera.Eye + new Vector3(0, -1, 0));
                    CamToObj = true;

                }
                if (window.Keyboard?.IsKeyPressed(Key.Keypad6) ?? false)
                {
                    camera.LookAt(camera.Eye, camera.Eye + new Vector3(0, 0, -1));
                    CamToObj = true;

                }
            }
            if (CamToObj)
            {
                CamSceneObj = CamSceneObj ?? window.CurrentScene.SelectedObjects.First();

                AxisAlignedBoundingBox aabb = CamSceneObj.AABB;

                switch (CamSceneObj)
                {
                    case ISceneObj x when x is IStageSceneObj y:
                        aabb *= y.StageObj.Scale;
                        camera.LookFrom(y.StageObj.Translation * 0.01f, aabb.GetDiagonal() * 0.01f);
                        break;
                    case ISceneObj x when x is RailSceneObj y:
                        camera.LookFrom(y.Center * 0.01f, aabb.GetDiagonal() * 0.02f);
                        break;
                    case ISceneObj x when x is RailPointSceneObj y:
                        camera.LookFrom(y.RailPoint.Point0Trans * 0.01f - new Vector3(0,0.5f,0), aabb.GetDiagonal() * 0.01f);
                        break;
                    case ISceneObj x when x is RailHandleSceneObj y:
                        camera.LookFrom((y.ParentPoint.RailPoint.Point0Trans + y.Offset) * 0.01f - new Vector3(0,0.5f,0), aabb.GetDiagonal() * 0.01f);
                        break;
                }
                CamToObj = false;
                CamSceneObj = null;
            }
        }
        

        if (_isSceneHovered && window.ContextHandler.SystemSettings.ShowHoverInfo == HoverInfoMode.Tooltip)
        {
            // Tooltip
            ISceneObj? hoveringObj = window.CurrentScene.HoveringObject;

            if (hoveringObj is not null)
            {     
                if (hoveringObj is IStageSceneObj)
                    ImGui.SetTooltip(((IStageSceneObj)hoveringObj).StageObj.Name);
                else if (hoveringObj is RailSceneObj)
                    ImGui.SetTooltip(((RailSceneObj)hoveringObj).RailObj.Name);
                else if (hoveringObj is RailPointSceneObj)
                    ImGui.SetTooltip($"{((RailPointSceneObj)hoveringObj).ParentRail.RailObj.Name} Point {((RailPointSceneObj)hoveringObj).ParentRail.RailPoints.IndexOf((RailPointSceneObj)hoveringObj)}");
                else if (hoveringObj is RailHandleSceneObj)
                    ImGui.SetTooltip($"{((RailHandleSceneObj)hoveringObj).ParentPoint.ParentRail.RailObj.Name} Point {((RailHandleSceneObj)hoveringObj).ParentPoint.ParentRail.RailPoints.IndexOf(((RailHandleSceneObj)hoveringObj).ParentPoint)} Handle");
                // ImGui.SetTooltip(hoveringObj.GetType().ToString()+" "+ hoveringObj.PickingId.ToString());
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

        window.CurrentScene?.Render(
            window.GL,
            viewMatrix,
            projectionMatrix,
            window.CurrentScene.Camera.Rotation,
            window.CurrentScene.Camera.Eye
        );

        if (ModelRenderer.VisibleGrid)
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
            upperRightCorner + _innerPadding,
            radius: _cubeSize,
            out Vector3 facingDirection
        );
        bool MoveGizmoHovered = false;
        bool RotGizmoHovered = false;
        bool ScaleGizmoHovered = false;
        HoveredAxis TransformGizmoAxis = HoveredAxis.NONE;

        if (window.CurrentScene!.SelectedObjects.Any() && TransformGizmo > 0)
        {
            // Method one: Gizmo is placed in the middle position of the selected objects
            if (_transformGizmoPosMulti == 0)
            {
                int st = 0;
                trls = Vector3.Zero;
                rots = Vector3.Zero;
                foreach (ISceneObj s in window.CurrentScene!.SelectedObjects)
                {
                    if (s is ActorSceneObj ac)
                    {
                        trls += ac.StageObj.Translation * 0.01f;
                        rots += ac.StageObj.Rotation;
                        //scls += ac.StageObj.Scale;
                    }
                    else if (s is RailSceneObj rc)
                    {
                        trls += (rc.FakeOffset + rc.Center) * 0.01f;;
                        rots += rc.FakeRotation;
                        //scls += ac.StageObj.Scale;
                    }
                    else if (s is RailPointSceneObj rp)
                    {
                        trls += rp.RailPoint.Point0Trans * 0.01f;
                        rots += rp.FakeRotation;
                        //scls += ac.StageObj.Scale;
                    }
                    else if (s is RailHandleSceneObj rh)
                    {
                        trls += (rh.ParentPoint.RailPoint.Point0Trans + rh.Offset) * 0.01f;
                        //scls += ac.StageObj.Scale;
                    }

                    st += 1;
                }
                trls /= st;
                rots /= st;
                // scls /= st;
            }
            // Method two: Gizmo is placed on the first object
            else if (_transformGizmoPosMulti == 1 && window.CurrentScene!.SelectedObjects.First() != null)
            {
                ISceneObj fst = window.CurrentScene!.SelectedObjects.First();
                trls = fst switch 
                {
                    ISceneObj x when x is IStageSceneObj stg => stg.StageObj.Translation * 0.01f,
                    ISceneObj x when x is RailSceneObj r => (r.Center + r.FakeOffset) * 0.01f,
                    ISceneObj x when x is RailPointSceneObj rp => rp.RailPoint.Point0Trans * 0.01f,
                    ISceneObj x when x is RailHandleSceneObj rp => (rp.ParentPoint.RailPoint.Point0Trans + rp.Offset) * 0.01f,
                    _ => Vector3.Zero
                };
                rots = fst switch 
                {
                    ISceneObj x when x is IStageSceneObj stg => stg.StageObj.Rotation,
                    ISceneObj x when x is RailSceneObj r => r.FakeRotation,
                    ISceneObj x when x is RailPointSceneObj rp => rp.FakeRotation,
                    _ => Vector3.Zero
                };
            }
            // Method three: Gizmo is placed on the last object
            else if (_transformGizmoPosMulti == 2 && window.CurrentScene!.SelectedObjects.Last() != null)
            {
                ISceneObj lst = window.CurrentScene!.SelectedObjects.Last();
                trls = lst switch
                {
                    ISceneObj x when x is IStageSceneObj stg => stg.StageObj.Translation * 0.01f,
                    ISceneObj x when x is RailSceneObj r => (r.Center + r.FakeOffset) * 0.01f,
                    ISceneObj x when x is RailPointSceneObj rp => rp.RailPoint.Point0Trans * 0.01f,
                    ISceneObj x when x is RailHandleSceneObj rp => (rp.ParentPoint.RailPoint.Point0Trans + rp.Offset) * 0.01f,
                    _ => Vector3.Zero
                };
                rots = lst switch
                {
                    ISceneObj x when x is IStageSceneObj stg => stg.StageObj.Rotation,
                    ISceneObj x when x is RailSceneObj r => r.FakeRotation,
                    ISceneObj x when x is RailPointSceneObj rp => rp.FakeRotation,
                    _ => Vector3.Zero
                };

            }
            tl = Matrix4x4.CreateTranslation(trls);
            rt = Matrix4x4.CreateRotationX(rots.X / 180 * (float)Math.PI)* Matrix4x4.CreateRotationY(rots.Y / 180 * (float)Math.PI) * Matrix4x4.CreateRotationZ(rots.Z / 180 * (float)Math.PI) ;
            switch (TransformGizmo)
            {
                case 1:
                MoveGizmoHovered = GizmoDrawer.TranslationGizmo(
                    tl,  float.Clamp(800 / Vector3.Distance(eyeAnimated, trls), 50, 180)/* 800 / Vector3.Distance(eyeAnimated, trls)*/, out TransformGizmoAxis);
                break;
                case 2:
                RotGizmoHovered = GizmoDrawer.RotationGizmo(
                    tl, float.Clamp(800 / Vector3.Distance(eyeAnimated, trls), 50, 180), out TransformGizmoAxis);
                break;
                case 3:
                ScaleGizmoHovered = GizmoDrawer.ScaleGizmo(
                    rt * tl, float.Clamp(800 / Vector3.Distance(eyeAnimated, trls), 50, 180), out TransformGizmoAxis);
                break;
            }
        }
        GizmoDrawer.EndGizmoDrawing();


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
            //Debug.Assert(canInvert);
            Vector4 worldMousePos = Vector4.Transform(ndcMousePos3D, inverseViewProjection);
            worldMousePos /= worldMousePos.W;

            if (AddRailPoint != 0)
            {
                if (AddRailPoint == AddRailPointState.Add)
                {
                    RailSceneObj rl = (RailSceneObj)window.CurrentScene.SelectedObjects.FirstOrDefault(x => x is RailSceneObj)!;
                    ChangeHandler.ChangeAddPoint(window, window.CurrentScene.History, rl, 100 * new Vector3(worldMousePos.X, worldMousePos.Y, worldMousePos.Z));
                }
                else if (AddRailPoint == AddRailPointState.Insert)
                {
                    RailPointSceneObj rp = (RailPointSceneObj)window.CurrentScene.SelectedObjects.FirstOrDefault(x => x is RailPointSceneObj)!;
                    ChangeHandler.ChangeInsertPoint(window, window.CurrentScene.History, rp.ParentRail, rp.ParentRail.RailPoints.IndexOf(rp), 100 * new Vector3(worldMousePos.X, worldMousePos.Y, worldMousePos.Z));
                    ChangeHandler.ToggleObjectSelection(window, window.CurrentScene.History, rp.ParentRail.RailPoints[rp.ParentRail.RailPoints.IndexOf(rp) + 1].PickingId, true);
                }
                AddRailPoint = 0;
            }

            // Calculate camera zoom in / out
            if (window.Mouse!.ScrollWheels[0].Y != 0 && _isSceneHovered)
            {
                if (window.ContextHandler.SystemSettings.ZoomToMouse == ImGui.IsKeyDown(ImGuiKey.ModAlt))
                {
                    camera.Eye -= Vector3.Transform(
                        Vector3.UnitZ * window.Mouse.ScrollWheels[0].Y * 6 * camMoveSpeed,
                        camera.Rotation
                    );
                }
                else
                {
                    camera.Eye = camera.Eye + -window.Mouse.ScrollWheels[0].Y * 2 * camMoveSpeed * (camera.Eye - Vector3.Lerp(camera.Eye, new Vector3(worldMousePos.X, worldMousePos.Y, worldMousePos.Z), 0.1f));
                }
            }
            
            if (_isSceneHovered)
                window.CurrentScene.SetHoveringObject(pixel);

            if (
                ImGui.IsMouseClicked(ImGuiMouseButton.Left)
                && _isSceneHovered
                && !IsTranslationActive && !IsRotationActive && !IsScaleActive
                && (_mouseMoveKey == ImGuiMouseButton.Right ? !ImGui.IsKeyDown(ImGuiKey.ModAlt) : true))
            {
                if (!_isSceneWindowFocused)
                    ImGui.SetWindowFocus();

                if (MoveGizmoHovered && !IsTransformFromGizmo && !ImGui.IsKeyDown(ImGuiKey.ModShift))
                {
                    TranslationStarted = true;
                    IsTransformFromGizmo = true;
                }
                else if (RotGizmoHovered && !IsTransformFromGizmo && !ImGui.IsKeyDown(ImGuiKey.ModShift))
                {
                    RotationStarted = true;
                    IsTransformFromGizmo = true;
                }
                else if (ScaleGizmoHovered && !IsTransformFromGizmo && !ImGui.IsKeyDown(ImGuiKey.ModShift))
                {
                    ScaleStarted = true;
                    IsTransformFromGizmo = true;
                }
                else if (orientationCubeHovered)
                {
                    camera.LookAt(camera.Eye, camera.Eye - facingDirection);
                }
                else if (_mouseClickActions.TryDequeue(out var action))
                {
                    action(window, worldMousePos);
                }
                else
                {
                    ChangeHandler.ToggleObjectSelection(
                        window,
                        window.CurrentScene.History,
                        pixel,
                        !(window.Keyboard?.IsShiftPressed() ?? false)
                    );
                }
                if (IsTransformFromGizmo && (RotationStarted || TranslationStarted || ScaleStarted))
                {
                    switch (TransformGizmoAxis)
                    {
                        case HoveredAxis.X_AXIS:
                        _axisLock = Vector3.UnitX;
                        break;
                        case HoveredAxis.Y_AXIS:
                        _axisLock = Vector3.UnitY;
                        break;
                        case HoveredAxis.Z_AXIS:
                        _axisLock = Vector3.UnitZ;
                        break;                    
                        case HoveredAxis.YZ_PLANE:
                        _axisLock = Vector3.One - Vector3.UnitX;
                        break;
                        case HoveredAxis.XZ_PLANE:
                        _axisLock = Vector3.One - Vector3.UnitY;
                        break;
                        case HoveredAxis.XY_PLANE:
                        _axisLock = Vector3.One - Vector3.UnitZ;
                        break;
                    }
                }
            }
            else if ((_mouseMoveKey == ImGuiMouseButton.Right ? (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.IsKeyDown(ImGuiKey.ModAlt)) : ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                && _isSceneHovered
                && !IsTranslationActive && !IsRotationActive && !IsScaleActive)
            {
                if (window.CurrentScene.TryGetPickableObj(pixel, out _pickObject) && _pickObject != null && (_pickObject is IStageSceneObj || _pickObject is RailSceneObj))
                {
                    _objectOptionsPos = windowMousePos;
                    bool select1 = window.CurrentScene.SelectedObjects.Count() == 1;

                    bool selectover1 = window.CurrentScene.SelectedObjects.Count() > 0;

                    _selNotSame = selectover1 ? !window.CurrentScene.SelectedObjects.Contains(_pickObject) : true;

                    StageObjType sType = _pickObject switch
                    {
                        ISceneObj x when x is IStageSceneObj y => y.StageObj.Type,
                        ISceneObj x when x is RailSceneObj y => StageObjType.Rail,
                    };

                    bool isSelectionChildable = sType == StageObjType.Area
                                    || sType == StageObjType.AreaChild
                                    || sType == StageObjType.Child
                                    || sType == StageObjType.Regular;

                    isSelectionChildable &= _pickObject is IStageSceneObj stageSceneObj && stageSceneObj.StageObj.FileType == StageFileType.Map;

                    bool cantChild = window.CurrentScene.SelectedObjects.Any(x => x is not IStageSceneObj y ||
                                    (y.StageObj.Type != StageObjType.Area
                                    && y.StageObj.Type != StageObjType.AreaChild
                                    && y.StageObj.Type != StageObjType.Child
                                    && y.StageObj.Type != StageObjType.Regular)
                                    || y.StageObj.FileType != StageFileType.Map
                                    );

                    _selCantParent = !selectover1 || !_selNotSame || !isSelectionChildable || cantChild;
                    _selCantChild = !select1 || !_selNotSame || !isSelectionChildable || cantChild;
                    _isObjectOptionsEnabled = true;
                    _objectOptionsTime = 0;
                }
                else
                    _isObjectOptionsEnabled = false;
            }
            else if ((_isSceneHovered && window.CurrentScene.SelectedObjects.Any()) || IsTranslationActive || IsScaleActive || IsRotationActive)
            {
                Vector3 _ndcMousePos3D =
                    new(ndcMousePos.X * sceneImageSize.X / 2,
                        ndcMousePos.Y * sceneImageSize.Y / 2,
                        (normPickingDepth * 10 - 1) / 10f);
                _ndcMousePos3D = Vector3.Transform(_ndcMousePos3D, window.CurrentScene.Camera.Rotation);

            if (TranslateToPoint)
            {
                TranslateToPoint = false;
                var sobj = window.CurrentScene.SelectedObjects.First();

                switch (sobj)
                {
                    case ISceneObj x when x is IStageSceneObj y:
                        ChangeHandler.ChangeStageObjTransform(
                            window.CurrentScene.History,
                            y,
                            "Translation",
                            y.StageObj.Translation,
                            100 * new Vector3(worldMousePos.X, worldMousePos.Y, worldMousePos.Z)
                        );
                        break;
                    case ISceneObj x when x is RailPointSceneObj y:
                    ChangeHandler.ChangePointPosition(
                            window.CurrentScene.History,
                            y,
                            y.RailPoint.Point0Trans,
                            100 * new Vector3(worldMousePos.X, worldMousePos.Y, worldMousePos.Z),
                            !ImGui.IsKeyDown(ImGuiKey.ModShift)
                        );
                        break;
                    case ISceneObj x when x is RailHandleSceneObj y:
                        ChangeHandler.ChangeHandleTransform(
                            window.CurrentScene.History,
                            y,
                            y.Offset,
                            -y.ParentPoint.RailPoint.Point0Trans + 100 * new Vector3(worldMousePos.X, worldMousePos.Y, worldMousePos.Z),
                            false
                        );
                        break;
                    case ISceneObj x when x is RailSceneObj y:
                    break;
                }

                if (!_isSceneWindowFocused)
                    ImGui.SetWindowFocus();
            }
            TranslateAction(_ndcMousePos3D);
            RotateAction(ndcMousePos);
            ScaleAction(_ndcMousePos3D);
                
            }
        }
        
        GizmoButtons(upperRightCorner);
        ActionPanel(contentAvail);
        ActionMenu(deltaSeconds);

        ImGui.End();
    }
    private void ActionMenu(double deltaSeconds)
    {

        if (_isObjectOptionsEnabled)
        {
            string name = _pickObject switch
            {
                ISceneObj x when x is IStageSceneObj y => y.StageObj.Name,
                ISceneObj x when x is RailSceneObj y => y.RailObj.Name,
                _ => string.Empty // can't happen
            };

            ImGui.PushStyleColor(ImGuiCol.FrameBg, ImGui.GetColorU32(ImGuiCol.WindowBg));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 2f);
            var mpos = _objectOptionsPos - new Vector2(2.5f, 25);
            ImGui.SetCursorPos(mpos);
            float w = ImGui.CalcTextSize(name).X * 1.3f + 10;
            if (ImGui.BeginListBox("##SelectableListbox", new(w > 140 ? w : 140, 150)))
            {
                ImGuiWidgets.TextHeader(name, 1.3f, 0.95f);

                if (_selCantParent)
                    ImGui.BeginDisabled();

                if (_pickObject is IStageSceneObj pickStSceneObj && ImGui.Selectable("Set parent of selection"))
                {
                    _isObjectOptionsEnabled = false;
                    foreach (ISceneObj sobj in window.CurrentScene!.SelectedObjects)
                    {
                        if (sobj == _pickObject || sobj is not IStageSceneObj stageSceneObj) continue;
                        if (pickStSceneObj.StageObj.Parent != null && pickStSceneObj.StageObj.Parent == stageSceneObj.StageObj) continue;
                        ChangeHandler.ChangeSetChild(window, window.CurrentScene.History, stageSceneObj.StageObj, pickStSceneObj.StageObj);
                    }
                }
                if (_selCantParent)
                    ImGui.EndDisabled();
                ImGui.Separator();

                if (_selCantChild)
                    ImGui.BeginDisabled();
                if (_pickObject is IStageSceneObj pickStSceneObj1 && ImGui.Selectable("Add as child of selection"))
                {
                    _isObjectOptionsEnabled = false;
                    var sobj = (IStageSceneObj)window.CurrentScene!.SelectedObjects.First(x => x is IStageSceneObj);
                    if (sobj.StageObj.Parent != pickStSceneObj1.StageObj)
                    {
                        ChangeHandler.ChangeSetChild(window, window.CurrentScene.History, pickStSceneObj1.StageObj, sobj.StageObj);
                    }
                }
                if (_selCantChild)
                    ImGui.EndDisabled();
                ImGui.Separator();

                if (ImGui.Selectable("Duplicate"))
                {
                    _isObjectOptionsEnabled = false;
                    if (_selNotSame)
                        ChangeHandler.ToggleObjectSelection(
                            window,
                            window.CurrentScene!.History,
                            _pickObject.PickingId,
                            true
                        );
                    window.ContextHandler.ActionHandler.ExecuteAction(CommandID.DuplicateObj, window);
                }
                ImGui.Separator();
                if (ImGui.Selectable("Delete"))
                {
                    _isObjectOptionsEnabled = false;
                    if (_selNotSame)
                        ChangeHandler.ToggleObjectSelection(
                            window,
                            window.CurrentScene!.History,
                            _pickObject.PickingId,
                            true
                        );
                    window.ContextHandler.ActionHandler.ExecuteAction(CommandID.RemoveObj, window);

                }
                ImGui.EndListBox();
                ImGui.SetWindowFontScale(1f);
            }
            //ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetWindowPos() + mpos, ImGui.GetWindowPos() + _objectOptionsPos + new Vector2(w, 150), 0x7f7f7f7f);
            if (!ImGui.IsMouseHoveringRect(ImGui.GetWindowPos() + mpos, ImGui.GetWindowPos() + _objectOptionsPos + new Vector2(w, 150)))
            {
                //Console.WriteLine("Hovering");
                if (_objectOptionsTime != 0 && (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(_mouseMoveKey)))
                {
                    _objectOptionsTime = 100;
                }
                _objectOptionsTime += deltaSeconds;
            }
            else
                _objectOptionsTime = 0;
            if (_objectOptionsTime >= 1.15)
            {
                _isObjectOptionsEnabled = false;

            }
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }
    }
    private void ActionPanel(Vector2 contentAvail)
    {
        var opos = ImGui.GetCursorPos();
        // Needed for the gizmos not to overlap with this panel
        ImGui.GetWindowDrawList().AddRectFilled(
            ImGui.GetCursorScreenPos() - new Vector2(0, 4), 
            ImGui.GetCursorScreenPos() + new Vector2(ImGui.GetWindowWidth(), 28),
            ImGui.GetColorU32(ImGuiCol.WindowBg) | 0xFF000000);

        if (IsTranslationActive || IsScaleActive || IsRotationActive)
        {
            ImGui.SetWindowFontScale(1.0f);

            ImGui.SetCursorPos(opos + new Vector2(8, -2));
            ImGui.Text(ActTransform.FullTransformString);
            ImGui.SetWindowFontScale(1.0f);
            ImGui.SetCursorPos(opos);
        }

        //ImGui.PushFont(window.FontPointers[1]);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, default));
        float buttons = ImGui.CalcTextSize(IconUtils.GRID).X*6 + 6*11 + 12;
        ImGui.SetCursorPos(new Vector2(contentAvail.X - buttons, opos.Y - 3f));

        if (ImGui.Button(IconUtils.GRID))
        {
            ModelRenderer.VisibleGrid = !ModelRenderer.VisibleGrid;
        }
        ImGui.SetItemTooltip($"Grid visibility {(ModelRenderer.VisibleGrid ? "ON" : "OFF")}");
        ImGui.SameLine();

        if (ImGui.Button(IconUtils.TRANSPARENT))
        {
            ModelRenderer.VisibleTransparentWall = !ModelRenderer.VisibleTransparentWall;
        }
        ImGui.SetItemTooltip($"Transparent walls visibility {(ModelRenderer.VisibleTransparentWall ? "ON" : "OFF")}");
        ImGui.SameLine();
        
        if (ImGui.Button(IconUtils.PATH))
        {
            ModelRenderer.VisibleRails = !ModelRenderer.VisibleRails;
        }
        ImGui.SetItemTooltip($"Rails visibility {(ModelRenderer.VisibleRails ? "ON" : "OFF")}");
        ImGui.SameLine();

        if (ImGui.Button(IconUtils.AREA))
        {
            ModelRenderer.VisibleAreas = !ModelRenderer.VisibleAreas;
        }
        ImGui.SetItemTooltip($"Area visibility {(ModelRenderer.VisibleAreas ? "ON" : "OFF")}");
        ImGui.SameLine();
        if (ImGui.Button(IconUtils.CAMERA))
        {
            ModelRenderer.VisibleCameraAreas = !ModelRenderer.VisibleCameraAreas;
        }
        ImGui.SetItemTooltip($"CameraArea visibility {(ModelRenderer.VisibleCameraAreas ? "ON" : "OFF")}");
        ImGui.SameLine();


        ImGui.PopStyleVar(2);
        //ImGui.PopFont();
        ImGui.SetCursorPos(opos);
    }
    private void GizmoButtons(Vector2 upperRightCorner)
    {
        var olpos = ImGui.GetCursorPos();
        ImGui.SetCursorScreenPos(upperRightCorner + new Vector2(_innerPadding.X, _innerPadding.Y + _cubeSize));
        if (ImGui.BeginChild("OverlayGizmos", new (34 , 120)))
        {
            var col = ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.Button));
            col.W = 0.7f;
            ImGui.PushStyleColor(ImGuiCol.Button, col);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 30);
            if (ImGui.Button(IconUtils.MOVE+"##movegizmo", new Vector2(30)) || (!ImGui.GetIO().WantTextInput && ImGui.IsKeyPressed(ImGuiKey._1, false)))
                TransformGizmo = TransformGizmo == 1 ? 0 : 1;
            ImGui.SetItemTooltip($"Move Gizmo {(TransformGizmo == 1 ? "ON" : "OFF")}");
            if (ImGui.Button(IconUtils.ROTATE+"##rotategizmo", new Vector2(30)) || (!ImGui.GetIO().WantTextInput && ImGui.IsKeyPressed(ImGuiKey._2, false)))
                TransformGizmo = TransformGizmo == 2 ? 0 : 2;
            ImGui.SetItemTooltip($"Rotate Gizmo {(TransformGizmo == 2 ? "ON" : "OFF")}");
            if (ImGui.Button(IconUtils.SCALE+"##sclgizmo", new Vector2(30)) || (!ImGui.GetIO().WantTextInput && ImGui.IsKeyPressed(ImGuiKey._3, false)))
                TransformGizmo = TransformGizmo == 3 ? 0 : 3;
            ImGui.SetItemTooltip($"Scale Gizmo {(TransformGizmo == 3 ? "ON" : "OFF")}");
            ImGui.EndChild();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }
        ImGui.SetCursorPos(olpos);
    }
    private void TabsPanel()
    {
    }

    public void TranslateAction(Vector3 _ndcMousePos3D)
    {
        // if (_isRotationActive || _isScaleActive)
        //     return;

        float dist = 0;
        if (IsTranslationActive)
        {
            if (!_isSceneWindowFocused)
                ImGui.SetWindowFocus();

            //multiply by distance to camera to make it responsive at different distances

            if (!IsTransformFromGizmo)
                GetAxis();
            if (_axisLock != Vector3.One)
            {
                TransformChange();
            }
            else
                _transformChangeString = "";

            dist = Vector3.Distance(
                ActTransform.Originals[window.CurrentScene!.SelectedObjects.First()] / 100,
                window.CurrentScene.Camera.Eye
            );

            _ndcMousePos3D *= dist / 11;

            foreach (ISceneObj scobj in window.CurrentScene.SelectedObjects)
            {
                Vector3 defPos = ActTransform.Originals[scobj] - (ActTransform.Relative[scobj] + _ndcMousePos3D); // default position

                if (_transformChangeString != string.Empty && _transformChangeString != "-")
                {
                    defPos = -Vector3.One * float.Parse(_transformChangeString);
                }

                Vector3 nTr = ActTransform.Originals[scobj] - defPos * _axisLock;

                switch (scobj)
                {
                    case ISceneObj x when x is IStageSceneObj y:
                        y.StageObj.Translation = nTr;
                        break;
                    case ISceneObj x when x is RailHandleSceneObj y:
                        y.Offset = nTr - y.ParentPoint.RailPoint.Point0Trans * (Vector3.One - _axisLock);
                        break;
                    case ISceneObj x when x is RailPointSceneObj y:
                        y.RailPoint.Point0Trans = nTr;
                        break;
                    case ISceneObj x when x is RailSceneObj y:
                        y.FakeOffset = nTr * ( _axisLock);// - y.Center ;
                        break;
                }

                if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
                {
                    switch (scobj)
                    {
                        case ISceneObj x when x is IStageSceneObj y:
                            y.StageObj.Translation = MathUtils.Round(y.StageObj.Translation / 50) * 50;
                            break;
                        case ISceneObj x when x is RailHandleSceneObj y:
                            y.Offset = MathUtils.Round(y.Offset / 50) * 50;
                            break;
                        case ISceneObj x when x is RailPointSceneObj y:
                            y.RailPoint.Point0Trans  = MathUtils.Round(y.RailPoint.Point0Trans / 50) * 50;
                            break;
                        case ISceneObj x when x is RailSceneObj y:
                            y.FakeOffset = MathUtils.Round(y.FakeOffset / 50) * 50;
                            break;
                    }
                }
                if (scobj is IStageSceneObj) scobj.UpdateTransform();
                else if (scobj is RailPointSceneObj) (scobj as RailPointSceneObj)!.UpdateModelMoving();
                else if (scobj is RailHandleSceneObj) (scobj as RailHandleSceneObj)!.UpdateTransform();
                else if (scobj is RailSceneObj) (scobj as RailSceneObj)!.UpdateDuringMove();

            }

            Vector3 STR = Vector3.Zero;
            switch (window.CurrentScene.SelectedObjects.First())
            {
                case ISceneObj x when x is IStageSceneObj y:
                    STR = y.StageObj.Translation;
                    break;
                case ISceneObj x when x is RailHandleSceneObj y:
                    STR = y.Offset;
                    break;
                case ISceneObj x when x is RailPointSceneObj y:
                    STR = y.RailPoint.Point0Trans;
                    break;
                case ISceneObj x when x is RailSceneObj y:
                    STR = y.FakeOffset;
                    break;
            }

            GetAxisText(STR);
        }

        if ((IsTranslationFromDuplicate || TranslationStarted) && !IsTranslationActive)
        { // Start action
            IsTranslationFromDuplicate = false;
            TranslationStarted = false;
            IsTranslationActive = true;

            // Only get distance to first object to prevent misalignments
            var fst = window.CurrentScene!.SelectedObjects.First();
            switch (fst)
            {
                case ISceneObj x when x is IStageSceneObj y:
                    dist = Vector3.Distance(
                    y.StageObj.Translation / 100,
                    window.CurrentScene.Camera.Eye
                    );
                    break;
                case ISceneObj x when x is RailPointSceneObj y:
                    dist = Vector3.Distance(
                    y.RailPoint.Point0Trans / 100,
                    window.CurrentScene.Camera.Eye
                    );
                    break;
                case ISceneObj x when x is RailHandleSceneObj y:
                    dist = Vector3.Distance(
                    (y.Offset + y.ParentPoint.RailPoint.Point0Trans) / 100,
                    window.CurrentScene.Camera.Eye
                    );
                break;
                case ISceneObj x when x is RailSceneObj y:
                    dist = Vector3.Distance(
                    (y.FakeOffset + y.Center) / 100,
                    window.CurrentScene.Camera.Eye
                    );
                break;
            }
            

            _ndcMousePos3D *= dist / 11;

            bool hasRails = window.CurrentScene.SelectedObjects.Any(x => x is RailSceneObj);
            bool hasPoints = window.CurrentScene.SelectedObjects.Any(x => x is RailPointSceneObj);
            List<ISceneObj> remove = new();
            foreach (ISceneObj scobj in window.CurrentScene.SelectedObjects)
            {   
                switch (scobj)
                {
                    case ISceneObj x when x is IStageSceneObj y:
                        ActTransform.Originals.Add(y, y.StageObj.Translation);
                        ActTransform.Relative[y] = y.StageObj.Translation - _ndcMousePos3D;
                        break;
                    case ISceneObj x when x is RailHandleSceneObj y:
                        if (hasRails || hasPoints) remove.Add(x);
                        else
                        {
                            ActTransform.Originals.Add(y, y.Offset +y.ParentPoint.RailPoint.Point0Trans);
                            ActTransform.Relative[y] = y.Offset - _ndcMousePos3D;
                        }
                        break;
                    case ISceneObj x when x is RailPointSceneObj y:
                        if (hasRails) remove.Add(x);
                        else
                        {
                            ActTransform.Originals.Add(y, y.RailPoint.Point0Trans);
                            ActTransform.Relative[y] = y.RailPoint.Point0Trans - _ndcMousePos3D;
                        }
                        break;
                    case ISceneObj x when x is RailSceneObj y:
                        ActTransform.Originals.Add(y, y.FakeOffset + y.Center);
                        ActTransform.Relative[y] = y.FakeOffset - _ndcMousePos3D;
                        break;
                }
            }
            window.CurrentScene.UnselectMultiple(remove);
        }
        else if (
            (
                FinishTransform
                || (IsTransformFromGizmo && ImGui.IsKeyReleased(ImGuiKey.MouseLeft))
                || ImGui.IsKeyPressed(ImGuiKey.MouseLeft, false)
                || ImGui.IsKeyPressed(ImGuiKey.Enter, false)
            ) && IsTranslationActive
        )
        { // Apply action
            IsTransformFromGizmo = false;
            IsTranslationActive = false;
            FinishTransform = false;
            _axisLock = Vector3.One;

            // Add to Undo stack
            if (window.CurrentScene!.SelectedObjects.Count() == 1)
            {
                var sobj = window.CurrentScene.SelectedObjects.First();
                switch (sobj)
                {
                    case ISceneObj x when x is IStageSceneObj y:
                        ChangeHandler.ChangeStageObjTransform(
                            window.CurrentScene.History,
                            y,
                            "Translation",
                            ActTransform.Originals[y],
                            y.StageObj.Translation
                        );
                        break;
                    case ISceneObj x when x is RailPointSceneObj y:
                    ChangeHandler.ChangePointPosition(
                            window.CurrentScene.History,
                            y,
                            ActTransform.Originals[y],
                            y.RailPoint.Point0Trans,
                            true
                        );
                        break;
                    case ISceneObj x when x is RailHandleSceneObj y:
                        ChangeHandler.ChangeHandleTransform(
                            window.CurrentScene.History,
                            y,
                            ActTransform.Originals[y],
                            y.Offset
                        );
                        break;
                    case ISceneObj x when x is RailSceneObj y:
                        ChangeHandler.ChangeRailPosition(
                            window.CurrentScene.History,
                            y,
                            ActTransform.Originals[y],
                            y.FakeOffset
                        );
                    break;
                }
                
            }
            else
            {
                bool hasRails = window.CurrentScene.SelectedObjects.Any(x => x is RailSceneObj);

                foreach (ISceneObj scobj in window.CurrentScene.SelectedObjects)
                {   
                    switch (scobj)
                    {
                        case ISceneObj x when x is IStageSceneObj y:
                            ActTransform.Finals.Add(y, y.StageObj.Translation);
                            break;
                        case ISceneObj x when x is RailHandleSceneObj y:
                            if (hasRails) 
                            {
                                y.Offset = ActTransform.Originals[scobj] - y.ParentPoint.RailPoint.Point0Trans;
                                y.UpdateTransform();
                            }
                            else 
                            {
                                ActTransform.Finals.Add(y, y.Offset +y.ParentPoint.RailPoint.Point0Trans);
                            }
                            break;
                        case ISceneObj x when x is RailPointSceneObj y:
                            if (hasRails) 
                            {
                                y.RailPoint.Point0Trans = ActTransform.Originals[scobj];
                                y.UpdateModel();
                            }
                            else 
                            {
                                ActTransform.Finals.Add(y, y.RailPoint.Point0Trans);
                            }
                            break;
                        case ISceneObj x when x is RailSceneObj y:
                            ActTransform.Finals.Add(y, y.FakeOffset);
                            break;
                    }
                }
                ChangeHandler.ChangeMultiMove(window.CurrentScene.History, ActTransform.Originals, ActTransform.Finals);
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
            ActTransform.Finals = new();
            _transformChangeString = "";
            window.CurrentScene.IsSaved = false;
        }
        else if (
            (ImGui.IsMouseClicked(ImGuiMouseButton.Right) 
            || ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            && IsTranslationActive
        )
        { // Cancel action
            IsTransformFromGizmo = false;
            IsTranslationActive = false;

            foreach (ISceneObj scobj in window.CurrentScene!.SelectedObjects)
            {
                switch (scobj) // Reset to what it was
                {
                    case ISceneObj x when x is IStageSceneObj y:
                        y.StageObj.Translation = ActTransform.Originals[scobj]; // Reset to what it was
                        y.UpdateTransform();
                        break;
                    case ISceneObj x when x is RailHandleSceneObj y:
                        y.Offset = ActTransform.Originals[scobj] - y.ParentPoint.RailPoint.Point0Trans;
                        y.UpdateTransform();
                        break;
                    case ISceneObj x when x is RailPointSceneObj y:
                        y.RailPoint.Point0Trans = ActTransform.Originals[scobj];
                        y.UpdateModel();
                        break;
                    case ISceneObj x when x is RailSceneObj y:
                        y.FakeOffset = ActTransform.Originals[scobj] - y.Center;
                        y.UpdateAfterMove();
                        break;
                }

            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
            ActTransform.Finals = new();
            _axisLock = Vector3.One;
            _transformChangeString = "";
        }
    }

    public void RotateAction(Vector2 ndcMousePos)
    {
        //if (_isScaleActive || _isTranslationActive)
        //    return;

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
        if (IsRotationActive)
        {
            if (!_isSceneWindowFocused)
                ImGui.SetWindowFocus();

            if (!IsTransformFromGizmo)
                GetAxis();
            if (_axisLock != Vector3.One)
                TransformChange();
            else
                _transformChangeString = "";

            if (_axisLock == Vector3.One)
                _axisLock = Vector3.UnitY;

            foreach (ISceneObj sobj in window.CurrentScene!.SelectedObjects)
            {
                if (sobj is IStageSceneObj)
                {                
                    if (_transformChangeString != string.Empty && _transformChangeString != "-")
                    {
                        (sobj as IStageSceneObj)!.StageObj.Rotation =
                            ActTransform.Originals[sobj] + _axisLock * float.Parse(_transformChangeString);
                    }
                    else
                    {
                        (sobj as IStageSceneObj)!.StageObj.Rotation =
                            ActTransform.Originals[sobj] + _axisLock * (-ActTransform.Relative[sobj].X + (float)rot);
                    }

                    if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
                    {
                        (sobj as IStageSceneObj)!.StageObj.Rotation = MathUtils.Round((sobj as IStageSceneObj)!.StageObj.Rotation / 5) * 5;
                    }
                    sobj.UpdateTransform();
                }
                else if (sobj is RailPointSceneObj)
                {
                    if (_transformChangeString != string.Empty && _transformChangeString != "-")
                    {
                        (sobj as RailPointSceneObj)!.FakeRotation =
                            ActTransform.Originals[sobj] + _axisLock * float.Parse(_transformChangeString);
                    }
                    else
                    {
                        (sobj as RailPointSceneObj)!.FakeRotation =
                            ActTransform.Originals[sobj] + _axisLock * (-ActTransform.Relative[sobj].X + (float)rot);
                    }

                    if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
                    {
                        (sobj as RailPointSceneObj)!.FakeRotation = MathUtils.Round((sobj as RailPointSceneObj)!.FakeRotation / 5) * 5;
                    }
                    (sobj as RailPointSceneObj)!.UpdateModelRotating();
                }
                else if (sobj is RailSceneObj)
                {
                    if (_transformChangeString != string.Empty && _transformChangeString != "-")
                    {
                        (sobj as RailSceneObj)!.FakeRotation =
                            ActTransform.Originals[sobj] + _axisLock * float.Parse(_transformChangeString);
                    }
                    else
                    {
                        (sobj as RailSceneObj)!.FakeRotation =
                            ActTransform.Originals[sobj] + _axisLock * (-ActTransform.Relative[sobj].X + (float)rot);
                    }

                    if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
                    {
                        (sobj as RailSceneObj)!.FakeRotation = MathUtils.Round((sobj as RailSceneObj)!.FakeRotation / 5) * 5;
                    }
                    (sobj as RailSceneObj)!.UpdateDuringRotate();
                }
            }

            var fst = window.CurrentScene.SelectedObjects.First();
        
            Vector3 STR = fst switch
            {
                ISceneObj x when x is IStageSceneObj y => y.StageObj.Rotation,
                ISceneObj x when x is RailPointSceneObj y => y.FakeRotation,
                ISceneObj x when x is RailSceneObj y => y.FakeRotation,
            };

            GetAxisText(STR);
        }

        if (RotationStarted && !IsRotationActive)
        { // Start action
            IsRotationActive = true;
            RotationStarted = false;

            bool hasRails = window.CurrentScene.SelectedObjects.Any(x => x is RailSceneObj);
            bool hasPoints = window.CurrentScene.SelectedObjects.Any(x => x is RailPointSceneObj);
            List<ISceneObj> remove = new();
            foreach (ISceneObj scobj in window.CurrentScene.SelectedObjects)
            {   
                switch (scobj)
                {
                    case ISceneObj x when x is IStageSceneObj y:
                        ActTransform.Originals.Add(y, (y as IStageSceneObj)!.StageObj.Rotation);
                        ActTransform.Relative.Add(y, Vector3.UnitX * (float)rot);
                        break;
                    case ISceneObj x when x is RailHandleSceneObj y:
                        if (hasRails || hasPoints) remove.Add(x);
                        break;
                    case ISceneObj x when x is RailPointSceneObj y:
                        if (hasRails) remove.Add(x);
                        else
                        {
                            ActTransform.Originals.Add(y, Vector3.Zero);
                            ActTransform.Relative.Add(y, Vector3.UnitX * (float)rot);
                        }
                        break;
                    case ISceneObj x when x is RailSceneObj y:
                        ActTransform.Originals.Add(y, Vector3.Zero);
                        ActTransform.Relative.Add(y, Vector3.UnitX * (float)rot);
                        break;
                }
            }
            window.CurrentScene.UnselectMultiple(remove);
            // foreach (ISceneObj sobj in window.CurrentScene!.SelectedObjects)
            // {
            //     if (sobj is IStageSceneObj) ActTransform.Originals.Add(sobj, (sobj as IStageSceneObj)!.StageObj.Rotation);
            //     else if (sobj is RailPointSceneObj) ActTransform.Originals.Add(sobj, Vector3.Zero);
            //     else if (sobj is RailSceneObj) ActTransform.Originals.Add(sobj, Vector3.Zero);
            //     else { IsRotationActive = false; return;}
            //     ActTransform.Relative.Add(sobj, Vector3.UnitX * (float)rot);
            // }
            if (ActTransform.Originals.Count < 1)
            {
                IsRotationActive = false;
                IsTransformFromGizmo = false;
            }
        }
        else if (
            (
                FinishTransform
                || (IsTransformFromGizmo && ImGui.IsKeyReleased(ImGuiKey.MouseLeft))
                || ImGui.IsKeyPressed(ImGuiKey.MouseLeft, false)
                || ImGui.IsKeyPressed(ImGuiKey.Enter, false)
            ) && IsRotationActive
        )
        { // Apply action
            IsTransformFromGizmo = false;
            IsRotationActive = false;
            FinishTransform = false;
            _axisLock = Vector3.One;

            if (window.CurrentScene!.SelectedObjects.Count() == 1)
            {
                var sobj = window.CurrentScene.SelectedObjects.First();
                switch (sobj)
                {
                    case ISceneObj x when x is IStageSceneObj y:
                        ChangeHandler.ChangeStageObjTransform(
                            window.CurrentScene.History,
                            y,
                            "Rotation",
                            ActTransform.Originals[y],
                            y.StageObj.Rotation
                        );
                        break;
                    case ISceneObj x when x is RailPointSceneObj y:
                    ChangeHandler.ChangePointRot(
                            window.CurrentScene.History,
                            y,
                            y.FakeRotation
                        );
                        break;
                    case ISceneObj x when x is RailSceneObj y:
                    ChangeHandler.ChangeRailRot(
                            window.CurrentScene.History,
                            y,
                            y.FakeRotation
                        );
                        break;
                }
            }
            else
            {
                bool hasRails = window.CurrentScene.SelectedObjects.Any(x => x is RailSceneObj);
                foreach (ISceneObj scobj in window.CurrentScene.SelectedObjects)
                {   
                    // switch (scobj)
                    // {
                    //     case ISceneObj x when x is IStageSceneObj y:
                    //         ActTransform.Finals.Add(y, y.StageObj.Rotation);
                    //         break;
                    //     case ISceneObj x when x is RailHandleSceneObj y:
                    //         //ActTransform.Finals.Add(y, y.Offset +y.ParentPoint.RailPoint.Point0Trans);
                    //         break;
                    //     case ISceneObj x when x is RailPointSceneObj y:
                    //         ActTransform.Finals.Add(y, y.FakeRotation);
                    //         break;
                    //     case ISceneObj x when x is RailSceneObj y:
                    //         //ActTransform.Finals.Add(y, y.FakeOffset);
                    //         break;
                    // }
                    switch (scobj)
                    {
                        case ISceneObj x when x is IStageSceneObj y:
                            ActTransform.Finals.Add(y, y.StageObj.Rotation);
                            break;
                        case ISceneObj x when x is RailHandleSceneObj y:
                            // 
                            break;
                        case ISceneObj x when x is RailPointSceneObj y:
                            if (hasRails) 
                            {
                                y.FakeRotation = ActTransform.Originals[scobj];
                                y.UpdateModel();
                            }
                            else 
                            {
                                ActTransform.Finals.Add(y, y.FakeRotation);
                            }
                            break;
                        case ISceneObj x when x is RailSceneObj y:
                            ActTransform.Finals.Add(y, y.FakeRotation);
                            break;
                    }

                }
                ChangeHandler.ChangeMultiRotate(window.CurrentScene.History, ActTransform.Originals, ActTransform.Finals);
                //ChangeMultiRotate
                //ChangeHandler.ChangeMultiTransform(window.CurrentScene.History, ActTransform.Originals, "Rotation");
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
            ActTransform.Finals = new();
            _transformChangeString = "";
            window.CurrentScene.IsSaved = false;
            // Add to Undo stack
        }
        else if (
            (ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            && IsRotationActive
        )
        { // Cancel action
            IsTransformFromGizmo = false;
            IsRotationActive = false;

            foreach (ISceneObj sobj in window.CurrentScene!.SelectedObjects)
            {
                if (sobj is IStageSceneObj) {(sobj as IStageSceneObj)!.StageObj.Rotation = ActTransform.Originals[sobj]; sobj.UpdateTransform(); }
                else if (sobj is RailPointSceneObj) {(sobj as RailPointSceneObj)!.FakeRotation = Vector3.Zero; (sobj as RailPointSceneObj)!.UpdateModel(); }
                else if (sobj is RailSceneObj) {(sobj as RailSceneObj)!.FakeRotation = Vector3.Zero; (sobj as RailSceneObj)!.UpdateAfterRotate(); }
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
            ActTransform.Finals = new();
            _axisLock = Vector3.One;
        }
    }

    public void ScaleAction(Vector3 _ndcMousePos3D)
    { // Get distance to object and if it decreases we scale down, otherwise we increase, by default it scales in all axis, can scale on individual axis
        // if (_isRotationActive || _isTranslationActive)
        //     return;

        float dist = 0;
        if (IsScaleActive)
        {
            if (!_isSceneWindowFocused)
                ImGui.SetWindowFocus();
            //multiply by distance to camera

            if (!IsTransformFromGizmo)
                GetAxis();

            if (_axisLock != Vector3.One)
            {
                TransformChange();
            }
            else
                _transformChangeString = "";

            var fst = window.CurrentScene!.SelectedObjects.First();
            switch (fst)
            {
                case ISceneObj x when x is IStageSceneObj y:
                    dist = Vector3.Distance(
                    y.StageObj.Translation / 100,
                    window.CurrentScene.Camera.Eye
                    );
                    break;
                case ISceneObj x when x is RailPointSceneObj y:
                    dist = Vector3.Distance(
                    y.RailPoint.Point0Trans / 100,
                    window.CurrentScene.Camera.Eye
                    );
                    break;
                case ISceneObj x when x is RailHandleSceneObj y:
                    dist = Vector3.Distance(
                    (y.Offset + y.ParentPoint.RailPoint.Point0Trans) / 100,
                    window.CurrentScene.Camera.Eye
                    );
                break;
                case ISceneObj x when x is RailSceneObj y:
                    dist = Vector3.Distance(
                    (y.FakeOffset + y.Center) / 100,
                    window.CurrentScene.Camera.Eye
                    );
                break;
            }
            _ndcMousePos3D *= dist / 2;

            foreach (ISceneObj sobj in window.CurrentScene!.SelectedObjects)
            {
                if (sobj is IStageSceneObj)
                {                

                    (sobj as IStageSceneObj)!.StageObj.Scale = ActTransform.Originals[sobj];
                    float distA = Vector3.Distance(window.CurrentScene.Camera.Eye, ActTransform.Relative[sobj]);
                    float distB = Vector3.Distance(window.CurrentScene.Camera.Eye, _ndcMousePos3D);

                    if (_transformChangeString != string.Empty && _transformChangeString != "-")
                    {
                        (sobj as IStageSceneObj)!.StageObj.Scale =
                            ActTransform.Originals[sobj]
                            + Vector3.One * (distB - distA) / 500 * _axisLock * float.Parse(_transformChangeString); // original scale * (distance to selection from mouse )
                    }
                    else
                    {
                        (sobj as IStageSceneObj)!.StageObj.Scale = ActTransform.Originals[sobj] + Vector3.One * (distB - distA) / 500 * _axisLock; // original scale * (distance to selection from mouse )
                    }

                    if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
                    {
                        (sobj as IStageSceneObj)!.StageObj.Scale = MathUtils.Round((sobj as IStageSceneObj)!.StageObj.Scale * 10) / 10;
                    }

                    (sobj as IStageSceneObj)!.UpdateTransform();
                }
                else if (sobj is RailSceneObj)
                {
                    (sobj as RailSceneObj)!.FakeScale = ActTransform.Originals[sobj];
                    float distA = Vector3.Distance(window.CurrentScene.Camera.Eye, ActTransform.Relative[sobj]);
                    float distB = Vector3.Distance(window.CurrentScene.Camera.Eye, _ndcMousePos3D);

                    if (_transformChangeString != string.Empty && _transformChangeString != "-")
                    {
                        (sobj as RailSceneObj)!.FakeScale =
                            ActTransform.Originals[sobj]
                            + Vector3.One * (distB - distA) / 500 * _axisLock * float.Parse(_transformChangeString); // original scale * (distance to selection from mouse )
                    }
                    else
                    {
                        (sobj as RailSceneObj)!.FakeScale = ActTransform.Originals[sobj] + Vector3.One * (distB - distA) / 500 * _axisLock; // original scale * (distance to selection from mouse )
                    }

                    if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
                    {
                        (sobj as RailSceneObj)!.FakeScale = MathUtils.Round((sobj as RailSceneObj)!.FakeScale * 10) / 10;
                    }

                    (sobj as RailSceneObj)!.UpdateDuringScale();
                }
            }

            Vector3 STR = fst switch
            {
                ISceneObj x when x is IStageSceneObj y => y.StageObj.Scale,
                ISceneObj x when x is RailSceneObj y => y.FakeScale,
            };

            GetAxisText(STR);
        }

        if (ScaleStarted && !IsScaleActive)// && !ImGui.IsKeyDown(ImGuiKey.ModCtrl))
        { // Start action
            IsScaleActive = true;
            ScaleStarted = false;

            // Only get distance to first object to prevent misalignments
            var fst = window.CurrentScene!.SelectedObjects.First();
            switch (fst)
            {
                case ISceneObj x when x is IStageSceneObj y:
                    dist = Vector3.Distance(
                    y.StageObj.Translation / 100,
                    window.CurrentScene.Camera.Eye
                    );
                    break;
                case ISceneObj x when x is RailPointSceneObj y:
                    dist = Vector3.Distance(
                    y.RailPoint.Point0Trans / 100,
                    window.CurrentScene.Camera.Eye
                    );
                    break;
                case ISceneObj x when x is RailHandleSceneObj y:
                    dist = Vector3.Distance(
                    (y.Offset + y.ParentPoint.RailPoint.Point0Trans) / 100,
                    window.CurrentScene.Camera.Eye
                    );
                break;
                case ISceneObj x when x is RailSceneObj y:
                    dist = Vector3.Distance(
                    (y.FakeOffset + y.Center) / 100,
                    window.CurrentScene.Camera.Eye
                    );
                break;
            }

            _ndcMousePos3D *= dist / 2;


            bool hasRails = window.CurrentScene.SelectedObjects.Any(x => x is RailSceneObj);
            bool hasPoints = window.CurrentScene.SelectedObjects.Any(x => x is RailPointSceneObj);
            List<ISceneObj> remove = new();
            foreach (ISceneObj scobj in window.CurrentScene.SelectedObjects)
            {   
                switch (scobj)
                {
                    case ISceneObj x when x is IStageSceneObj y:

                        ActTransform.Originals.Add(y, y.StageObj.Scale);
                        ActTransform.Relative.Add(y, _ndcMousePos3D);
                        break;
                    case ISceneObj x when x is RailHandleSceneObj y:
                        remove.Add(x);
                        // else
                        // {
                        //     ActTransform.Originals.Add(y, y.Offset +y.ParentPoint.RailPoint.Point0Trans);
                        //     ActTransform.Relative[y] = y.Offset - _ndcMousePos3D;
                        // }
                        break;
                    case ISceneObj x when x is RailPointSceneObj y:
                        remove.Add(x);
                        // else
                        // {
                        //     ActTransform.Originals.Add(y, y.RailPoint.Point0Trans);
                        //     ActTransform.Relative[y] = y.RailPoint.Point0Trans - _ndcMousePos3D;
                        // }
                        break;
                    case ISceneObj x when x is RailSceneObj y:
                        ActTransform.Originals.Add(y, y.FakeScale);
                        ActTransform.Relative.Add(y, _ndcMousePos3D);
                        break;
                }
            }
            window.CurrentScene.UnselectMultiple(remove);
            if (ActTransform.Originals.Count < 1)
            {
                IsScaleActive = false;
                IsTransformFromGizmo = false;
            }
        }
        else if (
            (
                FinishTransform
                || (IsTransformFromGizmo && ImGui.IsKeyReleased(ImGuiKey.MouseLeft))
                || ImGui.IsKeyPressed(ImGuiKey.MouseLeft, false)
                || ImGui.IsKeyPressed(ImGuiKey.Enter, false)
            ) && IsScaleActive
        )
        { // Apply action
            IsTransformFromGizmo = false;
            IsScaleActive = false;
            FinishTransform = false;
            _axisLock = Vector3.One;

            if (window.CurrentScene!.SelectedObjects.Count() == 1)
            {
                var sobj = window.CurrentScene.SelectedObjects.First();
                switch (sobj)
                {
                    case ISceneObj x when x is IStageSceneObj y:
                        ChangeHandler.ChangeStageObjTransform(
                            window.CurrentScene.History,
                            y,
                            "Scale",
                            ActTransform.Originals[sobj],
                            y.StageObj.Scale
                        );
                        break;
                    // case ISceneObj x when x is RailPointSceneObj y:
                    //     break;
                    // case ISceneObj x when x is RailHandleSceneObj y:
                    //     break;
                    case ISceneObj x when x is RailSceneObj y:
                        ChangeHandler.ChangeRailScale(
                            window.CurrentScene.History,
                            y,
                            ActTransform.Originals[y],
                            y.FakeScale
                        );
                    break;
                }
            }
            else
            {
                bool hasRails = window.CurrentScene.SelectedObjects.Any(x => x is RailSceneObj);

                foreach (ISceneObj scobj in window.CurrentScene.SelectedObjects)
                {   
                    switch (scobj)
                    {
                        case ISceneObj x when x is IStageSceneObj y:
                            ActTransform.Finals.Add(y, y.StageObj.Scale);
                            break;
                        case ISceneObj x when x is RailSceneObj y:
                            ActTransform.Finals.Add(y, y.FakeScale);
                            break;
                    }
                }
                ChangeHandler.ChangeMultiScale(window.CurrentScene.History, ActTransform.Originals, ActTransform.Finals);
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
            ActTransform.Finals = new();
            _transformChangeString = "";
            window.CurrentScene.IsSaved = false;
            // Add to Undo stack
        }
        else if (
            (ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            && IsScaleActive
        )
        { // Cancel action
            IsTransformFromGizmo = false;
            IsScaleActive = false;

            foreach (ISceneObj sobj in window.CurrentScene!.SelectedObjects)
            {
                if (sobj is IStageSceneObj) {(sobj as IStageSceneObj)!.StageObj.Scale = ActTransform.Originals[sobj]; sobj.UpdateTransform(); }
                //else if (sobj is RailPointSceneObj) {(sobj as RailPointSceneObj)!.FakeRotation = Vector3.Zero; (sobj as RailPointSceneObj)!.UpdateModel(); }
                else if (sobj is RailSceneObj) {(sobj as RailSceneObj)!.FakeScale = Vector3.One; (sobj as RailSceneObj)!.UpdateAfterScale(); }
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
            ActTransform.Finals = new();
            _axisLock = Vector3.One;
        }
    }

    private void TransformChange()
    {
        bool isPos = !_transformChangeString.Contains('-');
        string r = isPos ? _transformChangeString.Split('-')[0] : _transformChangeString.Split('-')[1];

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

        _transformChangeString = isPos ? r : "-" + r;
    }
}
