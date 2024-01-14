using Autumn.Background;
using Autumn.Commands;
using Autumn.GUI.Editors;
using Autumn.IO;
using Autumn.Scene;
using Autumn.Scene.Gizmo;
using Autumn.Storage;
using ImGuiNET;
using SceneGL.GLHelpers;
using Silk.NET.OpenGL;
using System.Numerics;

namespace Autumn.GUI;

internal class MainWindowContext : WindowContext
{
    public List<Scene.Scene> Scenes { get; } = new();
    public Scene.Scene? CurrentScene { get; set; }

    public SceneGL.GLWrappers.Framebuffer SceneFramebuffer { get; }

    public BackgroundManager BackgroundManager { get; } = new();

    private bool _isFirstFrame = true;

    private bool _closingDialogOpened = false;

    private bool _stageSelectOpened = false;
    private string _stageSearchInput = string.Empty;

    private bool _newStageOpened = false;
    private string _newStageName = string.Empty;
    private string _newStageScenario = "1";

    private bool _projectPropertiesOpened = false;
    private string _projectPropertiesName = string.Empty;
    private string _projectPropertiesBuildPath = string.Empty;
    private bool _projectPropertiesBuildPathValid = false;
    private bool _projectPropertiesUseClassNames = false;

#if DEBUG
    private bool _showDemoWindow = false;
#endif

    public MainWindowContext()
        : base()
    {
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

            // Fix docking settings not loading properly:
            if (_isFirstFrame)
            {
                ImGui.LoadIniSettingsFromDisk(ImguiSettingsFile);
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

            if (!ProjectHandler.ProjectLoaded)
                RenderNoProjectScreen();
            else
                RenderEditors(deltaSeconds);

#if DEBUG
            if (_showDemoWindow)
                ImGui.ShowDemoWindow(ref _showDemoWindow);
#endif

            #region Dialogs and popups
            // These dialogs are only rendered when their corresponding variables are set to true.

            if (_stageSelectOpened)
                RenderStageSelectPopup();

            if (_newStageOpened)
                RenderNewStagePopup();

            if (_closingDialogOpened)
                RenderClosingDialog();

            if (_projectPropertiesOpened)
                RenderProjectPropertiesDialog();

            #endregion

            GL!.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL!.Clear(ClearBufferMask.ColorBufferBit);
            GL!.Viewport(Window.FramebufferSize);
            ImGuiController.Render();
        };
    }

    public override bool Close()
    {
        if (BackgroundManager.IsBusy)
        {
            _closingDialogOpened = true;
            return false;
        }

        // The window is only closed if all tasks have finished.
        return true;
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
            if (ImGuiWidgets.CommandMenuItem(CommandID.NewProject) && ProjectHandler.ProjectLoaded)
                _projectPropertiesOpened = true;

            ImGuiWidgets.CommandMenuItem(CommandID.OpenProject);

            // Menu that displays the recently opened projects' list.
            if (ImGui.BeginMenu("Recent"))
            {
                if (RecentHandler.RecentOpenedPaths.Count <= 0)
                    ImGui.TextDisabled("There are no recent entries.");
                else
                {
                    foreach (string path in RecentHandler.RecentOpenedPaths)
                        if (ImGui.Selectable(path))
                        {
                            ProjectHandler.LoadProject(path);
                            break;
                        }
                }

                ImGui.EndMenu();
            }

            ImGui.Separator();

            if (ImGui.MenuItem("Exit"))
                Window.Close();

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Stage"))
        {
            if (ImGui.MenuItem("Create New", ProjectHandler.ProjectLoaded))
                _newStageOpened = true;

            if (ImGui.MenuItem("Save", CurrentScene is not null))
                BackgroundManager.Add(
                    $"Saving stage \"{CurrentScene!.Stage.Name + CurrentScene!.Stage.Scenario}\"...",
                    () => StageHandler.SaveProjectStage(CurrentScene!.Stage)
                );

            if (ImGui.MenuItem("Import from RomFS", ProjectHandler.ProjectLoaded))
                _stageSelectOpened |= true;

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Help"))
        {
            if (ImGui.MenuItem("Show welcome window"))
                WindowManager.Add(new WelcomeWindowContext());

#if DEBUG
            if (ImGui.MenuItem("Show demo window"))
                _showDemoWindow = true;
#endif

            ImGui.EndMenu();
        }

        #region SceneTabs
        // Opened stages are displayed in tabs in the main menu bar.

        ImGuiTabBarFlags barFlags = ImGuiTabBarFlags.AutoSelectNewTabs;
        int sceneCount = Scenes.Count;

        if (sceneCount > 0 && ImGui.BeginTabBar("sceneTabs", barFlags))
        {
            for (int i = 0; i < sceneCount; i++)
            {
                ImGuiTabItemFlags flags = ImGuiTabItemFlags.NoPushId;

                Scene.Scene scene = Scenes[i];

                if (!scene.Stage.Saved)
                    flags |= ImGuiTabItemFlags.UnsavedDocument;

                bool opened = true;
                string displayName = scene.Stage.Name + scene.Stage.Scenario;

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
                    sceneCount = Scenes.Count;

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

        string msg = BackgroundManager.StatusMessage;
        if (BackgroundManager.StatusMessageSecondary != string.Empty)
            msg += " | " + BackgroundManager.StatusMessageSecondary;
        ImGui.Text(msg);

        ImGui.End();
        ImGui.PopStyleVar();
    }

    private void RenderEditors(double deltaSeconds)
    {
        StageWindow.Render(this);
        ObjectWindow.Render(this);
        PropertiesWindow.Render(this);
        SceneWindow.Render(this, deltaSeconds);
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

        ImGui.TextDisabled("Please, open a project from the menu or drop a folder here.");

        ImGui.End();
    }

    /// <summary>
    /// Renders a dialog that allows to the user to open a stage from the RomFS.
    /// </summary>
    private void RenderStageSelectPopup()
    {
        if (!RomFSHandler.RomFSAvailable)
        {
            _stageSelectOpened = false;
            return;
        }

        ImGui.OpenPopup("Stage selector");

        Vector2 dimensions = new Vector2(450, 230) + ImGui.GetStyle().ItemSpacing;
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Appearing,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Stage selector",
                ref _stageSelectOpened,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        # region Search box

        ImGui.Text("Search:");
        ImGui.SameLine();

        ImGui.SetNextItemWidth(450 - ImGui.GetCursorPosX());

        ImGui.InputTextWithHint(
            label: "",
            hint: "Insert the name of the stage here.",
            ref _stageSearchInput,
            maxLength: 128
        );

        #endregion

        ImGui.SetNextItemWidth(450 - ImGui.GetCursorPosX());

        if (ImGui.BeginListBox(""))
        {
            foreach (var (name, scenario) in RomFSHandler.StageNames)
            {
                if (
                    !string.IsNullOrEmpty(_stageSearchInput)
                    && !name.Contains(
                        _stageSearchInput,
                        StringComparison.InvariantCultureIgnoreCase
                    )
                )
                    continue;

                // Does not show the stage if there is an already opened one with the same name and scenario:
                if (
                    Scenes.Find(
                        scene => scene.Stage.Name == name && scene.Stage.Scenario == scenario
                    )
                    is not null
                )
                    continue;

                if (ImGui.Selectable(name + scenario, false, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    _stageSelectOpened = false;

                    BackgroundManager.Add(
                        $"Importing stage \"{name + scenario}\" from RomFS...",
                        () =>
                        {
                            if (!StageHandler.TryImportStage(name, scenario, out Stage stage))
                            {
                                ImGui.CloseCurrentPopup();
                                ImGui.EndPopup();
                                return;
                            }

                            ProjectHandler.ActiveProject.Stages.Add(stage);
                        }
                    );

                    ImGui.CloseCurrentPopup();
                }
            }
        }

        ImGui.EndPopup();
    }

    /// <summary>
    /// Renders a dialog that asks the user for a name and a scenario for the new stage.
    /// </summary>
    private void RenderNewStagePopup()
    {
        ImGui.OpenPopup("Create new stage");

        Vector2 dimensions = new Vector2(400, 95) + ImGui.GetStyle().ItemSpacing;
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Appearing,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Create new stage",
                ref _newStageOpened,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        ImGui.SetNextItemWidth(350);
        ImGui.InputTextWithHint("##name", "Name", ref _newStageName, 100);

        ImGui.SameLine();
        ImGui.SetNextItemWidth(30);
        ImGui.InputText("##scenario", ref _newStageScenario, 2, ImGuiInputTextFlags.CharsDecimal);

        ImGui.Spacing();

        float windowWidth = ImGui.GetWindowWidth();
        ImGui.SetCursorPosX(windowWidth / 2 - 25);

        if (ImGui.Button("Create", new(50, 0)))
        {
            byte scenario = byte.Parse(_newStageScenario);
            Stage stage = StageHandler.CreateNewStage(_newStageName, scenario);

            ProjectHandler.ActiveProject.Stages.Add(stage);

            ImGui.CloseCurrentPopup();
            _newStageOpened = false;
        }
    }

    /// <summary>
    /// Renders a dialog that prompts the user to wait for the background tasks to finish.
    /// This dialog is only rendered when the window is waiting to close.
    /// </summary>
    private void RenderClosingDialog()
    {
        ImGui.OpenPopup("##ClosingDialog");

        Vector2 dimensions = new Vector2(450, 185) + ImGui.GetStyle().ItemSpacing;
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Appearing,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "##ClosingDialog",
                ref _closingDialogOpened,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        ImGui.TextWrapped(
            "Please wait for the following tasks to finish before exiting the program:"
        );

        ImGui.Spacing();

        foreach (var (message, _) in BackgroundManager.GetRemainingTasks())
        {
            if (message is null)
                ImGui.TextDisabled("BackgroundTask");
            else
                ImGui.Text(message);
        }

        if (!BackgroundManager.IsBusy)
            Window.Close();

        ImGui.EndPopup();
    }

    private void RenderProjectPropertiesDialog()
    {
        ImGui.OpenPopup("Project properties");

        Vector2 dimensions = new Vector2(480, 185) + ImGui.GetStyle().ItemSpacing;
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Appearing,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Project properties",
                ref _projectPropertiesOpened,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        ImGui.TextWrapped("Project: " + ProjectHandler.ActiveProject.SavePath);

        ImGui.Separator();
        ImGui.Spacing();

        // Correctly aline first field:
        float size1 = ImGui.CalcTextSize("Name").X;
        float size2 = ImGui.CalcTextSize("Build path").X;
        float cursorX = ImGui.GetCursorPosX();

        ImGui.SetCursorPosX(cursorX + size2 - size1);

        ImGui.Text("Name: ");
        ImGui.SameLine();
        ImGui.InputText("", ref _projectPropertiesName, 255);

        ImGui.Text("Build path: ");
        ImGui.SameLine();
        ImGuiWidgets.DirectoryPathSelector(
            ref _projectPropertiesBuildPath,
            ref _projectPropertiesBuildPathValid,
            dialogTitle: "Please specify the project's build path"
        );

        ImGui.SameLine();
        Vector2 cursorPos = ImGui.GetCursorPos();
        ImGui.TextDisabled("?");
        ImGui.SetCursorPos(cursorPos);
        ImGui.InvisibleButton("helpButton", new Vector2(20, 20));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "The build path is where the mod files will be saved to.\n"
                    + "If you use an emulator, we recommend you to set this to the emulator's mod directory."
            );

        ImGui.Checkbox("Use class name patch", ref _projectPropertiesUseClassNames);
        ImGui.SameLine();
        cursorPos = ImGui.GetCursorPos();
        ImGui.TextDisabled("?");
        ImGui.SetCursorPos(cursorPos);
        ImGui.InvisibleButton("helpButton", new Vector2(20, 20));

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                "If enabled, the project will replace use of the CreatorClassNameTable with ObjectName/ClassName values individual to objects.\n"
                + "Useful for convenience, but requires an ExeFS patch."
            );

        float okButtonY = ImGui.GetWindowHeight() - 40;
        float okButtonX = ImGui.GetWindowWidth() - 60;

        ImGui.SetCursorPos(new Vector2(okButtonX, okButtonY));

        if (ImGui.Button("Ok", new(40, 0)))
        {
            ImGui.CloseCurrentPopup();
            _projectPropertiesOpened = false;

            ProjectHandler.ActiveProject.Name = _projectPropertiesName;
            ProjectHandler.ActiveProject.UseClassNames = _projectPropertiesUseClassNames;

            if (_projectPropertiesBuildPathValid)
                ProjectHandler.ActiveProject.BuildOutput = _projectPropertiesBuildPath;

            ProjectHandler.SaveProject();
        }

        ImGui.EndPopup();
    }
}
