using System.Numerics;
using Autumn.GUI.Windows;
using ImGuiNET;
using TinyFileDialogsSharp;

namespace Autumn.GUI.Dialogs;

internal class WelcomeDialog
{
    private readonly MainWindowContext _window;

    private bool _isOpened = false;

    private byte _currentPage = 0;

    private string _romfsInput;
    private bool _romfsIsValidPath;

    public WelcomeDialog(MainWindowContext window)
    {
        _window = window;
        _romfsInput = window.ContextHandler.Settings.RomFSPath ?? string.Empty;
        _romfsIsValidPath = Directory.Exists(_romfsInput);
    }

    public void Open() => _isOpened = true;

    public void Render()
    {
        if (!_isOpened)
            return;

        ImGui.OpenPopup("Welcome to Autumn!");

        ImGui.SetNextWindowSize(new(540, 540), ImGuiCond.Once);
        ImGui.SetNextWindowPos(
            ImGui.GetMainViewport().GetCenter(),
            ImGuiCond.Always,
            new(0.5f, 0.5f)
        );

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(60, 40));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);

        ImGui.BeginPopupModal(
            "Welcome to Autumn!",
            ref _isOpened,
            ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoSavedSettings
        );

        Vector2 windowSize = ImGui.GetWindowSize();

        float buttonsRowY = windowSize.Y - 60;
        float secondButtonX = windowSize.X - 140;

        float pathInputY = windowSize.Y / 2;

        switch (_currentPage)
        {
            case 0:
                ImGui.TextWrapped(
                    "Welcome to Autumn, the stage editor for 3D Land.\n\n"
                        + "Before you continue, you will have to make some initial configurations."
                );

                ImGui.SetCursorPosY(buttonsRowY);

                if (ImGui.Button("Exit", new(80, 0)))
                {
                    _isOpened = false;
                    break;
                }

                ImGui.SetCursorPosY(buttonsRowY);
                ImGui.SetCursorPosX(secondButtonX);

                if (ImGui.Button("Start", new(80, 0)))
                    _currentPage++;

                break;

            case 1:
                ImGui.TextWrapped(
                    "In order to function properly, Autumn will need the RomFS from 3D Land."
                        + " Please input the path to a copy of 3D Land's romfs:"
                );

                ImGui.SetCursorPosY(pathInputY);

                ImGuiWidgets.DirectoryPathSelector(
                    ref _romfsInput,
                    ref _romfsIsValidPath,
                    width: ImGui.GetContentRegionAvail().X - 60,
                    dialogTitle: "Select the folder containing the RomFS"
                );

                ImGui.SetCursorPosY(buttonsRowY);

                if (ImGui.Button("Back", new(80, 0)))
                    _currentPage--;

                ImGui.SetCursorPosY(buttonsRowY);
                ImGui.SetCursorPosX(secondButtonX);

                if (ImGui.Button("Continue", new(80, 0)))
                {
                    if (string.IsNullOrEmpty(_romfsInput))
                    {
                        MessageBoxButton result = TinyFileDialogs.MessageBox(
                            "Warning",
                            "You have not specified a path for the RomFS."
                                + " This will disable many features from Autumn.\n\n"
                                + "Do you wish to continue anyways?",
                            DialogType.YesNo,
                            MessageIconType.Warning,
                            MessageBoxButton.NoCancel
                        );

                        if (result != MessageBoxButton.OkYes)
                            break;
                    }
                    else if (!_romfsIsValidPath)
                    {
                        TinyFileDialogs.MessageBox(
                            "Error",
                            "The specified path does not exist.",
                            iconType: MessageIconType.Error
                        );

                        break;
                    }

                    _currentPage++;
                }

                break;

            case 2:
                ImGui.TextWrapped(
                    "Autumn works differently than other stage editors since it introduces projects.\n\n"
                        + "In order to edit stages you must first create a new project within an empty folder. "
                        + "You can afterwards add your stages to a StageData folder within the contents folder "
                        + "that will be created in your project directory. Alternatively, you may use the Add "
                        + "Stage menu item to import stages from the romfs."
                );

                ImGui.SetCursorPosY(buttonsRowY);

                if (ImGui.Button("Back", new(80, 0)))
                    _currentPage--;

                ImGui.SetCursorPosY(buttonsRowY);
                ImGui.SetCursorPosX(secondButtonX);

                if (ImGui.Button("End", new(80, 0)))
                    _currentPage++;

                break;

            default:
                _isOpened = false;
                break;
        }

        if (!_isOpened)
        {
            if (!string.IsNullOrEmpty(_romfsInput))
                _window.ContextHandler.SetGlobalSetting("RomFSPath", _romfsInput);

            _window.ContextHandler.SystemSettings.SkipWelcomeDialog = true;
        }

        ImGui.End();

        ImGui.PopStyleVar();
        ImGui.PopStyleVar();
    }
}
