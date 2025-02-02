using Autumn.Enums;
using Autumn.GUI;
using Autumn.History;
using Autumn.Rendering;
using Autumn.Storage;
using TinyFileDialogsSharp;

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
                CommandID.Exit => Exit(),
                CommandID.AddStage => AddStage(),
                CommandID.SaveStage => SaveStage(),
                CommandID.AddObject => AddObj(),
                CommandID.RemoveObj => RemoveObj(),
                CommandID.DuplicateObj => DuplicateObj(),
                CommandID.HideObj => HideObj(),
                CommandID.Undo => Undo(),
                CommandID.Redo => Redo(),
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
                        window.ContextHandler.FSHandler.WriteStage(stage);
                        scene.IsSaved = true;
                        scene.SaveUndoCount = mainContext.CurrentScene.History.UndoSteps;
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

                foreach (SceneObj del in mainContext.CurrentScene!.SelectedObjects)
                {
                    ChangeHandler.ChangeRemove(mainContext, mainContext.CurrentScene.History, del);
                }

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

                foreach (SceneObj copy in mainContext.CurrentScene.SelectedObjects)
                {
                    newPickIds.Add(ChangeHandler.ChangeDuplicate(mainContext, mainContext.CurrentScene.History, copy));
                }

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

    private static Command HideObj() =>
        new(
            displayName: "Hide selected object(s)",
            action: window =>
            {
                if (window is not MainWindowContext mainContext)
                    return;
                ChangeHandler.ChangeFieldValueMultiple<bool>(
                    mainContext.CurrentScene!.History,
                    mainContext.CurrentScene!.SelectedObjects,
                    "isVisible"
                );
            },
            enabled: window =>
                window is MainWindowContext mainContext
                && mainContext.CurrentScene is not null
                && mainContext.CurrentScene.SelectedObjects.Any()
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
