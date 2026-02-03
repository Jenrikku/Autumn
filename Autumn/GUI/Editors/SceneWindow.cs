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
using Autumn.History;

namespace Autumn.GUI.Editors;

internal class SceneWindow(MainWindowContext window)
{
    public bool IsWindowFocused => _isSceneWindowFocused;
    public bool IsTransformActive => _isTranslationActive || _isRotationActive || _isScaleActive;
    public bool IsTranslationFromDuplicate = false;
    private bool _isTranslationActive = false;
    private bool _isRotationActive = false;
    private bool _isScaleActive = false;
    private string _transformChangeString = "";

    internal static class ActTransform
    {
        public static Dictionary<ISceneObj, Vector3> Relative = new();
        public static Dictionary<ISceneObj, Vector3> Originals = new();
        public static Dictionary<ISceneObj, Vector3> Finals = new();
        public static string FullTransformString = "";
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

    public void AddMouseClickAction(Action<MainWindowContext, Vector4> action)
    {
        _mouseClickActions.Enqueue(action);
    }

    public void GetAxis()
    {
        if (ImGui.IsKeyPressed(ImGuiKey.X, false))
        {
            if (_axisLock.Y != 0 || _axisLock.Z != 0)
                _axisLock = Vector3.UnitX;
            else
                _axisLock = Vector3.One;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.Z, false))
        {
            if (_axisLock.X != 0 || _axisLock.Y != 0)
                _axisLock = Vector3.UnitZ;
            else
                _axisLock = Vector3.One;
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.Y, false))
        {
            if (_axisLock.Z != 0 || _axisLock.X != 0)
                _axisLock = Vector3.UnitY;
            else
                _axisLock = Vector3.One;
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
            return;
        }

        if (!sceneReady)
        {
            ImGui.TextDisabled("The stage is being loaded, please wait...");
            ImGui.End();
            return;
        }

        Vector2 contentAvail = ImGui.GetContentRegionAvail() - new Vector2(0, 24 * window.ScalingFactor);
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
        if (_isSceneHovered || _isSceneWindowFocused)
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

            // if ((window.Keyboard?.IsKeyP ressed(Key.Space) ?? false) && window.CurrentScene.SelectedObjects.Count() > 0){
            //     camera.LookAt(camera.Eye, window.CurrentScene.SelectedObjects.First().StageObj.Translation*0.01f);
            // }
            if (window.CurrentScene.SelectedObjects.Any())
            {
                bool camToObj = window.Keyboard?.IsKeyPressed(Key.Space) ?? false;
                if (window.Keyboard?.IsKeyPressed(Key.Keypad1) ?? false)
                {
                    camera.LookAt(camera.Eye, camera.Eye + new Vector3(1, 0, 0));
                    camToObj = true;
                }
                if (window.Keyboard?.IsKeyPressed(Key.Keypad2) ?? false)
                {
                    camera.LookAt(camera.Eye, camera.Eye + new Vector3(0, 1, 0));
                    camToObj = true;

                }
                if (window.Keyboard?.IsKeyPressed(Key.Keypad3) ?? false)
                {
                    camera.LookAt(camera.Eye, camera.Eye + new Vector3(0, 0, 1));
                    camToObj = true;

                }
                if (window.Keyboard?.IsKeyPressed(Key.Keypad4) ?? false)
                {
                    camera.LookAt(camera.Eye, camera.Eye + new Vector3(-1, 0, 0));
                    camToObj = true;

                }
                if (window.Keyboard?.IsKeyPressed(Key.Keypad5) ?? false)
                {
                    camera.LookAt(camera.Eye, camera.Eye + new Vector3(0, -1, 0));
                    camToObj = true;

                }
                if (window.Keyboard?.IsKeyPressed(Key.Keypad6) ?? false)
                {
                    camera.LookAt(camera.Eye, camera.Eye + new Vector3(0, 0, -1));
                    camToObj = true;

                }
                if (camToObj)
                {
                    ISceneObj sceneObj = window.CurrentScene.SelectedObjects.First();

                    AxisAlignedBoundingBox aabb = sceneObj.AABB;

                    switch (sceneObj)
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
                }
            }
        }

        if (_isSceneHovered)
        {
            // Tooltip
            ISceneObj? hoveringObj = window.CurrentScene.HoveringObject;

            if(hoveringObj is not null)
            {
#if DEBUG       
                // if (hoveringObj is ActorSceneObj)
                // ImGui.SetTooltip(((ActorSceneObj)hoveringObj).StageObj.Name);
                // else if (hoveringObj is RailSceneObj)
                // ImGui.SetTooltip(((RailSceneObj)hoveringObj).RailObj.Name);
                // else
                ImGui.SetTooltip(hoveringObj.GetType().ToString()+" "+ hoveringObj.PickingId.ToString());
#else
#endif
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
            upperRightCorner + new Vector2(-60, 60),
            radius: 45,
            out Vector3 facingDirection
        );
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

            if (ImGui.IsKeyPressed(ImGuiKey.J, false))
            {
                if (window.CurrentScene.SelectedObjects.FirstOrDefault(x => x is RailSceneObj) != null)
                {RailSceneObj rl = (RailSceneObj)window.CurrentScene.SelectedObjects.FirstOrDefault(x => x is RailSceneObj)!;
                ChangeHandler.ChangeAddPoint(window, window.CurrentScene.History, rl, 100 * new Vector3(worldMousePos.X, worldMousePos.Y, worldMousePos.Z));
                }
                else if (window.CurrentScene.SelectedObjects.FirstOrDefault(x => x is RailPointSceneObj) != null)
                {
                    RailPointSceneObj rp = (RailPointSceneObj)window.CurrentScene.SelectedObjects.FirstOrDefault(x => x is RailPointSceneObj)!;
                    ChangeHandler.ChangeInsertPoint(window, window.CurrentScene.History, rp.ParentRail, rp.ParentRail.RailPoints.IndexOf(rp), 100 * new Vector3(worldMousePos.X, worldMousePos.Y, worldMousePos.Z));
                }
            }
            // if (ImGui.IsKeyPressed(ImGuiKey.K, true) && window.CurrentScene.SelectedObjects.FirstOrDefault(x => x is RailPointSceneObj) != null)
            // {
            //     RailPointSceneObj rp = (RailPointSceneObj)window.CurrentScene.SelectedObjects.FirstOrDefault(x => x is RailPointSceneObj)!;
            //     rp.FakeRot.X += 3;
            //     //rp.RailPoint.Point2Trans = rp.RailPoint.Point2Trans * Matrix4x4.CreateTranslation(rp.RailPoint.Point0Trans * 0.01f) + Matrix4x4.CreateRotationX(20 * (float)Math.PI / 180);
            //     //rp.UpdateSceneHandles();
            //     rp.UpdateModelMoving();
            // }


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
                && !_isTranslationActive && !_isRotationActive && !_isScaleActive
                && (_mouseMoveKey == ImGuiMouseButton.Right ? !ImGui.IsKeyDown(ImGuiKey.ModAlt) : true))
            {
                if (!_isSceneWindowFocused)
                    ImGui.SetWindowFocus();

                if (orientationCubeHovered)
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
            }
            else if ((_mouseMoveKey == ImGuiMouseButton.Right ? (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.IsKeyDown(ImGuiKey.ModAlt)) : ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                && _isSceneHovered
                && !_isTranslationActive && !_isRotationActive && !_isScaleActive)
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
            else if ((_isSceneHovered && window.CurrentScene.SelectedObjects.Any()) || _isTranslationActive || _isScaleActive || _isRotationActive)
            {
                Vector3 _ndcMousePos3D =
                    new(ndcMousePos.X * sceneImageSize.X / 2,
                        ndcMousePos.Y * sceneImageSize.Y / 2,
                        (normPickingDepth * 10 - 1) / 10f);
                _ndcMousePos3D = Vector3.Transform(_ndcMousePos3D, window.CurrentScene.Camera.Rotation);

                if (!ImGui.GetIO().WantTextInput)
                {
                    if (ImGui.IsKeyPressed(ImGuiKey.G, false) && window.Keyboard!.IsShiftPressed())
                    {
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
                                    -y.ParentPoint.RailPoint.Point0Trans + 100 * new Vector3(worldMousePos.X, worldMousePos.Y, worldMousePos.Z)
                                );
                                break;
                            case ISceneObj x when x is RailSceneObj y:
                            break;
                        }

                        if (!_isSceneWindowFocused)
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
        }
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
                        window.CurrentScene.Stage.GetStageFile(StageFileType.Map).SetChild(stageSceneObj.StageObj, pickStSceneObj.StageObj);
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
                        window.CurrentScene.Stage.GetStageFile(StageFileType.Map).SetChild(pickStSceneObj1.StageObj, sobj.StageObj);
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
        if (_isTranslationActive || _isScaleActive || _isRotationActive)
        {
            ImGui.SetWindowFontScale(1.0f);
            string s = "";

            if (_isTranslationActive)
                s = "Moving ";
            else if (_isScaleActive)
                s = "Scaling ";
            else if (_isRotationActive)
                s = "Rotating ";

            if (window.CurrentScene!.SelectedObjects.Count() > 1)
                s += "multiple objects";
            else
            {
                var fs = window.CurrentScene.SelectedObjects.First();
                if (fs is IStageSceneObj) s+= (fs as IStageSceneObj)!.StageObj.Name; 
                else if (fs is RailSceneObj) s+= (fs as RailSceneObj)!.RailObj.Name;
                else s += fs.PickingId;// ((IStageSceneObj)window.CurrentScene.SelectedObjects.First(x => x is IStageSceneObj)).StageObj.Name;
            }


            if (_axisLock != Vector3.One)
            {
                s += " on the ";

                if (_axisLock == Vector3.UnitX)
                    s += "X ";
                else if (_axisLock == Vector3.UnitY)
                    s += "Y ";
                else
                    s += "Z ";

                s += "axis";

                if (_transformChangeString != "-" && _transformChangeString != "" && _axisLock != Vector3.One)
                    s += ": " + (_transformChangeString != "-" ? _transformChangeString : "");
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

        //ImGui.PushFont(window.FontPointers[1]);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1, default));
        float buttons = ImGui.CalcTextSize(IconUtils.GRID).X*4 + 4*10;
        ImGui.SetCursorPos(new Vector2(contentAvail.X - buttons, opos.Y - 3f));
        if (ImGui.Button(IconUtils.GRID))
        {
            ModelRenderer.VisibleGrid = !ModelRenderer.VisibleGrid;
        }
        ImGui.SetItemTooltip("Toggle Grid");
        ImGui.SameLine();
        
        if (ImGui.Button(IconUtils.PATH))
        {
            ModelRenderer.VisibleRails = !ModelRenderer.VisibleRails;
        }
        ImGui.SetItemTooltip("Toggle Rails");
        ImGui.SameLine();

        if (ImGui.Button(IconUtils.AREA))
        {
            ModelRenderer.VisibleAreas = !ModelRenderer.VisibleAreas;
        }
        ImGui.SetItemTooltip("Toggle Areas");
        ImGui.SameLine();
        if (ImGui.Button(IconUtils.CAMERA))
        {
            ModelRenderer.VisibleCameraAreas = !ModelRenderer.VisibleCameraAreas;
        }
        ImGui.SetItemTooltip("Toggle CameraAreas");

        ImGui.PopStyleVar(2);
        //ImGui.PopFont();
        ImGui.SetCursorPos(opos);
    }

    public void TranslateAction(Vector3 _ndcMousePos3D)
    {
        if (_isRotationActive || _isScaleActive)
            return;

        float dist = 0;
        if (_isTranslationActive)
        {
            if (!_isSceneWindowFocused)
                ImGui.SetWindowFocus();

            //multiply by distance to camera to make it responsive at different distances

            GetAxis();
            if (_axisLock != Vector3.One)
            {
                TransformChange();
            }
            else
                _transformChangeString = "";

            // var fst = window.CurrentScene!.SelectedObjects.First();
            // switch (fst)
            // {
            //     case ISceneObj x when x is IStageSceneObj y:
            //         dist = Vector3.Distance(
            //         ActTransform.Originals[y]  / 100,
            //         window.CurrentScene.Camera.Eye
            //         );
            //         break;
            //     case ISceneObj x when x is RailPointSceneObj y:
            //         dist = Vector3.Distance(
            //         y.RailPoint.Point0Trans / 100,
            //         window.CurrentScene.Camera.Eye
            //         );
            //         break;
            //     case ISceneObj x when x is RailHandleSceneObj y:
            //         dist = Vector3.Distance(
            //         y.Translation / 100,
            //         window.CurrentScene.Camera.Eye
            //         );
            //     break;
            //     case ISceneObj x when x is RailSceneObj y:
            //     break;
            // }

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
                        y.RailModel.Offset = nTr;
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
                            y.RailModel.Offset = MathUtils.Round(y.RailModel.Offset / 50) * 50;
                            break;
                    }
                }
                if (scobj is IStageSceneObj) scobj.UpdateTransform();
                else if (scobj is RailPointSceneObj) (scobj as RailPointSceneObj)!.UpdateModelMoving();
                else if (scobj is RailHandleSceneObj) (scobj as RailHandleSceneObj)!.UpdateTransform();
                else if (scobj is RailSceneObj) (scobj as RailSceneObj)!.UpdateModelTmp();

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
                    STR = y.RailModel.Offset;
                    break;
            }

            if (_axisLock == Vector3.One)
            {
                ActTransform.FullTransformString = $"X: {STR.X}, Y: {STR.Y}, Z: {STR.Z}";
            }
            else
            {
                if (_axisLock == Vector3.UnitX)
                    ActTransform.FullTransformString = $" {STR.X}";

                if (_axisLock == Vector3.UnitY)
                    ActTransform.FullTransformString = $" {STR.Y}";

                if (_axisLock == Vector3.UnitZ)
                    ActTransform.FullTransformString = $" {STR.Z}";
            }
        }

        // TODO: Movement for rails still needs to be implemented
        if ((ImGui.IsKeyPressed(ImGuiKey.G, false) && !_isTranslationActive) || IsTranslationFromDuplicate)
        { // Start action
            IsTranslationFromDuplicate = false;
            _isTranslationActive = true;

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
                    (y.RailModel.Offset) / 100,
                    window.CurrentScene.Camera.Eye
                    );
                break;
            }
            

            _ndcMousePos3D *= dist / 11;

            foreach (ISceneObj scobj in window.CurrentScene.SelectedObjects)
            {   
                switch (scobj)
                {
                    case ISceneObj x when x is IStageSceneObj y:
                        ActTransform.Originals.Add(y, y.StageObj.Translation);
                        ActTransform.Relative[y] = y.StageObj.Translation - _ndcMousePos3D;
                        break;
                    case ISceneObj x when x is RailHandleSceneObj y:
                        ActTransform.Originals.Add(y, y.Offset +y.ParentPoint.RailPoint.Point0Trans);
                        ActTransform.Relative[y] = y.Offset - _ndcMousePos3D;
                        break;
                    case ISceneObj x when x is RailPointSceneObj y:
                        ActTransform.Originals.Add(y, y.RailPoint.Point0Trans);
                        ActTransform.Relative[y] = y.RailPoint.Point0Trans - _ndcMousePos3D;
                        break;
                    case ISceneObj x when x is RailSceneObj y:
                        ActTransform.Originals.Add(y, y.RailModel.Offset);
                        ActTransform.Relative[y] = y.RailModel.Offset - _ndcMousePos3D;
                        break;
                }
            }
        }
        else if (
            (
                ImGui.IsKeyPressed(ImGuiKey.G, false)
                || ImGui.IsKeyPressed(ImGuiKey.MouseLeft, false)
                || ImGui.IsKeyPressed(ImGuiKey.Enter, false)
            ) && _isTranslationActive
        )
        { // Apply action
            _isTranslationActive = false;
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
                        ChangeHandler.ChangeRailTransform(
                            window.CurrentScene.History,
                            y,
                            ActTransform.Originals[y],
                            y.RailModel.Offset
                        );
                    break;
                }
                
            }
            else
            {
                foreach (ISceneObj scobj in window.CurrentScene.SelectedObjects)
                {   
                    switch (scobj)
                    {
                        case ISceneObj x when x is IStageSceneObj y:
                            ActTransform.Finals.Add(y, y.StageObj.Translation);
                            break;
                        case ISceneObj x when x is RailHandleSceneObj y:
                            ActTransform.Finals.Add(y, y.Offset +y.ParentPoint.RailPoint.Point0Trans);
                            break;
                        case ISceneObj x when x is RailPointSceneObj y:
                            ActTransform.Finals.Add(y, y.RailPoint.Point0Trans);
                            break;
                        case ISceneObj x when x is RailSceneObj y:
                            ActTransform.Finals.Add(y, y.RailModel.Offset);
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
            (ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            && _isTranslationActive
        )
        { // Cancel action
            _isTranslationActive = false;

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
                        y.RailModel.Offset = ActTransform.Originals[scobj];
                        y.UpdateModel();
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
        if (_isScaleActive || _isTranslationActive)
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
        if (_isRotationActive)
        {
            if (!_isSceneWindowFocused)
                ImGui.SetWindowFocus();

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
                        (sobj as RailPointSceneObj)!.FakeRot =
                            ActTransform.Originals[sobj] + _axisLock * float.Parse(_transformChangeString);
                    }
                    else
                    {
                        (sobj as RailPointSceneObj)!.FakeRot =
                            ActTransform.Originals[sobj] + _axisLock * (-ActTransform.Relative[sobj].X + (float)rot);
                    }

                    if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
                    {
                        (sobj as RailPointSceneObj)!.FakeRot = MathUtils.Round((sobj as RailPointSceneObj)!.FakeRot / 5) * 5;
                    }
                    (sobj as RailPointSceneObj)!.UpdateModelRotating();
                }
            }

            var fst = window.CurrentScene.SelectedObjects.First();
            var STR = (fst is IStageSceneObj) ? (fst as IStageSceneObj)!.StageObj.Rotation : (fst as RailPointSceneObj)!.FakeRot;

            if (_axisLock == Vector3.One)
            {
                ActTransform.FullTransformString = $"X: {STR.X}, Y: {STR.Y}, Z: {STR.Z}";
            }
            else
            {
                if (_axisLock == Vector3.UnitX)
                    ActTransform.FullTransformString = $" {STR.X}";

                if (_axisLock == Vector3.UnitY)
                    ActTransform.FullTransformString = $" {STR.Y}";

                if (_axisLock == Vector3.UnitZ)
                    ActTransform.FullTransformString = $" {STR.Z}";
            }
        }

        if (ImGui.IsKeyPressed(ImGuiKey.R, false) && !_isRotationActive)
        { // Start action
            _isRotationActive = true;

            foreach (ISceneObj sobj in window.CurrentScene!.SelectedObjects)
            {
                if (sobj is IStageSceneObj) ActTransform.Originals.Add(sobj, (sobj as IStageSceneObj)!.StageObj.Rotation);
                else if (sobj is RailPointSceneObj) ActTransform.Originals.Add(sobj, Vector3.Zero);
                else { _isRotationActive = false; return;}
                ActTransform.Relative.Add(sobj, Vector3.UnitX * (float)rot);
            }
            if (ActTransform.Originals.Count < 1) 
                _isRotationActive = false;
        }
        else if (
            (
                ImGui.IsKeyPressed(ImGuiKey.R, false)
                || ImGui.IsKeyPressed(ImGuiKey.MouseLeft, false)
                || ImGui.IsKeyPressed(ImGuiKey.Enter, false)
            ) && _isRotationActive
        )
        { // Apply action
            _isRotationActive = false;
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
                            y.FakeRot
                        );
                        break;
                }
            }
            else
            {
                foreach (ISceneObj scobj in window.CurrentScene.SelectedObjects)
                {   
                    switch (scobj)
                    {
                        case ISceneObj x when x is IStageSceneObj y:
                            ActTransform.Finals.Add(y, y.StageObj.Rotation);
                            break;
                        case ISceneObj x when x is RailHandleSceneObj y:
                            //ActTransform.Finals.Add(y, y.Offset +y.ParentPoint.RailPoint.Point0Trans);
                            break;
                        case ISceneObj x when x is RailPointSceneObj y:
                            ActTransform.Finals.Add(y, y.FakeRot);
                            break;
                        case ISceneObj x when x is RailSceneObj y:
                            //ActTransform.Finals.Add(y, y.RailModel.Offset);
                            break;
                    }
                }
                ChangeHandler.ChangeMultiRotate(window.CurrentScene.History, ActTransform.Originals, ActTransform.Finals);
                //ChangeMultiRotate
                //ChangeHandler.ChangeMultiTransform(window.CurrentScene.History, ActTransform.Originals, "Rotation");
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
            _transformChangeString = "";
            window.CurrentScene.IsSaved = false;
            // Add to Undo stack
        }
        else if (
            (ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            && _isRotationActive
        )
        { // Cancel action
            _isRotationActive = false;


            foreach (ISceneObj sobj in window.CurrentScene!.SelectedObjects)
            {
                if (sobj is IStageSceneObj) {(sobj as IStageSceneObj)!.StageObj.Rotation = ActTransform.Originals[sobj]; sobj.UpdateTransform(); }
                else if (sobj is RailPointSceneObj) {(sobj as RailPointSceneObj)!.FakeRot = Vector3.Zero; (sobj as RailPointSceneObj)!.UpdateModel(); }
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
            _axisLock = Vector3.One;
        }
    }

    public void ScaleAction(Vector3 _ndcMousePos3D)
    { // Get distance to object and if it decreases we scale down, otherwise we increase, by default it scales in all axis, can scale on individual axis
        if (_isRotationActive || _isTranslationActive)
            return;

        float dist = 0;
        if (_isScaleActive)
        {
            if (!_isSceneWindowFocused)
                ImGui.SetWindowFocus();
            //multiply by distance to camera

            GetAxis();

            if (_axisLock != Vector3.One)
            {
                TransformChange();
            }
            else
                _transformChangeString = "";

            dist = Vector3.Distance(
                ((IStageSceneObj)window.CurrentScene!.SelectedObjects.First(x => x is IStageSceneObj)).StageObj.Translation / 100,
                window.CurrentScene.Camera.Eye
            );

            _ndcMousePos3D *= dist / 2;

            foreach (IStageSceneObj sobj in window.CurrentScene.SelectedObjects.Where(x => x is IStageSceneObj).Cast<IStageSceneObj>())
            {
                sobj.StageObj.Scale = ActTransform.Originals[sobj];
                float distA = Vector3.Distance(window.CurrentScene.Camera.Eye, ActTransform.Relative[sobj]);
                float distB = Vector3.Distance(window.CurrentScene.Camera.Eye, _ndcMousePos3D);

                if (_transformChangeString != string.Empty && _transformChangeString != "-")
                {
                    sobj.StageObj.Scale =
                        ActTransform.Originals[sobj]
                        + Vector3.One * (distB - distA) / 500 * _axisLock * float.Parse(_transformChangeString); // original scale * (distance to selection from mouse )
                }
                else
                {
                    sobj.StageObj.Scale = ActTransform.Originals[sobj] + Vector3.One * (distB - distA) / 500 * _axisLock; // original scale * (distance to selection from mouse )
                }

                if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
                {
                    sobj.StageObj.Scale = MathUtils.Round(sobj.StageObj.Scale * 10) / 10;
                }

                sobj.UpdateTransform();
            }

            var STR = ((IStageSceneObj)window.CurrentScene.SelectedObjects.First(x => x is IStageSceneObj)).StageObj.Scale;
            if (_axisLock == Vector3.One)
            {
                ActTransform.FullTransformString = $"X: {STR.X}, Y: {STR.Y}, Z: {STR.Z}";
            }
            else
            {
                if (_axisLock == Vector3.UnitX)
                    ActTransform.FullTransformString = $" {STR.X}";
                if (_axisLock == Vector3.UnitY)
                    ActTransform.FullTransformString = $" {STR.Y}";
                if (_axisLock == Vector3.UnitZ)
                    ActTransform.FullTransformString = $" {STR.Z}";
            }
        }

        if (ImGui.IsKeyPressed(_scaleKey, false) && !_isScaleActive && !ImGui.IsKeyDown(ImGuiKey.ModCtrl))
        { // Start action
            _isScaleActive = true;

            var fobj = window.CurrentScene!.SelectedObjects.First();
            if (fobj is not IStageSceneObj) { _isScaleActive = false; return;}
            dist = Vector3.Distance(
                (fobj as IStageSceneObj)!.StageObj.Translation / 100,
                window.CurrentScene.Camera.Eye
            );

            _ndcMousePos3D *= dist / 2;

            foreach (IStageSceneObj sobj in window.CurrentScene.SelectedObjects.Where(x => x is IStageSceneObj).Cast<IStageSceneObj>())
            {
                ActTransform.Originals.Add(sobj, sobj.StageObj.Scale);
                ActTransform.Relative.Add(sobj, _ndcMousePos3D);
            }
        }
        else if (
            (
                ImGui.IsKeyPressed(_scaleKey, false)
                || ImGui.IsKeyPressed(ImGuiKey.MouseLeft, false)
                || ImGui.IsKeyPressed(ImGuiKey.Enter, false)
            ) && _isScaleActive
        )
        { // Apply action
            _isScaleActive = false;
            _axisLock = Vector3.One;

            if (window.CurrentScene!.SelectedObjects.Count() == 1)
            {
                var sobj = (IStageSceneObj)window.CurrentScene.SelectedObjects.First(x => x is IStageSceneObj);
                ChangeHandler.ChangeStageObjTransform(
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
            _transformChangeString = "";
            window.CurrentScene.IsSaved = false;
            // Add to Undo stack
        }
        else if (
            (ImGui.IsMouseClicked(ImGuiMouseButton.Right) || ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            && _isScaleActive
        )
        { // Cancel action
            _isScaleActive = false;

            foreach (IStageSceneObj sobj in window.CurrentScene!.SelectedObjects.Where(x => x is IStageSceneObj).Cast<IStageSceneObj>())
            {
                sobj.StageObj.Scale = ActTransform.Originals[sobj];
                sobj.UpdateTransform();
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
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
