using System.Numerics;
using Autumn.Background;
using Autumn.Context;
using Autumn.Enums;
using Autumn.GUI.Dialogs;
using Autumn.GUI.Editors;
using Autumn.Rendering;
using Autumn.Rendering.Gizmo;
using ImGuiNET;
using SceneGL.GLHelpers;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Autumn.GUI;

internal class MainWindowContext : WindowContext
{
    public List<Scene> Scenes { get; } = new();
    public Scene? CurrentScene { get; set; }

    public SceneGL.GLWrappers.Framebuffer SceneFramebuffer { get; }

    public BackgroundManager BackgroundManager { get; } = new();

    public GLTaskScheduler GLTaskScheduler { get; } = new();

    private bool _isFirstFrame = true;

    private readonly AddStageDialog _addStageDialog;
    private readonly ClosingDialog _closingDialog;
    private readonly NewStageObjDialog _newStageObjDialog;
    public EditChildrenDialog _editChildrenDialog;

    private readonly StageWindow _stageWindow;
    private readonly ObjectWindow _objectWindow;
    private readonly PropertiesWindow _propertiesWindow;
    private readonly SceneWindow _sceneWindow;
    private readonly WelcomeDialog _welcomeDialog;

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

        // Initialize editors:
        _stageWindow = new(this);
        _objectWindow = new(this);
        _propertiesWindow = new(this);
        _sceneWindow = new(this);
        _welcomeDialog = new(this);

        Window.Title = "Autumn: Stage Editor";

        SceneFramebuffer = new(
            initialSize: null,
            depthAttachment: SceneGL.PixelFormat.D24_UNorm_S8_UInt,
            SceneGL.PixelFormat.R8_G8_B8_A8_UNorm, // Regular color.
            SceneGL.PixelFormat.R32_UInt // Used for object selection.
        );

        Window.Load += () =>
        {
            InfiniteGrid.Initialize(GL!);
            ModelRenderer.Initialize(GL!);

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

            ImGuiIOPtr io = ImGui.GetIO();

            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            io.ConfigWindowsMoveFromTitleBarOnly = true;
        };

        Window.Render += (deltaSeconds) =>
        {
            if (ImGuiController is null)
                return;

            ImGuiController.MakeCurrent();

            if (_isFirstFrame)
            {
                // Fix docking settings not loading properly:
                ImGui.LoadIniSettingsFromDisk(ImguiSettingsFile);

                if (!ContextHandler.SystemSettings.SkipWelcomeDialog)
                    _welcomeDialog.Open();

                _isFirstFrame = false;
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
                RenderNoProjectScreen();
            else
            {
                _stageWindow.Render();
                _objectWindow.Render();
                _propertiesWindow.Render();
                _sceneWindow.Render(deltaSeconds);
            }

#if DEBUG
            if (_showDemoWindow)
                ImGui.ShowDemoWindow(ref _showDemoWindow);
#endif

            _addStageDialog.Render();
            _closingDialog.Render();
            _newStageObjDialog.Render();
            _editChildrenDialog?.Render();
            _welcomeDialog.Render();

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

    public void AddSceneMouseClickAction(Action<MainWindowContext, Vector4> action) =>
        _sceneWindow.AddMouseClickAction(action);

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

            ImGuiWidgets.CommandMenuItem(CommandID.Exit, ContextHandler.ActionHandler, this);

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Edit"))
        {
            ImGuiWidgets.CommandMenuItem(CommandID.Undo, ContextHandler.ActionHandler, this);
            ImGuiWidgets.CommandMenuItem(CommandID.Redo, ContextHandler.ActionHandler, this);

            ImGui.Separator();

            if (ImGui.MenuItem("Add object", CurrentScene is not null))
                _newStageObjDialog.Open();

            ImGui.EndMenu();
        }

#if DEBUG
        if (ImGui.BeginMenu("Debug"))
        {
            if (ImGui.MenuItem("Show demo window"))
                _showDemoWindow = true;

            ImGui.EndMenu();
        }
#endif

        #region SceneTabs
        // Opened stages are displayed in tabs in the main menu bar.

        ImGuiTabBarFlags barFlags = ImGuiTabBarFlags.AutoSelectNewTabs;

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
            ImGuiWindowFlags.NoSavedSettings
            | ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.NoInputs;

        if (!ImGui.Begin("StatusBar", flags))
            return;

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

        ImGui.End();
        ImGui.PopStyleVar();
    }

    /// <summary>
    /// Renders the screen that appears when no project has been loaded.
    /// </summary>
    private static void RenderNoProjectScreen()
    {
        ImGuiWindowFlags flags =
            ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoBackground
            | ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.NoInputs
            | ImGuiWindowFlags.NoSavedSettings;

        ImGui.SetNextWindowPos(
            ImGui.GetWindowViewport().GetCenter(),
            ImGuiCond.Always,
            new(0.5f, 0.5f)
        );

        if (!ImGui.Begin("##", flags))
            return;

        ImGui.TextDisabled("Please open a project from the menu or drop a project directory here.");

        ImGui.End();
    }
}
