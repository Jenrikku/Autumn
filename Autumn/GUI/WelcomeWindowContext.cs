using System.Numerics;
using Autumn.IO;
using ImGuiNET;
using Silk.NET.OpenGL;
using TinyFileDialogsSharp;

namespace Autumn.GUI;

// This Window is meant to be populated as Autumn's features increases.
internal class WelcomeWindowContext : WindowContext
{
    private byte _currentPage = 0;

    private string _romfsInput = string.Empty;
    private bool _romfsIsValidPath = false;

    public WelcomeWindowContext()
        : base()
    {
        Window.Size = new(540, 540);
        Window.Title = "Welcome to Autumn!";

        Window.Render += (deltaSeconds) =>
        {
            if (ImGuiController is null)
                return;

            ImGuiController.MakeCurrent();

            ImGui.SetNextWindowPos(new(0, 0));
            ImGui.SetNextWindowSize(ImGui.GetMainViewport().Size);

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(60, 40));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);

            if (
                !ImGui.Begin(
                    "Welcome",
                    ImGuiWindowFlags.NoDecoration
                        | ImGuiWindowFlags.NoResize
                        | ImGuiWindowFlags.NoMove
                        | ImGuiWindowFlags.NoSavedSettings
                )
            )
                return;

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

                    if (ImGui.Button("Exit", new(80, 20)))
                        WindowManager.Stop();

                    ImGui.SetCursorPosY(buttonsRowY);
                    ImGui.SetCursorPosX(secondButtonX);

                    if (ImGui.Button("Start", new(80, 20)))
                        _currentPage++;

                    break;

                case 1:
                    ImGui.TextWrapped(
                        "In order to function properly, Autumn will need the RomFS from 3D Land."
                            + " Please input the path to an unmodifier copy of 3D Land's romfs."
                    );

                    ImGui.SetCursorPosY(pathInputY);

                    ImGuiWidgets.DirectoryPathSelector(
                        ref _romfsInput,
                        ref _romfsIsValidPath,
                        ImGui.GetContentRegionAvail().X - 60,
                        "Select the folder containing the RomFS"
                    );

                    ImGui.SetCursorPosY(buttonsRowY);

                    if (ImGui.Button("Back", new(80, 20)))
                        _currentPage--;

                    ImGui.SetCursorPosY(buttonsRowY);
                    ImGui.SetCursorPosX(secondButtonX);

                    if (ImGui.Button("Continue", new(80, 20)))
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
                        "Autumn works differently than other stage editors since it introduces its kind"
                            + " of \"Projects\". Please make sure to read the wiki for more info. (WIP)"
                    ); // Add a link to the wiki.

                    ImGui.SetCursorPosY(buttonsRowY);

                    if (ImGui.Button("Back", new(80, 20)))
                        _currentPage--;

                    ImGui.SetCursorPosY(buttonsRowY);
                    ImGui.SetCursorPosX(secondButtonX);

                    if (ImGui.Button("End", new(80, 20)))
                        _currentPage++;

                    break;

                default:
                    WindowManager.Remove(this);

                    if (WindowManager.IsEmpty)
                        WindowManager.Add(new MainWindowContext());

                    if (!string.IsNullOrEmpty(_romfsInput))
                        RomFSHandler.RomFSPath = _romfsInput;

                    break;
            }

            ImGui.End();

            ImGui.PopStyleVar();

            GL!.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL!.Clear(ClearBufferMask.ColorBufferBit);
            GL!.Viewport(Window.FramebufferSize);
            ImGuiController.Render();
        };
    }
}