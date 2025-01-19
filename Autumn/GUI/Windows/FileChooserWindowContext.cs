using System.Numerics;
using Autumn.Context;
using ImGuiNET;

namespace Autumn.GUI.Windows;

internal abstract class FileChooserWindowContext : WindowContext
{
    public string Title { get; set; } = "Autumn: File Chooser";
    public string DefaultPath { get; set; } = Home;

    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    // In order to go up one directory.
    private string _parentDirectory = string.Empty;

    private const ImGuiWindowFlags _mainWindowFlags =
        ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoSavedSettings;

    private const float _bottomPanelBaseHeight = 60;

    protected static readonly string Root = OperatingSystem.IsWindows() ? "C:\\" : "/";
    protected static readonly string Home = Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile
    );

    protected string SearchString = "";
    protected string SelectedFile = "";
    protected string CurrentDirectory = Home;

    /// <summary>
    /// The current directory's file and subdirectory infos.<br>
    /// Updated every time the window is focused.
    /// </summary>
    protected readonly List<FileSystemInfo> DirectoryEntries = new();

    /// <summary>
    /// The comparison used to sort files and directories.
    /// </summary>
    protected Comparison<FileSystemInfo> CurrentComparison = CompareByName;

    public FileChooserWindowContext(ContextHandler contextHandler, WindowManager windowManager)
        : base(contextHandler, windowManager)
    {
        Window.Load += () =>
        {
            Window.Title = Title;
            Window.Size = new(640, 480);

            ChangeDirectory(DefaultPath);
        };

        Window.FocusChanged += focused =>
        {
            if (!focused)
                return;

            // Refresh contents
            ChangeDirectory(CurrentDirectory, updateHistory: false);
        };

        Window.Render += (deltaSeconds) =>
        {
            if (ImGuiController is null)
                return;

            ImGuiController.MakeCurrent();

            ImGuiViewportPtr viewport = ImGui.GetMainViewport();

            ImGui.SetNextWindowPos(new(0, 0));
            ImGui.SetNextWindowSize(viewport.Size);

            if (!ImGui.Begin("##FileChooser", _mainWindowFlags))
                return;

            if (
                ImGui.BeginChild(
                    "FileTopBar",
                    new(
                        ImGui.GetContentRegionAvail().X,
                        ImGui.GetFontSize() + ImGui.GetStyle().ItemInnerSpacing.Y * 2
                    ),
                    ImGuiChildFlags.None,
                    ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoScrollWithMouse
                )
            )
            {
                if (_historyIndex <= 0)
                    ImGui.BeginDisabled();

                if (ImGui.ArrowButton("Back", ImGuiDir.Left))
                    ChangeDirectory(_history[--_historyIndex], updateHistory: false);

                ImGui.EndDisabled();
                ImGui.SameLine();

                if (_historyIndex == _history.Count - 1)
                    ImGui.BeginDisabled();

                if (ImGui.ArrowButton("Forward", ImGuiDir.Right))
                    ChangeDirectory(_history[++_historyIndex], updateHistory: false);

                ImGui.EndDisabled();
                ImGui.SameLine();

                if (string.IsNullOrEmpty(_parentDirectory))
                    ImGui.BeginDisabled();

                if (ImGui.ArrowButton("Up", ImGuiDir.Up))
                    ChangeDirectory(_parentDirectory);

                ImGui.EndDisabled();
                ImGui.SameLine();
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 5);

                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 5);

                ImGui.InputTextWithHint("", "Search...", ref SearchString, 1024);

                ImGui.EndChild();
            }

            if (
                ImGui.BeginChild(
                    "FilePathBar",
                    new(
                        ImGui.GetContentRegionAvail().X,
                        ImGui.GetFontSize() + ImGui.GetStyle().ItemInnerSpacing.Y * 2
                    ),
                    ImGuiChildFlags.None,
                    ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoScrollWithMouse
                )
            )
            {
                string[] tokens = CurrentDirectory.Split(Path.DirectorySeparatorChar);
                tokens[0] = Path.GetPathRoot(CurrentDirectory)!;

                for (int i = 0; i < tokens.Length; )
                {
                    string token = tokens[i];

                    if (ImGui.Button(token))
                    {
                        string path = tokens[0];

                        for (int j = 1; j <= i; j++)
                            path = Path.Join(path, tokens[j]);

                        if (path != CurrentDirectory)
                            ChangeDirectory(path);
                    }

                    if (++i >= tokens.Length || string.IsNullOrEmpty(tokens[i]))
                        break;

                    ImGui.SameLine();
                    ImGui.Text(">");
                    ImGui.SameLine();
                }

                ImGui.EndChild();
            }

            if (
                ImGui.BeginChild(
                    "##Places",
                    ImGui.GetContentRegionAvail() / new Vector2(4, 1),
                    ImGuiChildFlags.Border
                )
            )
            {
                if (ImGui.Selectable("Home"))
                    ChangeDirectory(Home);

                ImGui.EndChild();
            }

            ImGui.SameLine();

            if (ImGui.BeginChild("##Main", ImGui.GetContentRegionAvail()))
            {
                Vector2 fileChooseSize = ImGui.GetContentRegionAvail();
                fileChooseSize.Y -= _bottomPanelBaseHeight * ScalingFactor;

                if (ImGui.BeginChild("##FileChoose", fileChooseSize, ImGuiChildFlags.Border))
                {
                    RenderFileChoosePanel();
                    ImGui.EndChild();
                }

                if (ImGui.BeginChild("##Bottom", ImGui.GetContentRegionAvail()))
                {
                    float width = ImGui.GetContentRegionAvail().X;

                    ImGui.SetNextItemWidth(width);

                    bool enterPressed = ImGui.InputText(
                        "",
                        ref SelectedFile,
                        1024,
                        ImGuiInputTextFlags.EnterReturnsTrue
                    );

                    float buttonWidth = 60 * ScalingFactor;
                    Vector2 buttonSize = new(buttonWidth, 0);

                    ImGui.SetCursorPosX(width - 2 * buttonWidth - ImGui.GetStyle().CellPadding.X);

                    if (ImGui.Button("Cancel", buttonSize))
                        Window.Close();

                    ImGui.SameLine();

                    if (
                        (ImGui.Button("Ok", buttonSize) || enterPressed)
                        && !string.IsNullOrEmpty(SelectedFile)
                    ) { }
                }

                ImGui.EndChild();
            }

            GL!.Viewport(Window.FramebufferSize);
            ImGuiController.Render();
        };
    }

    protected abstract void RenderFileChoosePanel();

    protected void ChangeDirectory(string directory, bool updateHistory = true)
    {
        CurrentDirectory = directory;
        _parentDirectory = Directory.GetParent(directory)?.FullName ?? string.Empty;

        if (updateHistory)
        {
            if (++_historyIndex < _history.Count)
                _history.RemoveRange(_historyIndex, _history.Count - _historyIndex);

            _history.Add(directory);
        }

        DirectoryEntries.Clear();

        DirectoryInfo dirInfo = new(directory);
        DirectoryEntries.AddRange(dirInfo.EnumerateFileSystemInfos());

        DirectoryEntries.Sort(CurrentComparison);
    }

    #region Comparisions

    protected static int CompareByName(FileSystemInfo i1, FileSystemInfo i2)
    {
        if (i1 is DirectoryInfo && i2 is FileInfo)
            return -1;

        if (i1 is FileInfo && i2 is DirectoryInfo)
            return 1;

        return string.Compare(i1.Name, i2.Name, ignoreCase: true);
    }

    protected static int CompareByDate(FileSystemInfo i1, FileSystemInfo i2) =>
        DateTime.Compare(i1.LastWriteTime, i2.LastAccessTime);

    protected static int CompareBySize(FileSystemInfo i1, FileSystemInfo i2)
    {
        if (i1 is DirectoryInfo && i2 is FileInfo)
            return -1;

        if (i1 is FileInfo && i2 is DirectoryInfo)
            return 1;

        if (i1 is FileInfo file1 && i2 is FileInfo file2)
            return file1.Length.CompareTo(file2.Length);

        return string.Compare(i1.Name, i2.Name, ignoreCase: true); // Directories
    }

    #endregion
}
