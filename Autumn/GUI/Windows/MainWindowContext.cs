using System.Numerics;
using Autumn.Background;
using Autumn.Context;
using Autumn.Enums;
using Autumn.GUI.Dialogs;
using Autumn.GUI.Editors;
using Autumn.Rendering;
using Autumn.Rendering.Gizmo;
using Autumn.Rendering.Rail;
using Autumn.Storage;
using Autumn.Utils;
using Autumn.Wrappers;
using ImGuiNET;
using SceneGL.GLHelpers;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Autumn.GUI.Windows;

internal class MainWindowContext : WindowContext
{
    public List<Scene> Scenes { get; } = new();
    public Scene? CurrentScene { get; set; }

    public SceneGL.GLWrappers.Framebuffer SceneFramebuffer { get; }
    public SceneGL.GLWrappers.Framebuffer CameraFramebuffer { get; }

    public BackgroundManager BackgroundManager { get; } = new();
    public GLTaskScheduler GLTaskScheduler { get; } = new();

    public bool IsTransformActive => _sceneWindow.IsTransformActive;
    public bool IsSceneFocused => _sceneWindow.IsWindowFocused || IsFocused;

    private bool _isFirstFrame = true;

    private static uint s_welcomeImage;

    #region Dialogs
    private readonly AddStageDialog _addStageDialog;
    private readonly ClosingDialog _closingDialog;
    private readonly NewStageObjDialog _newStageObjDialog;
    private readonly SettingsDialog _settingsDialog;
    private readonly ShortcutsDialog _shortcutsDialog;
    private readonly DatabaseEditor _DBEditorDialog;
    #endregion

    #region Editor Dialogs
    public readonly EditChildrenDialog _editChildrenDialog;
    public readonly EditExtraPropsDialog _editExtraPropsDialog;
    public readonly EditCreatorClassNameTable _editCCNT;
    #endregion

    #region Editor Windows 
    private readonly StageWindow _stageWindow;
    private readonly ObjectWindow _objectWindow;
    private readonly PropertiesWindow _propertiesWindow;
    private readonly SceneWindow _sceneWindow;
    private readonly WelcomeDialog _welcomeDialog;
    #endregion

    #region Params Windows
    private readonly MiscParamsWindow _miscParams;
    private readonly CameraParamsWindow _camParams;
    private readonly FogParamsWindow _fogParams;
    private readonly LightParamsWindow _lightParams;
    private readonly SwitchesWindow _switchParams;
    #endregion

#if DEBUG
    private bool _showDemoWindow = false;
#endif

    public MainWindowContext(ContextHandler contextHandler, WindowManager windowManager)
        : base(contextHandler, windowManager)
    {
        // Initialize dialogs:
        _addStageDialog = new(this);
        _closingDialog = new(this);
        _newStageObjDialog = new(this);
        _welcomeDialog = new(this);
        _editChildrenDialog = new(this);
        _editExtraPropsDialog = new(this);
        _settingsDialog = new(this);
        _editCCNT = new(this);
        _shortcutsDialog = new(this);
        _DBEditorDialog = new(this);

        // Initialize editors:
        _stageWindow = new(this);
        _objectWindow = new(this);
        _propertiesWindow = new(this);
        _sceneWindow = new(this);

        // Initialize param editors
        _miscParams = new(this);
        _camParams = new(this);
        _fogParams = new(this);
        _lightParams = new(this);
        _switchParams = new(this);

        Window.Title = "Autumn: Stage Editor";

        SceneFramebuffer = new(
            initialSize: null,
            depthAttachment: SceneGL.PixelFormat.D24_UNorm_S8_UInt,
            SceneGL.PixelFormat.R8_G8_B8_A8_UNorm, // Regular color.
            SceneGL.PixelFormat.R32_UInt // Used for object selection.
        );
        CameraFramebuffer = new(
            initialSize: null,
            depthAttachment: SceneGL.PixelFormat.D24_UNorm_S8_UInt,
            SceneGL.PixelFormat.R8_G8_B8_A8_UNorm // Regular color.
        );

        Window.Load += () =>
        {
            InfiniteGrid.Initialize(GL!);
            RailRenderer.Initialize(GL!);
            ModelRenderer.Initialize(GL!, contextHandler.FSHandler);

            var cubeTex = Image.Load<Rgba32>(Path.Join("Resources", "OrientationCubeTex.png"));
            var cubeTexPixels = new Rgba32[cubeTex.Width * cubeTex.Height];

            cubeTex.CopyPixelDataTo(cubeTexPixels);

            uint cubeTexName = TextureHelper.CreateTexture2D<Rgba32>(
                GL!,
                SceneGL.PixelFormat.R8_G8_B8_A8_UNorm,
                (uint)cubeTex.Width,
                (uint)cubeTex.Height,
                cubeTexPixels,
                true
            );

            GizmoDrawer.SetOrientationCubeTexture((nint)cubeTexName);

            if (s_welcomeImage != 0)
                return;

            // Welcome screen image
            var welcomeImage = Image.Load<Rgba32>(Path.Join("Resources", "Icons", "autumn256.png"));
            var welcomeTexPixels = new Rgba32[welcomeImage.Width * welcomeImage.Height];

            welcomeImage.CopyPixelDataTo(welcomeTexPixels);

            uint welcomeTex = GL!.GenTexture();
            GL!.BindTexture(TextureTarget.Texture2D, welcomeTex);

            GL!.TexImage2D<Rgba32>(TextureTarget.Texture2D, 0, 
                InternalFormat.Rgba8, 
                (uint)welcomeImage.Width, (uint)welcomeImage.Height, 0, 
                PixelFormat.Rgba, 
                PixelType.UnsignedByte, 
                welcomeTexPixels);

            GL!.GenerateMipmap(TextureTarget.Texture2D);

            GL!.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, [(uint)TextureMagFilter.Linear]);
            GL!.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, [(uint)TextureMinFilter.Linear]);

            GL!.BindTexture(TextureTarget.Texture2D, 0);

            s_welcomeImage = welcomeTex;
        };

        Window.Render += (deltaSeconds) =>
        {
            if (ImGuiController is null)
                return;

            ImGuiController.MakeCurrent();

            if (_isFirstFrame)
            {
                ImGuiIOPtr io = ImGui.GetIO();

                io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
                io.ConfigWindowsMoveFromTitleBarOnly = true;

                // Fix docking settings not loading properly:
                ImGui.LoadIniSettingsFromDisk(ImguiSettingsFile);

                ModelRenderer.VisibleAreas = ContextHandler.SystemSettings.VisibleDefaults[0];
                ModelRenderer.VisibleCameraAreas = ContextHandler.SystemSettings.VisibleDefaults[1];
                ModelRenderer.VisibleRails = ContextHandler.SystemSettings.VisibleDefaults[2];
                ModelRenderer.VisibleGrid = ContextHandler.SystemSettings.VisibleDefaults[3];

                switch (ContextHandler.SystemSettings.Theme)
                {
                    case 2:
                        ImGui.StyleColorsLight();
                        break;

                    default:
                        ImGui.StyleColorsDark();
                        break;
                }

                if (!ContextHandler.SystemSettings.SkipWelcomeDialog)
                    _welcomeDialog.Open();

            }

            ImGuiViewportPtr viewport = ImGui.GetMainViewport();

            RenderMainMenuBar(out float barHeight);
            RenderStatusBar(barHeight, viewport.Size);

            #region DockSpace

            // This style vars will only be applied to the window containing the dockspace.
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

            ImGui.SetNextWindowPos(viewport.Pos + new Vector2(0, barHeight));
            ImGui.SetNextWindowSize(viewport.Size - new Vector2(0, barHeight * 2));
            ImGui.SetNextWindowViewport(viewport.ID);

            // Window that contains the dockspace.
            ImGui.Begin(
                "mainDockSpaceWindow",
                ImGuiWindowFlags.NoDecoration
                    | ImGuiWindowFlags.NoBringToFrontOnFocus
                    | ImGuiWindowFlags.NoSavedSettings
            );

            ImGui.PopStyleVar(2);

            ImGui.DockSpace(ImGui.GetID("mainDockSpace"));
            ImGui.End();

            #endregion

            if (!ContextHandler.IsProjectLoaded)
            {
                if (!_welcomeDialog.IsOpened) RenderWelcomeScreen(barHeight);
            }
            else
            {
                _objectWindow.Render();
                _propertiesWindow.Render();
                _sceneWindow.Render(deltaSeconds);
                _stageWindow.Render();
            }

#if DEBUG
            if (_showDemoWindow)
                ImGui.ShowDemoWindow(ref _showDemoWindow);
#endif
            _addStageDialog.Render();
            _closingDialog.Render();
            _newStageObjDialog.Render();
            _editChildrenDialog.Render();
            _editExtraPropsDialog.Render();
            _editCCNT.Render();
            _DBEditorDialog.Render();
            _shortcutsDialog.Render();
            _welcomeDialog.Render();
            _settingsDialog.Render();

            _miscParams.Render();
            _camParams.Render();
            _fogParams.Render();
            _lightParams.Render();
            _switchParams.Render();

            if (_isFirstFrame)
            {
                _isFirstFrame = false;
                ImGui.SetWindowFocus("Stages");
            }

            GLTaskScheduler.DoTasks(GL!, deltaSeconds);

            GL!.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL!.Clear(ClearBufferMask.ColorBufferBit);
            GL!.Viewport(Window.FramebufferSize);
            ImGuiController.Render();
        };

        Window.FileDrop += (paths) =>
        {
            if (paths.Length < 1)
                return;

            string path = paths[0];

            if (Directory.Exists(path))
                ContextHandler.OpenProject(path);
            else if (ContextHandler.IsProjectLoaded && File.Exists(path) && Path.GetExtension(path) == ".szs")
            {
                BackgroundManager.Add(
                    $"Importing stage from {path}...",
                    manager =>
                    {
                        Stage? stage = ContextHandler.FSHandler.TryReadStage(path);
                        if (stage == null)
                            return;
                        Scene scene =
                            new(
                                stage,
                                ContextHandler.FSHandler,
                                GLTaskScheduler,
                                ref manager.StatusMessageSecondary
                            );
                        scene.ResetCamera();
                        Scenes.Add(scene);
                        ImGui.SetWindowFocus("Objects");
                    }
                );
            }

        };
    }

    public override bool Close()
    {
        if (BackgroundManager.IsBusy)
        {
            _closingDialog.Open();
            return false;
        }

        // The window is only closed if all tasks have finished.
        return true;
    }

    public void OpenAddStageDialog() => _addStageDialog.Open();

    public void OpenAddObjectDialog() => _newStageObjDialog.Open();

    public void OpenSettingsDialog() => _settingsDialog.Open();
    public void OpenDbEntryDialog(ClassDatabaseWrapper.DatabaseEntry e) => _DBEditorDialog.Open(e);

    public void AddSceneMouseClickAction(Action<MainWindowContext, Vector4> action) =>
        _sceneWindow.AddMouseClickAction(action);

    public void SetSceneDuplicateTranslation() =>
        _sceneWindow.IsTranslationFromDuplicate = true;

    public void SetSwitchSelected(int i)
    {
        _switchParams.IsOpen = true;
        _switchParams.SelectedSwitch = i;
        ImGui.SetWindowFocus("Switches##SwitchWindow");
    }
    public void SetCameraSelected(int i)
    {
        _camParams.IsOpen = true;
        CurrentScene!.SelectedCam = i;
        ImGui.SetWindowFocus("cameras");
    }
    public void UpdateCameraList()
    {
        _propertiesWindow.updateCameras = true;
    }
    internal void SetupChildrenDialog(StageObj stageObj) => _editChildrenDialog.Open(stageObj);
    internal void SetupExtraPropsDialog(StageObj stageObj, string propName) => _editExtraPropsDialog.Open(stageObj, propName);
    internal void SetupExtraPropsDialogNew(StageObj stageObj) => _editExtraPropsDialog.New(stageObj);
    internal void CloseCurrentScene()
    {
        int i = Scenes.IndexOf(CurrentScene!) - 1;
        Scenes.Remove(CurrentScene!);
        if (i < 0)
            CurrentScene = null;
        else
            CurrentScene = Scenes[i];
        if (Scenes.Count == 0)
        {
            ImGui.SetWindowFocus("Stages");
        }

    }

    /// <summary>
    /// Renders the main menu bar seen at the very top of the window.
    /// </summary>
    /// <seealso cref="ImGuiWidgets.CommandMenuItem"/>
    /// <seealso cref="Commands"/>
    private void RenderMainMenuBar(out float height)
    {
        height = 0;

        if (!ImGui.BeginMainMenuBar())
            return;

        height = ImGui.GetWindowHeight();

        if (ImGui.BeginMenu("Project"))
        {
            ImGuiWidgets.CommandMenuItem(CommandID.NewProject, ContextHandler.ActionHandler, this);
            ImGuiWidgets.CommandMenuItem(CommandID.OpenProject, ContextHandler.ActionHandler, this);
            ImGuiWidgets.CommandMenuItem(CommandID.CloseProject, ContextHandler.ActionHandler, this);

            // Menu that displays the recently opened projects' list.
            if (ImGui.BeginMenu("Recent"))
            {
                if (ContextHandler.SystemSettings.RecentlyOpenedPaths.Count <= 0)
                    ImGui.TextDisabled("There are no recent entries.");
                else
                {
                    foreach (string path in ContextHandler.SystemSettings.RecentlyOpenedPaths)
                        if (ImGui.Selectable(path))
                        {
                            ContextHandler.OpenProject(path);
                            break;
                        }

                    ImGui.Separator();

                    if (ImGui.Selectable("Clear all"))
                        ContextHandler.SystemSettings.RecentlyOpenedPaths.Clear();
                }

                ImGui.EndMenu();
            }

            ImGui.Separator();

            ImGuiWidgets.CommandMenuItem(CommandID.AddStage, ContextHandler.ActionHandler, this);
            ImGuiWidgets.CommandMenuItem(CommandID.SaveStage, ContextHandler.ActionHandler, this);

            ImGui.Separator();

            ImGuiWidgets.CommandMenuItem(CommandID.OpenSettings, ContextHandler.ActionHandler, this);

            ImGui.Separator();

            ImGuiWidgets.CommandMenuItem(CommandID.Exit, ContextHandler.ActionHandler, this);

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Edit"))
        {
            ImGuiWidgets.CommandMenuItem(CommandID.Undo, ContextHandler.ActionHandler, this);
            ImGuiWidgets.CommandMenuItem(CommandID.Redo, ContextHandler.ActionHandler, this);

            ImGui.Separator();

            ImGuiWidgets.CommandMenuItem(CommandID.AddObject, ContextHandler.ActionHandler, this);
            ImGuiWidgets.CommandMenuItem(CommandID.DuplicateObj, ContextHandler.ActionHandler, this);
            ImGuiWidgets.CommandMenuItem(CommandID.RemoveObj, ContextHandler.ActionHandler, this);

            ImGui.Separator();

            ImGuiWidgets.CommandMenuItem(CommandID.HideObj, ContextHandler.ActionHandler, this);

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Stage"))
        {
            if (ImGui.MenuItem("Edit General Params"))
            {
                //_paramsWindow.MiscEnabled = true;
                _miscParams.IsOpen = true;
            }
            if (ImGui.MenuItem("Edit Cameras"))
                _camParams.IsOpen = true;
            //ImGui.SetWindowFocus("cameras");
            ImGui.Separator();
            if (ImGui.MenuItem("Edit Fogs"))
                _fogParams.IsOpen = true;
            if (ImGui.MenuItem("Edit Lights"))
                _lightParams.IsOpen = true;
            if (ImGui.MenuItem("Edit Switches"))
                _switchParams.IsOpen = true;
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("General"))
        {
            if (ImGui.MenuItem("CreatorClassNameTable"))
                _editCCNT.Open();
            ImGui.BeginDisabled();
            if (ImGui.MenuItem("Audio"))
                _editCCNT.Open();
            if (ImGui.MenuItem("Light Areas"))
                _editCCNT.Open();
            if (ImGui.MenuItem("Stage List"))
                _editCCNT.Open();
            if (ImGui.MenuItem("Effects"))
                _editCCNT.Open();
            ImGui.EndDisabled();
            ImGui.Separator();
            if (ImGui.MenuItem("Object Database"))
                _DBEditorDialog.Open();
            
            ImGui.EndMenu();
        }

#if DEBUG
        if (ImGui.BeginMenu("Debug"))
        {
            if (ImGui.MenuItem("Show demo window"))
                _showDemoWindow = true;

            ImGui.Separator();

            if (ImGui.MenuItem("Single File Choose"))
            {
                SingleFileChooserContext fileChooserContext = new(ContextHandler, WindowManager);
                WindowManager.Add(fileChooserContext);

                fileChooserContext.SuccessCallback += result => Console.WriteLine(result[0]);
            }

            ImGui.Separator();
            if (ImGui.MenuItem("Show all params"))
            {
                _fogParams.IsOpen = true;
                _lightParams.IsOpen = true;
                _miscParams.IsOpen = true;
                _switchParams.IsOpen = true;
                _camParams.IsOpen = true;
            }
            ImGui.Separator();
            ImGuiWidgets.CommandMenuItem(CommandID.AddALL, ContextHandler.ActionHandler, this);
            ImGuiWidgets.CommandMenuItem(CommandID.SaveALL, ContextHandler.ActionHandler, this);

            ImGui.EndMenu();
        }
#endif
        var c = ImGui.GetCursorPos();
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - ImGui.CalcTextSize("Help").X - ImGui.GetStyle().ItemSpacing.X * 2);
        if (ImGui.BeginMenu("Help"))
        {
            if (ImGui.MenuItem("Shortcuts"))
                _shortcutsDialog.Open();
            if (ImGui.MenuItem("About"))
            {

            }
            ImGui.EndMenu();
        }
        ImGui.SetCursorPos(c);
        #region SceneTabs
        // Opened stages are displayed in tabs in the main menu bar.

        ImGuiTabBarFlags barFlags = ImGuiTabBarFlags.AutoSelectNewTabs;

        if (ContextHandler.ProjectChanged)
        {
            CurrentScene = null;
            Scenes.Clear();
            ContextHandler.ProjectChanged = false;
        }

        if (Scenes.Count > 0 && ImGui.BeginTabBar("sceneTabs", barFlags))
        {
            for (int i = 0; i < Scenes.Count; i++)
            {
                ImGuiTabItemFlags flags = ImGuiTabItemFlags.NoPushId;

                Scene scene = Scenes[i];

                if (!scene.IsSaved)
                    flags |= ImGuiTabItemFlags.UnsavedDocument;

                bool opened = true;
                string displayName = scene.Stage!.Name + scene.Stage.Scenario;

                ImGui.PushID(displayName);

                if (ImGui.BeginTabItem(displayName, ref opened, flags) && CurrentScene != scene)
                    CurrentScene = scene;

                ImGui.EndTabItem();
                ImGui.SetItemTooltip(scene.Stage.UserPath);

                ImGui.PopID();

                // Remove the tab when it is closed.
                // This also closes the stage.
                if (!opened && Scenes.Remove(scene))
                {
                    // TO-DO: Check whether the stage is not saved.

                    i--;

                    // Set the scene to the one before if possible.
                    if (i < 0)
                        CurrentScene = null;
                    else
                        CurrentScene = Scenes[i];
                    if (Scenes.Count == 0)
                    {
                        ImGui.SetWindowFocus("Stages");
                    }
                }
            }

            ImGui.EndTabBar();
        }

        #endregion

        ImGui.EndMainMenuBar();

        return;
    }

    /// <summary>
    /// Renders the status bar which displays a message at the very bottom of the window.<br />
    /// The message displayed is controlled by <see cref="BackgroundManager"/>.
    /// </summary>
    private void RenderStatusBar(float height, Vector2 viewportSize)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0));
        ImGui.SetNextWindowPos(new(0, viewportSize.Y - height), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new(viewportSize.X, height));

        ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs;
        bool beginStatusBar = ImGui.Begin("StatusBar", flags);
        if (!beginStatusBar)
        {
            ImGui.PopStyleVar();
            return;
        }

        ImGui.SetCursorPosX(10);

        string msg = BackgroundManager.StatusMessage;
        if (!string.IsNullOrEmpty(BackgroundManager.StatusMessageSecondary))
            msg += " | " + BackgroundManager.StatusMessageSecondary;
        ImGui.Text(msg);

        if (GLTaskScheduler.TasksLeft > 0)
        {
            ImGui.SameLine();

            string glMessage = "GL tasks left: " + GLTaskScheduler.TasksLeft;
            Vector2 textSize = ImGui.CalcTextSize(glMessage);

            ImGui.SetCursorPosX(viewportSize.X - textSize.X - 10);
            ImGui.Text("GL tasks left: ");
        }

        if (_sceneWindow.MouseClickActionsCount > 0)
        {
            ImGui.SameLine();
            ImGui.Text("Click anywhere to place the object, Shift Click to keep adding the same object.");
        }

        ImGui.End();
        ImGui.PopStyleVar();
    }

    /// <summary>
    /// Renders the screen that appears when no project has been loaded.
    /// </summary>
    private void RenderWelcomeScreen(float barHeight)
    {
        ImGuiWindowFlags windowFlags =
            ImGuiWindowFlags.NoBackground
            | ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoSavedSettings;

        ImGui.SetNextWindowPos(ImGui.GetWindowViewport().GetCenter(), ImGuiCond.Always, new(0.5f, 0.5f));

        float footerSizeY = barHeight + ImGui.GetStyle().WindowPadding.Y + 20 * ScalingFactor;

        Vector2 windowSize = ImGui.GetWindowViewport().Size - new Vector2(0, barHeight * 2f + footerSizeY + 10 * ScalingFactor);
        windowSize.X = Math.Min(700 * ScalingFactor, windowSize.X);
        windowSize.Y = Math.Min(512 * ScalingFactor, windowSize.Y);

        ImGui.SetNextWindowSize(windowSize);

        if (!ImGui.Begin("##Welcome", windowFlags))
            return;

        Vector2 headerSize = windowSize with { Y = Math.Min(128 * ScalingFactor, windowSize.Y) };

        if (ImGui.BeginChild("##Header", headerSize))
        {
            ImGui.Image((nint)s_welcomeImage, new(128 * ScalingFactor));
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 20);

            if (ImGui.BeginChild("##Title"))
            {
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 25);
                ImGuiWidgets.TextHeader("Welcome to Autumn!", scale: 2.5f);
                ImGui.Text("\t\tA 3DL stage editor");
                ImGui.EndChild();
            }

            ImGui.EndChild();
        }

        Vector2 recentSize = ImGui.GetContentRegionAvail();
        recentSize.Y -= Math.Max(recentSize.Y / 8, 35);
        if (recentSize.Y < 0) recentSize.Y = 0;

        if (ImGui.BeginChild("##Recent", recentSize))
        {
            ImGui.Text("Recent projects");
            ImGui.Separator();
            ImGui.Spacing();

            const ImGuiTableFlags tableFlags =
                ImGuiTableFlags.ScrollY
                | ImGuiTableFlags.RowBg
                | ImGuiTableFlags.BordersOuter
                | ImGuiTableFlags.BordersV
                | ImGuiTableFlags.NoSavedSettings;

            Vector2 tableSize = ImGui.GetContentRegionAvail() - new Vector2(ImGui.GetStyle().ChildBorderSize);

            if (ImGui.BeginTable("##RecentsTable", 1, tableFlags, tableSize))
            {
                if (ContextHandler.SystemSettings.RecentlyOpenedPaths.Count <= 0)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.TextDisabled("There are no recent entries.");
                }
                else
                {
                    foreach (string path in ContextHandler.SystemSettings.RecentlyOpenedPaths)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);

                        if (ImGui.Selectable(path))
                        {
                            ContextHandler.OpenProject(path);
                            break;
                        }
                    }
                }

                ImGui.EndTable();
            }

            ImGui.EndChild();
        }

        if (ImGui.BeginChild("##Buttons", ImGui.GetContentRegionAvail()))
        {
            ImGui.Separator();
            ImGui.Spacing();

            Vector2 buttonSize = ImGui.GetContentRegionAvail() - ImGui.GetStyle().ItemSpacing;
            buttonSize.X /= 2;

            if (ImGui.Button(IconUtils.FOLDER + "    Open Project", buttonSize))
                ContextHandler.ActionHandler.ExecuteAction(CommandID.OpenProject, this);

            ImGui.SameLine();

            if (ImGui.Button(IconUtils.PLUS + "    New Project", buttonSize))
                ContextHandler.ActionHandler.ExecuteAction(CommandID.NewProject, this);

            ImGui.EndChild();
        }

        ImGui.End();

        float windowPosY = ImGui.GetWindowViewport().Size.Y - footerSizeY;
        ImGui.SetNextWindowPos(new(ImGui.GetWindowViewport().GetCenter().X, windowPosY), ImGuiCond.Always, new(0.5f, 0.5f));

        if (!ImGui.Begin("##WelcomeFooter", windowFlags | ImGuiWindowFlags.AlwaysAutoResize))
            return;

        ImGui.Checkbox("Always open last project when possible", ref ContextHandler.SystemSettings.OpenLastProject);

        ImGui.End();
    }
}
