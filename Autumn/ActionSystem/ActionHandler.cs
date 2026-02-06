using Autumn.Enums;
using Autumn.GUI;
using Autumn.GUI.Windows;
using Autumn.History;
using Autumn.Rendering;
using Autumn.Rendering.Storage;
using Autumn.Rendering.CtrH3D;
using Autumn.Storage;
using Silk.NET.SDL;
using TinyFileDialogsSharp;
using ImGuiNET;

namespace Autumn.ActionSystem;

internal class ActionHandler
{
    private readonly Dictionary<CommandID, (Command Command, Shortcut? Shortcut)> _actions = new();

    /// <summary>
    /// Create a new instance of ActionHandler.
    /// </summary>
    /// <param name="actions">The actions read from the settings</param>
    public ActionHandler(Dictionary<CommandID, Shortcut> actions)
    {
        foreach (CommandID id in Enum.GetValues<CommandID>())
        {
            Command? command = id switch
            {
                CommandID.NewProject => NewProject(),
                CommandID.OpenProject => OpenProject(),
                CommandID.OpenSettings => OpenSettings(),
                CommandID.CloseScene => CloseScene(),
                CommandID.Exit => Exit(),
                CommandID.AddStage => AddStage(),
                CommandID.SaveStage => SaveStage(),
                CommandID.AddObject => AddObj(),
                CommandID.RemoveObj => RemoveObj(),
                CommandID.DuplicateObj => DuplicateObj(),
                CommandID.HideObj => HideObj(),
                CommandID.Undo => Undo(),
                CommandID.Redo => Redo(),
                CommandID.GotoParent => GotoParent(),
                CommandID.UnselectAll => UnselectAll(),
                #if DEBUG
                CommandID.AddALL => AddAllStages(),
                CommandID.SaveALL => SaveAllStages(),
                #endif
                _ => null
            };

            if (command is null)
                continue;

            actions.TryGetValue(id, out Shortcut? shortcut);

            _actions.Add(id, (command, shortcut));
        }
    }


    public void ExecuteShortcuts(WindowContext? focusedWindow)
    {
        foreach (var (command, shortcut) in _actions.Values)
        {
            if ((shortcut?.IsTriggered() ?? false) && command.Enabled(focusedWindow))
                command.Action.Invoke(focusedWindow);
        }
    }

    public void ExecuteAction(CommandID commandID, WindowContext? focusedWindow)
    {
        _actions[commandID].Command.Action.Invoke(focusedWindow);
    }

    public bool SetShortcut(CommandID commandID, Shortcut shortcut)
    {
        if (!_actions.TryGetValue(commandID, out var action))
            return false;

        _actions[commandID] = (action.Command, shortcut);
        return true;
    }

    public (Command? Command, Shortcut? Shortcut) GetAction(CommandID commandID)
    {
        if (!_actions.TryGetValue(commandID, out var value))
            return (null, null);

        return value;
    }

    public IEnumerator<(Command Command, Shortcut? Shortcut)> EnumerateActions()
    {
        foreach (var value in _actions.Values)
            yield return value;
    }

    #region Command definition

    private static Command NewProject() =>
        new(
            displayName: "New Project",
            action: window =>
            {
                if (!window!.ContextHandler.SystemSettings.RestoreNativeFileDialogs)
                {
                    ProjectCreateChooserContext projectChooser = new(window.ContextHandler, window.WindowManager);
                    window.WindowManager.Add(projectChooser);

                    projectChooser.SuccessCallback += result =>
                    {
                        window.ContextHandler.SystemSettings.LastProjectOpenPath = result[0];
                        window.ContextHandler.NewProject(result[0]);
                    };

                    return;
                }

                bool isEmpty = false;
                string? output;

                do
                {
                    bool success = TinyFileDialogs.SelectFolderDialog(
                        out output,
                        title: "Select where to save the new Autumn project",
                        defaultPath: window?.ContextHandler.SystemSettings.LastProjectOpenPath
                    );

                    if (!success)
                        return; // Process cancelled by the user.

                    if (!Directory.Exists(output))
                    {
                        TinyFileDialogs.MessageBox(
                            message: "The directory does not exist.",
                            dialogType: DialogType.Ok,
                            iconType: MessageIconType.Error
                        );

                        continue;
                    }

                    isEmpty = !Directory.EnumerateFileSystemEntries(output!).Any();

                    if (!isEmpty)
                    {
                        TinyFileDialogs.MessageBox(
                            message: "The directory for the new project must be empty.",
                            dialogType: DialogType.Ok,
                            iconType: MessageIconType.Info
                        );
                    }
                } while (!isEmpty);

                window!.ContextHandler.SystemSettings.LastProjectOpenPath = output!;
                window.ContextHandler.NewProject(output!);
            },
            enabled: window => window is not null
        );

    private static Command OpenProject() =>
        new(
            displayName: "Open Project",
            action: window =>
            {
                if (!window!.ContextHandler.SystemSettings.RestoreNativeFileDialogs)
                {
                    ProjectChooserContext projectChooser = new(window.ContextHandler, window.WindowManager);
                    projectChooser.Title = "Autumn: Open Project";
                    window.WindowManager.Add(projectChooser);

                    projectChooser.SuccessCallback += result =>
                    {
                        window.ContextHandler.SystemSettings.LastProjectOpenPath = result[0];
                        window.ContextHandler.OpenProject(result[0]);
                    };

                    ImGui.SetWindowFocus("Stages");
                    return;
                }

                string? output;

                do
                {
                    bool success = TinyFileDialogs.SelectFolderDialog(
                        out output,
                        title: "Select the Autumn project directory",
                        defaultPath: window?.ContextHandler.SystemSettings.LastProjectOpenPath
                    );

                    if (!success)
                        return;

                    if (Directory.Exists(output))
                        break;

                    TinyFileDialogs.MessageBox(
                        message: "The directory does not exist.",
                        dialogType: DialogType.Ok,
                        iconType: MessageIconType.Error
                    );
                } while (true);

                window!.ContextHandler.SystemSettings.LastProjectOpenPath = output;
                window.ContextHandler.OpenProject(output);
                ImGui.SetWindowFocus("Stages");
            },
            enabled: window => true
        );

    private static Command Exit() =>
        new(
            displayName: "Exit",
            action: window =>
            {
                if (window is null)
                    return;

                window.WindowManager.Stop();
            },
            enabled: window => true
        );

    private static Command AddStage() =>
        new(
            displayName: "Add Stage",
            action: window =>
            {
                if (window is not MainWindowContext mainWindow)
                    return;

                mainWindow.OpenAddStageDialog();
            },
            enabled: window => window is MainWindowContext && window.ContextHandler.IsProjectLoaded
        );
    public static Command AddAllStages() =>
        new(
            displayName: "Open All Project Stages",
            action: window =>
            {
                if (window is not MainWindowContext mainWindow)
                    return;
                foreach (var st in mainWindow.ContextHandler.ProjectStages)
                {
                    mainWindow.BackgroundManager.Add(
                        $"Importing stage \"{st.Name + st.Scenario}\" from RomFS...",
                        manager =>
                        {
                            Stage stage = mainWindow.ContextHandler.FSHandler.ReadStage(st.Name, st.Scenario);
                            Scene scene =
                                new(
                                    stage,
                                    mainWindow.ContextHandler.FSHandler,
                                    mainWindow.GLTaskScheduler,
                                    ref manager.StatusMessageSecondary
                                );

                            mainWindow.Scenes.Add(scene);
                            
                        }
                    );
                }
            },
            enabled: window => window is MainWindowContext && window.ContextHandler.IsProjectLoaded
        );
    public static Command SaveAllStages() =>
        new(
            displayName: "Save Open Stages",
            action: window =>
            {
                if (window is not MainWindowContext mainWindow)
                    return;
                foreach (Scene sc in mainWindow.Scenes)
                {

                mainWindow.BackgroundManager.Add(
                    "Saving...",
                    manager =>
                    {
                        Scene scene = sc;
                        Stage stage = sc.Stage!;
                        window.ContextHandler.FSHandler.WriteStage(stage, window.ContextHandler.Settings.UseClassNames);
                        scene.IsSaved = true;
                        scene.SaveUndoCount = sc.History.UndoSteps;
                    }
                );
                } 
            },
            enabled: window => window is MainWindowContext && window.ContextHandler.IsProjectLoaded
        );

    private static Command OpenSettings() =>
        new(
            displayName: "Settings",
            action: window =>
            {
                if (window is not MainWindowContext mainWindow)
                    return;

                mainWindow.OpenSettingsDialog();
            },
            enabled: window => window is MainWindowContext && window.ContextHandler.IsProjectLoaded
        );
    private static Command CloseScene() =>
        new(
            displayName: "Close Current Scene",
            action: window =>
            {
                if (window is not MainWindowContext mainWindow)
                    return;

                mainWindow.CloseCurrentScene();
            },
            enabled: window => window is MainWindowContext mainContext && mainContext.CurrentScene is not null
        );

    private static Command SaveStage() =>
        new(
            displayName: "Save Stage",
            action: window =>
            {
                if (window is not MainWindowContext mainContext)
                    return;

                mainContext.BackgroundManager.Add(
                    "Saving...",
                    manager =>
                    {
                        Scene scene = mainContext.CurrentScene!;
                        Stage stage = mainContext.CurrentScene!.Stage!;
                        window.ContextHandler.FSHandler.WriteStage(stage, window.ContextHandler.Settings.UseClassNames);
                        scene.IsSaved = true;
                        scene.SaveUndoCount = mainContext.CurrentScene.History.UndoSteps;
                        if (!window.ContextHandler.ProjectStages.Contains(new(stage.Name, stage.Scenario)))
                        {
                            window.ContextHandler.AddProjectStage(stage.Name, stage.Scenario);
                        }
                    }
                );
            },
            enabled: window => window is MainWindowContext mainContext && mainContext.CurrentScene is not null
        );

    private static Command AddObj() =>
        new(
            displayName: "Add Object",
            action: window =>
            {
                if (window is not MainWindowContext mainContext)
                    return;

                mainContext.OpenAddObjectDialog();
            },
            enabled: window => window is MainWindowContext mainContext && mainContext.CurrentScene is not null
        );

    private static Command RemoveObj() =>
        new(
            displayName: "Remove selected Object(s)",
            action: window =>
            {
                if (window is not MainWindowContext mainContext)
                    return;
                bool unselect = true;
                foreach (ISceneObj del in mainContext.CurrentScene!.SelectedObjects)
                {
                    
                    if (del is IStageSceneObj || del is RailSceneObj) ChangeHandler.ChangeRemove(mainContext, mainContext.CurrentScene.History, del);
                    else if (del is RailPointSceneObj) ChangeHandler.ChangeRemovePoint(mainContext, mainContext.CurrentScene.History, (del as RailPointSceneObj)!);
                    else if (del is RailHandleSceneObj && !mainContext.CurrentScene!.SelectedObjects.Contains((del as RailHandleSceneObj)!.ParentPoint)) 
                    {
                        ChangeHandler.ChangeHandleTransform(mainContext.CurrentScene.History, (del as RailHandleSceneObj)!, (del as RailHandleSceneObj)!.Offset, System.Numerics.Vector3.Zero, false);
                        unselect = !(mainContext.CurrentScene!.SelectedObjects.Count() < 2);
                    }
                }
                if (unselect)
                    mainContext.CurrentScene.UnselectAllObjects();
            },
            enabled: window =>
                window is MainWindowContext mainContext
                && mainContext.CurrentScene is not null
                && mainContext.CurrentScene.SelectedObjects.Any()
                && !mainContext.IsTransformActive
        );

    private static Command DuplicateObj() =>
        new(
            displayName: "Duplicate selected Object(s)",
            action: window =>
            {
                if (window is not MainWindowContext mainContext)
                    return;

                int count = mainContext.CurrentScene!.SelectedObjects.Count();
                List<uint> newPickIds = new();

                foreach (ISceneObj copy in mainContext.CurrentScene.SelectedObjects)
                {
                    if (copy is RailPointSceneObj) continue;
                    if (copy is IStageSceneObj stageCopy && mainContext.CurrentScene.SelectedObjects.Any(x => x is IStageSceneObj y && y.StageObj.Children != null && y.StageObj.Children.Contains(stageCopy.StageObj)))
                        continue;

                    uint newestPickId = ChangeHandler.ChangeDuplicate(mainContext, mainContext.CurrentScene.History, copy);
                    newPickIds.Add(newestPickId);
                    
                    if(copy is IStageSceneObj stageCopy1) CheckPickChildren(stageCopy1.StageObj, ref newPickIds, ref newestPickId);
                }
                if (newPickIds.Count < 1) return;
                mainContext.CurrentScene.UnselectAllObjects();

                for (int i = 0; i < newPickIds.Count; i++)
                {
                    mainContext.CurrentScene.SetObjectSelected(newPickIds[i], true);
                }

                mainContext.SetSceneDuplicateTranslation();
            },
            enabled: window =>
                window is MainWindowContext mainContext
                && mainContext.CurrentScene is not null
                && mainContext.CurrentScene.SelectedObjects.Any()
                && !mainContext.IsTransformActive
        );

    private static void CheckPickChildren(StageObj StageObj, ref List<uint> newPickIds, ref uint newestPickId)
    {
        if (StageObj.Children is not null && StageObj.Children.Count > 0)
        {
            for (int i = 0; i < StageObj.Children.Count; i++)
            {
                newestPickId -= 1;
                newPickIds.Add(newestPickId);
                CheckPickChildren(StageObj.Children[i], ref newPickIds, ref newestPickId);
            }

        }
    }

    private static Command HideObj() =>
        new(
            displayName: "Hide selected object(s)",
            action: window =>
            {
                if (window is not MainWindowContext mainContext)
                    return;

                ChangeHandler.ChangeHideMultiple(mainContext.CurrentScene!.History, mainContext.CurrentScene.SelectedObjects);
            },
            enabled: window =>
                window is MainWindowContext mainContext && mainContext.CurrentScene is not null && mainContext.CurrentScene.SelectedObjects.Any() && mainContext.IsSceneFocused
        );
    private static Command UnselectAll() =>
        new(
            displayName: "Unselect all objects",
            action: window =>
            {
                if (window is not MainWindowContext mainContext)
                    return;

                mainContext.CurrentScene!.UnselectAllObjects();
            },
            enabled: window =>
                window is MainWindowContext mainContext && mainContext.CurrentScene is not null && mainContext.CurrentScene.SelectedObjects.Any() && mainContext.IsSceneFocused
        );

    private Command GotoParent() =>
        new(
            displayName: "Select Parent // First Child",
            action: window =>
            {
                if (window is not MainWindowContext mainContext)
                    return;

                if (mainContext.CurrentScene!.SelectedObjects.First() is not IStageSceneObj stageSceneObj)
                    return;

                StageObj parent = stageSceneObj.StageObj.Parent ?? stageSceneObj.StageObj;

                if (stageSceneObj.StageObj.Parent != null)
                    ChangeHandler.ToggleObjectSelection(mainContext, mainContext.CurrentScene.History, mainContext.CurrentScene.EnumerateStageSceneObjs().First(x => x.StageObj == parent).PickingId, true);
                else
                    ChangeHandler.ToggleObjectSelection(mainContext, mainContext.CurrentScene.History, mainContext.CurrentScene.EnumerateStageSceneObjs().First(x => x.StageObj.Parent == parent).PickingId, true);

                AxisAlignedBoundingBox aabb = mainContext.CurrentScene.SelectedObjects.First().AABB * stageSceneObj.StageObj.Scale;
                mainContext.CurrentScene!.Camera.LookFrom(mainContext.CurrentScene.SelectedObjects.First().Transform.Translation, aabb.GetDiagonal() * 0.01f);
            },
            enabled: window =>
            {
                if (window is not MainWindowContext mainContext || mainContext.CurrentScene is null || mainContext.CurrentScene.SelectedObjects.First() is not IStageSceneObj stageSceneObj)
                    return false;

                return mainContext.CurrentScene.SelectedObjects.Count() == 1 && (stageSceneObj.StageObj.Parent != null || (stageSceneObj.StageObj.Children != null && stageSceneObj.StageObj.Children.Any()));
            }
        );

    private static Command Undo() =>
        new(
            displayName: "Undo",
            action: window =>
            {
                if (window is not MainWindowContext mainContext)
                    return;

                mainContext.CurrentScene!.History.Undo();
                mainContext.CurrentScene.IsSaved =
                    mainContext.CurrentScene.SaveUndoCount == mainContext.CurrentScene.History.UndoSteps;
            },
            enabled: window =>
                window is MainWindowContext mainContext
                && mainContext.CurrentScene is not null
                && mainContext.CurrentScene.History.CanUndo
                && !mainContext.IsTransformActive
        );

    private static Command Redo() =>
        new(
            displayName: "Redo",
            action: window =>
            {
                if (window is not MainWindowContext mainContext)
                    return;

                mainContext.CurrentScene!.History.Redo();
                mainContext.CurrentScene.IsSaved =
                    mainContext.CurrentScene.SaveUndoCount == mainContext.CurrentScene.History.UndoSteps;
            },
            enabled: window =>
                window is MainWindowContext mainContext
                && mainContext.CurrentScene is not null
                && mainContext.CurrentScene.History.CanRedo
                && !mainContext.IsTransformActive
        );

    #endregion
}
