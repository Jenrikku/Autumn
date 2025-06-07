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

        _mouseMoveKey = window.ContextHandler.SystemSettings.UseMiddleMouse ? ImGuiMouseButton.Middle: ImGuiMouseButton.Right;

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

        if (_isSceneHovered || _isSceneWindowFocused)
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

            if (window.Mouse!.ScrollWheels[0].Y != 0 && _isSceneHovered)
            {
                camera.Eye -= Vector3.Transform(
                    Vector3.UnitZ * window.Mouse.ScrollWheels[0].Y * 6 * camMoveSpeed,
                    camera.Rotation
                );
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
                    AxisAlignedBoundingBox aabb = window.CurrentScene.SelectedObjects.First().AABB * window.CurrentScene.SelectedObjects.First().StageObj.Scale;
                    camera.LookFrom(window.CurrentScene.SelectedObjects.First().StageObj.Translation * 0.01f, aabb.GetDiagonal() * 0.01f);
                }
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
            else if ((_mouseMoveKey == ImGuiMouseButton.Right ? (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.IsKeyDown(ImGuiKey.ModAlt)) : ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                && _isSceneHovered
                && !_isTranslationActive && !_isRotationActive && !_isScaleActive)
            {
                _pickObject = window.CurrentScene.GetSceneObjFromPicking(pixel);
                if (_pickObject != null)
                {
                    _objectOptionsPos = windowMousePos;
                    bool select1 = window.CurrentScene.SelectedObjects.Count() == 1;

                    bool selectover1 = window.CurrentScene.SelectedObjects.Count() > 0;

                    _selNotSame = selectover1 ? !window.CurrentScene.SelectedObjects.Contains(_pickObject) : true;

                    var sType = _pickObject.StageObj.Type;
                    bool isSelectionChildable = (sType == StageObjType.Area
                                    || sType == StageObjType.AreaChild
                                    || sType == StageObjType.Child
                                    || sType == StageObjType.Regular)
                                    && _pickObject.StageObj.FileType == StageFileType.Map;
                    bool cantChild = window.CurrentScene.SelectedObjects.Any(x => (x.StageObj.Type != StageObjType.Area
                                    && x.StageObj.Type != StageObjType.AreaChild
                                    && x.StageObj.Type != StageObjType.Child
                                    && x.StageObj.Type != StageObjType.Regular)
                                    || x.StageObj.FileType != StageFileType.Map
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

        GizmoDrawer.EndGizmoDrawing();

        ActionPanel(contentAvail);
        ActionMenu(deltaSeconds);

        ImGui.End();
    }
    private void ActionMenu(double deltaSeconds)
    {

        if (_isObjectOptionsEnabled)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, ImGui.GetColorU32(ImGuiCol.WindowBg));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 3);
            var mpos = _objectOptionsPos - new Vector2(0, -20);
            ImGui.SetCursorPos(mpos);
            float w = ImGui.CalcTextSize(_pickObject.StageObj.Name).X * 1.3f + 10;
            if (ImGui.BeginListBox("##SelectableListbox", new(w > 140 ? w : 140, 150)))
            {
                ImGui.SetWindowFontScale(1.3f);
                ImGui.Text(_pickObject.StageObj.Name);
                ImGui.SetWindowFontScale(0.95f);
                ImGui.Separator();

                if (_selCantParent)
                    ImGui.BeginDisabled();
                if (ImGui.Selectable("Set parent of selection"))
                {
                    _isObjectOptionsEnabled = false;
                    foreach (ISceneObj sobj in window.CurrentScene.SelectedObjects)
                    {
                        if (sobj == _pickObject) continue;
                        if (_pickObject.StageObj.Parent != null && _pickObject.StageObj.Parent == sobj.StageObj) continue;
                        window.CurrentScene.Stage.GetStageFile(StageFileType.Map).SetChild(sobj.StageObj, _pickObject.StageObj);
                    }
                }
                if (_selCantParent)
                    ImGui.EndDisabled();
                ImGui.Separator();

                if (_selCantChild)
                    ImGui.BeginDisabled();
                if (ImGui.Selectable("Add as child of selection"))
                {
                    _isObjectOptionsEnabled = false;
                    var sobj = window.CurrentScene.SelectedObjects.First();
                    if (sobj.StageObj.Parent != _pickObject.StageObj)
                    {
                        window.CurrentScene.Stage.GetStageFile(StageFileType.Map).SetChild(_pickObject.StageObj, sobj.StageObj);
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
            if (_objectOptionsTime >= 0.45)
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
                s += window.CurrentScene.SelectedObjects.First().StageObj.Name;

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
        float buttons = /*ImGui.CalcTextSize("Toggle Paths").X + 1 +*/ ImGui.CalcTextSize("Toggle Grid").X + 1 + ImGui.CalcTextSize("Toggle Areas").X + 1 + ImGui.CalcTextSize("Toggle CameraAreas").X + 1;
        ImGui.SetCursorPos(new Vector2(contentAvail.X - buttons - 24, opos.Y - 3f));
        // if (ImGui.Button("Toggle Paths"))
        // {
        //     ModelRenderer.visibleAreas = !ModelRenderer.visibleAreas;
        // }
        // ImGui.SameLine();
        if (ImGui.Button("Toggle Grid"))
        {
            ModelRenderer.VisibleGrid = !ModelRenderer.VisibleGrid;
        }

        ImGui.SameLine();

        if (ImGui.Button("Toggle Areas"))
        {
            ModelRenderer.VisibleAreas = !ModelRenderer.VisibleAreas;
        }
        ImGui.SameLine();
        if (ImGui.Button("Toggle CameraAreas"))
        {
            ModelRenderer.VisibleCameraAreas = !ModelRenderer.VisibleCameraAreas;
        }

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

            dist = Vector3.Distance(
                ActTransform.Originals[window.CurrentScene!.SelectedObjects.First()] / 100,
                window.CurrentScene.Camera.Eye
            );

            _ndcMousePos3D *= dist / 8;

            foreach (ISceneObj scobj in window.CurrentScene.SelectedObjects)
            {
                Vector3 defPos = ActTransform.Originals[scobj] - (ActTransform.Relative[scobj] + _ndcMousePos3D); // default position

                if (_transformChangeString != string.Empty && _transformChangeString != "-")
                {
                    defPos = -Vector3.One * float.Parse(_transformChangeString);
                }

                Vector3 nTr = ActTransform.Originals[scobj] - defPos * _axisLock;
                scobj.StageObj.Translation = nTr;

                if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
                {
                    scobj.StageObj.Translation = MathUtils.Round(scobj.StageObj.Translation / 50) * 50;
                }

                scobj.UpdateTransform();
            }

            var STR = window.CurrentScene.SelectedObjects.First().StageObj.Translation;

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

        if ((ImGui.IsKeyPressed(ImGuiKey.G, false) && !_isTranslationActive) || IsTranslationFromDuplicate)
        { // Start action
            IsTranslationFromDuplicate = false;
            _isTranslationActive = true;

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
            ) && _isTranslationActive
        )
        { // Apply action
            _isTranslationActive = false;
            _axisLock = Vector3.One;

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
                scobj.StageObj.Translation = ActTransform.Originals[scobj]; // Reset to what it was
                scobj.UpdateTransform();
            }

            ActTransform.Relative = new();
            ActTransform.Originals = new();
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
                if (_transformChangeString != string.Empty && _transformChangeString != "-")
                {
                    sobj.StageObj.Rotation =
                        ActTransform.Originals[sobj] + _axisLock * float.Parse(_transformChangeString);
                }
                else
                {
                    sobj.StageObj.Rotation =
                        ActTransform.Originals[sobj] + _axisLock * (-ActTransform.Relative[sobj].X + (float)rot);
                }

                if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) || ImGui.IsKeyDown(ImGuiKey.ModSuper))
                {
                    sobj.StageObj.Rotation = MathUtils.Round(sobj.StageObj.Rotation / 5) * 5;
                }

                sobj.UpdateTransform();
            }

            var STR = window.CurrentScene.SelectedObjects.First().StageObj.Rotation;

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
                ActTransform.Originals.Add(sobj, sobj.StageObj.Rotation);
                ActTransform.Relative.Add(sobj, Vector3.UnitX * (float)rot);
            }
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
                sobj.StageObj.Rotation = ActTransform.Originals[sobj];
                sobj.UpdateTransform();
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
                window.CurrentScene!.SelectedObjects.First().StageObj.Translation / 100,
                window.CurrentScene.Camera.Eye
            );

            _ndcMousePos3D *= dist / 2;

            foreach (ISceneObj sobj in window.CurrentScene.SelectedObjects)
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

            var STR = window.CurrentScene.SelectedObjects.First().StageObj.Scale;
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

            foreach (ISceneObj sobj in window.CurrentScene!.SelectedObjects)
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
