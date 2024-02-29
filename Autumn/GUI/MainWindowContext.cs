using System.Numerics;
using Autumn.Background;
using Autumn.Commands;
using Autumn.GUI.Dialogs;
using Autumn.GUI.Editors;
using Autumn.IO;
using Autumn.Scene;
using Autumn.Scene.Gizmo;
using Autumn.Storage;
using Autumn.Utils;
using ImGuiNET;
using SceneGL.GLHelpers;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Autumn.GUI;

internal class MainWindowContext : WindowContext
{
    public List<Scene.Scene> Scenes { get; } = new();
    public Scene.Scene? CurrentScene { get; set; }

    public SceneGL.GLWrappers.Framebuffer SceneFramebuffer { get; }

    public ChangeHandler ChangeHandler { get; } = new();

    private bool _isFirstFrame = true;

    private AddStageDialog _addStageDialog;
    private ClosingDialog _closingDialog;
    private ProjectPropertiesDialog _projectPropertiesDialog;

    private bool _newObjectOpened = false;
    private string _newObjectName = "";
    private string _newObjectClass = "";
    private string _newObjectClassSearchQuery = "";
    private bool _newObjectPrevClassValid = false;
    private int[] _newObjectArgs = new int[8] { -1, -1, -1, -1, -1, -1, -1, -1 };
    private int _newObjectStageObjObjectType = 0;
    private string[] _newObjectStageObjObjectTypeNames = new string[]
    {
        "Obj",
        "Goal",
        "Start",
        "StartEvent",
        "DemoScene"
    };
    private const ImGuiTableFlags _newObjectClassTableFlags =
        ImGuiTableFlags.ScrollY
        | ImGuiTableFlags.RowBg
        | ImGuiTableFlags.BordersOuter
        | ImGuiTableFlags.BordersV
        | ImGuiTableFlags.Resizable;

#if DEBUG
    private bool _showDemoWindow = false;
#endif

    public MainWindowContext()
        : base()
    {
        // Initialize dialogs:
        _addStageDialog = new(this);
        _closingDialog = new(this);
        _projectPropertiesDialog = new(this);

        ProjectHandler.ProjectCreatedEvent += _projectPropertiesDialog.Open;

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

            # region Dialogs and popups
            // These dialogs are only rendered when their corresponding variables are set to true.

            _addStageDialog.Render();
            _closingDialog.Render();
            _projectPropertiesDialog.Render();

            if (_newObjectOpened)
                RenderNewObjectPopup();

            # endregion

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
            ProjectHandler.LoadProject(path);
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
            ImGuiWidgets.CommandMenuItem(CommandID.NewProject);
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

                    ImGui.Separator();

                    if (ImGui.Selectable("Clear all"))
                        RecentHandler.RecentOpenedPaths.Clear();
                }

                ImGui.EndMenu();
            }

            ImGui.Separator();

            if (ImGui.MenuItem("Add Stage", ProjectHandler.ProjectLoaded))
                _addStageDialog.Open();

            if (ImGui.MenuItem("Save Stage", CurrentScene is not null))
                BackgroundManager.Add(
                    $"Saving stage \"{CurrentScene!.Stage.Name + CurrentScene!.Stage.Scenario}\"...",
                    manager => StageHandler.SaveProjectStage(CurrentScene!.Stage),
                    BackgroundTaskPriority.High
                );

            ImGui.Separator();

            if (ImGui.MenuItem("Exit"))
                Window.Close();

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Edit"))
        {
            if (ImGui.MenuItem("Undo", CurrentScene is not null && ChangeHandler.History.CanUndo))
                ChangeHandler.History.Undo();

            if (ImGui.MenuItem("Redo", CurrentScene is not null && ChangeHandler.History.CanRedo))
                ChangeHandler.History.Redo();

            ImGui.Separator();

            if (ImGui.MenuItem("Add object", CurrentScene is not null))
                _newObjectOpened = true;

            if (ImGui.MenuItem("Project properties", ProjectHandler.ProjectLoaded))
                _projectPropertiesDialog.Open();

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

        ImGui.TextDisabled("Please open a project from the menu or drop a project file here.");

        ImGui.End();
    }

    private void AddQueuedObject(Vector4 trans)
    {
        StageObjType type;
        switch (_newObjectStageObjObjectType)
        {
            case 0:
                type = StageObjType.Regular;
                break;
            case 1:
                type = StageObjType.Goal;
                break;
            case 2:
                type = StageObjType.Start;
                break;
            case 3:
                type = StageObjType.StartEvent;
                break;
            case 4:
                type = StageObjType.DemoScene;
                break;
            default:
                type = StageObjType.Regular;
                break;
        }

        StageObj newObj =
            new()
            {
                Type = type,
                Name = _newObjectName,
                ClassName = _newObjectClass,
                Translation = new(trans.X * 100, trans.Y * 100, trans.Z * 100)
            };
        for (int i = 0; i < 8; i++)
            newObj.Properties.Add($"Arg{i}", _newObjectArgs[i]);

        CurrentScene?.Stage.StageData?.Add(newObj);
        CurrentScene?.GenerateSceneObject(newObj);

        if (Keyboard?.IsKeyPressed(Key.ShiftLeft) ?? false)
            SceneWindow.AddMouseClickAction(new Action<Vector4>(AddQueuedObject));
    }

    private void ResetNewObjectArgs()
    {
        for (int i = 0; i < 8; i++)
            _newObjectArgs[i] = -1;
    }

    /// <summary>
    /// Renders a dialog that has options for creating an Object
    /// </summary>
    private void RenderNewObjectPopup()
    {
        ImGui.OpenPopup("Add new object");

        Vector2 dimensions = new(800, 0);
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.Always);

        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Appearing,
            new(0.5f, 0.5f)
        );

        if (
            !ImGui.BeginPopupModal(
                "Add new object",
                ref _newObjectOpened,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings
            )
        )
            return;

        if (ImGui.BeginTabBar("ObjectType"))
        {
            if (ImGui.BeginTabItem("Object"))
            {
                bool databaseHasEntry = ClassDatabaseHandler.DatabaseEntries.TryGetValue(
                    _newObjectClass,
                    out ClassDatabaseHandler.DatabaseEntry dbEntry
                );

                ImGui.SetNextItemWidth(400);
                ImGui.InputText("Search", ref _newObjectClassSearchQuery, 128);
                if (
                    ImGui.BeginTable(
                        "ClassTable",
                        2,
                        _newObjectClassTableFlags,
                        new Vector2(400, 200)
                    )
                )
                {
                    ImGui.TableSetupColumn("ClassName", ImGuiTableColumnFlags.None, 0.5f);
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, 0.5f);
                    ImGui.TableHeadersRow();

                    foreach (var pair in ClassDatabaseHandler.DatabaseEntries)
                    {
                        if (
                            _newObjectClassSearchQuery != string.Empty
                            && !pair.Key.ToLower().Contains(_newObjectClassSearchQuery.ToLower())
                            && !pair.Value.Name.Contains(_newObjectClassSearchQuery.ToLower())
                        )
                            continue;
                        ImGui.TableNextRow();

                        ImGui.TableSetColumnIndex(0);
                        if (ImGui.Selectable(pair.Key))
                        {
                            _newObjectClass = pair.Key;
                            databaseHasEntry = false;
                            ResetNewObjectArgs();
                        }
                        ImGui.TableSetColumnIndex(1);
                        ImGui.Text(pair.Value.Name);
                    }

                    ImGui.EndTable();
                }
                ImGui.SameLine();

                {
                    ImGui.BeginChild("##Desc_Args", new Vector2(380, 210));
                    string description = dbEntry.Description ?? "No Description";
                    if (dbEntry.DescriptionAdditional is not null)
                        description += $"\n{dbEntry.DescriptionAdditional}";
                    ImGui.SetWindowFontScale(1.3f);
                    if (databaseHasEntry)
                        ImGui.Text(dbEntry.Name == " " ? _newObjectClass : dbEntry.Name);
                    else
                        ImGui.Text(_newObjectClass);
                    ImGui.SetWindowFontScale(1.0f);
                    ImGui.SameLine();
                    ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), _newObjectClass);

                    ImGui.BeginChild(
                        "##Description",
                        new Vector2(380, 40),
                        ImGuiChildFlags.None,
                        ImGuiWindowFlags.AlwaysVerticalScrollbar
                    );
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    ImGui.TextWrapped(description);
                    ImGui.EndChild();
                    if (
                        ImGui.BeginTable(
                            "ArgTable",
                            4,
                            _newObjectClassTableFlags,
                            new Vector2(380, 130)
                        )
                    )
                    {
                        ImGui.TableSetupColumn("Arg", ImGuiTableColumnFlags.None, 0.2f);
                        ImGui.TableSetupColumn("Val", ImGuiTableColumnFlags.None, 0.35f);
                        ImGui.TableSetupColumn("Name");
                        ImGui.TableSetupColumn("Desc");
                        ImGui.TableHeadersRow();

                        for (int i = 0; i < 8; i++)
                        {
                            string arg = $"Arg{i}";
                            string name = "";
                            string argDescription = "";
                            if (
                                databaseHasEntry
                                && dbEntry.Args is not null
                                && dbEntry.Args.TryGetValue(arg, out var argData)
                            )
                            {
                                if (argData.Name is not null)
                                    name = argData.Name;
                                if (argData.Description is not null)
                                    argDescription = argData.Description;
                                if (!_newObjectPrevClassValid)
                                    _newObjectArgs[i] = (int)argData.Default;
                            }

                            ImGui.TableNextRow();

                            ImGui.TableSetColumnIndex(0);
                            ImGui.Text(arg);
                            ImGui.TableSetColumnIndex(1);
                            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                            ImGui.DragInt($"##{arg}", ref _newObjectArgs[i]);
                            ImGui.TableSetColumnIndex(2);
                            ImGui.Text(name);
                            ImGui.TableSetColumnIndex(3);
                            bool needScrollbar =
                                ImGui.CalcTextSize(argDescription).X
                                > ImGui.GetContentRegionAvail().X;
                            float ysize =
                                ImGui.GetFont().FontSize
                                * (ImGui.GetFont().Scale * (needScrollbar ? 1.8f : 1.0f));
                            ImGui.BeginChild(
                                $"##ArgDescription{i}",
                                new Vector2(0, ysize),
                                ImGuiChildFlags.None,
                                ImGuiWindowFlags.HorizontalScrollbar
                            );
                            ImGui.Text(argDescription);
                            ImGui.EndChild();
                        }

                        ImGui.EndTable();
                    }

                    ImGui.EndChild();
                }

                float width = ImGui.GetContentRegionAvail().X;
                float spacingX = ImGui.GetStyle().ItemSpacing.X;
                float paddingX = ImGui.GetStyle().FramePadding.X;
                ImGui.PushItemWidth(width * 0.5f);
                ImGui.Text("ObjectName");
                ImGui.SameLine();
                ImGui.SetCursorPosX(width * 0.5f);
                ImGui.Text("ClassName");
                ImGui.PopItemWidth();

                float buttonTextSizeX = ImGui.CalcTextSize("<-").X;
                float objectNameWidth =
                    width * 0.5f - (paddingX * 2 + spacingX * 2 + buttonTextSizeX);
                ImGui.PushItemWidth(objectNameWidth);
                ImGuiWidgets.InputTextRedWhenEmpty("##ObjectName", ref _newObjectName, 128);
                ImGui.PopItemWidth();
                ImGui.SameLine();
                if (ImGui.Button("<-"))
                    _newObjectName = _newObjectClass;
                ImGui.SameLine();
                ImGui.PushItemWidth(width * 0.5f);
                if (ImGuiWidgets.InputTextRedWhenEmpty("##ClassName", ref _newObjectClass, 128))
                    ResetNewObjectArgs();
                ImGui.PopItemWidth();

                ImGui.SetNextItemWidth(100);
                ImGui.Combo(
                    "Object Type",
                    ref _newObjectStageObjObjectType,
                    _newObjectStageObjObjectTypeNames,
                    _newObjectStageObjObjectTypeNames.Length
                );

                _newObjectPrevClassValid = databaseHasEntry;
                bool canCreate = _newObjectName != string.Empty && _newObjectClass != string.Empty;
                if (canCreate)
                    ImGui.SameLine();
                if (canCreate && ImGui.Button("Add"))
                {
                    SceneWindow.AddMouseClickAction(new Action<Vector4>(AddQueuedObject));

                    _newObjectOpened = false;
                }

                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Area"))
            {
                ImGui.Text("Currently unsupported");
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Rail"))
            {
                ImGui.Text("Currently unsupported");
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }
}
