using System.Diagnostics;
using System.Numerics;
using Autumn.Context;
using Autumn.Utils;
using ImGuiNET;

namespace Autumn.GUI.Windows;

internal abstract class FileChooserWindowContext : WindowContext
{
    public string Title { get; set; } = "Autumn: File Chooser";
    public string DefaultPath { get; set; } = Home;

    /// <summary>
    /// Event that is triggered when the user selects a file.
    /// The argument of the delegated is an array with the paths of the selected files.
    /// </summary>
    public event Action<string[]> SuccessCallback;

    /// <summary>
    /// Event that is triggered when the user cancels the file selection.
    /// </summary>
    public event Action CancelCallback;

    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    // In order to go up one directory.
    private string _parentDirectory = string.Empty;

    private const ImGuiWindowFlags _mainWindowFlags =
        ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoSavedSettings;

    private const float _bottomPanelBaseHeight = 60;

    /// <summary>
    /// The active file comparisons available.
    /// </summary>
    private Comparison<FileSystemInfo>[] _activeComparisons = [CompareByName];

    private Comparison<FileSystemInfo>[] _invertedComparisons = [CompareByName];

    private int _comparisonIndex = 0;
    private bool _isComparisonInverted = false;

    /// <summary>
    /// When set to true, the top bar will display an input text for manual path change.
    /// </summary>
    private bool _inputtingPath = false;
    private string _inputPathBuffer = "";

    protected static readonly string Root = OperatingSystem.IsWindows() ? "C:\\" : "/";
    protected static readonly string Home = Environment.GetFolderPath(
        Environment.SpecialFolder.UserProfile
    );

    protected string SearchString = "";
    protected string SelectedFile = "";
    protected string CurrentDirectory = Home;

    protected DriveInfo[] Drives;

    // Meant to be set in a constructor.
    protected readonly bool IsMultiFileSelect = false;

    /// <summary>
    /// The current directory's file and subdirectory infos.<br>
    /// Updated every time the window is focused.
    /// </summary>
    protected readonly List<FileSystemInfo> DirectoryEntries = new();

    public FileChooserWindowContext(ContextHandler contextHandler, WindowManager windowManager)
        : base(contextHandler, windowManager)
    {
        SuccessCallback += _ => Window.Close();
        CancelCallback += Window.Close;
        Drives = DriveInfo.GetDrives();

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

            // Refresh drive list
            Drives = DriveInfo.GetDrives();

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
                if (_inputtingPath)
                {
                    float buttonWidth = 30 * ScalingFactor;
                    float width = ImGui.GetContentRegionAvail().X;
                    width -= buttonWidth * 2 + ImGui.GetStyle().CellPadding.X * 4;

                    ImGui.SetNextItemWidth(width);

                    bool enterPressed = ImGui.InputText(
                        "",
                        ref _inputPathBuffer,
                        1024,
                        ImGuiInputTextFlags.EnterReturnsTrue
                    );

                    ImGui.SameLine();

                    if (ImGui.Button("Ok", new(buttonWidth, 0)) || enterPressed)
                    {
                        _inputtingPath = false;
                        ChangeDirectory(
                            _inputPathBuffer,
                            updateHistory: CurrentDirectory != _inputPathBuffer
                        );
                    }

                    ImGui.SameLine();

                    if (ImGui.Button("X", new(buttonWidth, 0)))
                        _inputtingPath = false;
                }

                Vector2 initCur = ImGui.GetCursorPos();

                ImGui.SetNextItemAllowOverlap();

                if (ImGui.InvisibleButton("ManualPath", ImGui.GetContentRegionAvail()))
                {
                    _inputtingPath = true;
                    _inputPathBuffer = CurrentDirectory;
                }

                ImGui.SetCursorPos(initCur);

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

                if (ImGui.Selectable("Documents"))
                {
                    string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    ChangeDirectory(dir);
                }

                if (ImGui.Selectable("Pictures"))
                {
                    string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                    ChangeDirectory(dir);
                }

                if (ImGui.Selectable("Music"))
                {
                    string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                    ChangeDirectory(dir);
                }

                if (ImGui.Selectable("Videos"))
                {
                    string dir = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                    ChangeDirectory(dir);
                }

                if (ImGui.Selectable("Desktop"))
                {
                    string dir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    ChangeDirectory(dir);
                }

                ImGui.Separator();

                foreach (DriveInfo drive in Drives)
                {
                    if (
                        drive.DriveType == DriveType.Ram
                        || drive.DriveType == DriveType.Unknown
                        || drive.Name == "/"
                        || drive.Name.StartsWith("/sys")
                    )
                        continue;

                    if (ImGui.Selectable(drive.Name))
                        ChangeDirectory(drive.RootDirectory.FullName);
                }

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
                        4096,
                        ImGuiInputTextFlags.EnterReturnsTrue
                    );

                    float buttonWidth = 60 * ScalingFactor;
                    Vector2 buttonSize = new(buttonWidth, 0);

                    ImGui.SetCursorPosX(width - 2 * buttonWidth - ImGui.GetStyle().CellPadding.X);

                    if (ImGui.Button("Cancel", buttonSize))
                        CancelCallback.Invoke();

                    ImGui.SameLine();

                    if (
                        (ImGui.Button("Ok", buttonSize) || enterPressed)
                        && !string.IsNullOrEmpty(SelectedFile)
                    )
                    {
                        if (IsMultiFileSelect)
                        {
                            string[] result = SelectedFile.SplitExcept(';');

                            for (int i = 0; i < result.Length; i++)
                                result[i] = Path.Join(CurrentDirectory, result[i]);

                            SuccessCallback.Invoke(result);
                        }
                        else
                        {
                            string[] result = [Path.Join(CurrentDirectory, SelectedFile)];
                            SuccessCallback.Invoke(result);
                        }
                    }
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
        if (string.IsNullOrEmpty(directory))
            return;

        CurrentDirectory = directory;
        _parentDirectory = Directory.GetParent(directory)?.FullName ?? string.Empty;

        if (updateHistory)
        {
            if (++_historyIndex < _history.Count)
                _history.RemoveRange(_historyIndex, _history.Count - _historyIndex);

            _history.Add(directory);

            SelectedFile = string.Empty;
        }

        DirectoryEntries.Clear();

        DirectoryInfo dirInfo = new(directory);

        try
        {
            DirectoryEntries.AddRange(dirInfo.EnumerateFileSystemInfos());
        }
        catch (Exception e)
        {
            Debug.Print(e.Message);
        }

        Comparison<FileSystemInfo> comparison;

        if (_isComparisonInverted)
            comparison = _invertedComparisons[_comparisonIndex];
        else
            comparison = _activeComparisons[_comparisonIndex];

        DirectoryEntries.Sort(comparison);
    }

    protected void SetupFileComparisons(params Comparison<FileSystemInfo>[] comparisons)
    {
        _activeComparisons = comparisons;

        List<Comparison<FileSystemInfo>> inverted = new(comparisons.Length);

        foreach (var comparison in comparisons)
            inverted.Add((info1, info2) => -comparison(info1, info2));

        _invertedComparisons = inverted.ToArray();
    }

    protected void UpdateFileSortByTable(ImGuiTableSortSpecsPtr specs)
    {
        if (!specs.SpecsDirty)
            return;

        _comparisonIndex = specs.Specs.ColumnIndex;
        _isComparisonInverted = specs.Specs.SortDirection == ImGuiSortDirection.Descending;
        ChangeDirectory(CurrentDirectory, updateHistory: false);
        specs.SpecsDirty = false;
    }

    protected void InvokeCancelCallback() => CancelCallback.Invoke();

    protected void InvokeSuccessCallback(string[] result) => SuccessCallback.Invoke(result);

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
        DateTime.Compare(i1.LastWriteTime, i2.LastWriteTime);

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
