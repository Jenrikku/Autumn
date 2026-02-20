using System.IO.Compression;
using Autumn.Enums;
using Autumn.Wrappers;

namespace Autumn.Context;

/// <summary>
/// Class used to store values for system settings.<br/>
/// System settings are separated from regular settings
/// so that they may not be saved to the project.
/// </summary>
internal class SystemSettings
{
    public readonly List<string> RecentlyOpenedPaths = new();
    public string LastProjectOpenPath = string.Empty; // File chooser
    public string LastOpenedProject = string.Empty;
    public bool RestoreNativeFileDialogs;
    public bool SkipWelcomeDialog;
    public bool OpenLastProject;
    public bool EnableVSync;
    public bool AlwaysPreviewStageLights;
    public string Theme = string.Empty;
    public int MouseSpeed;
    public bool UseWASD;
    public bool UseMiddleMouse;
    public bool ZoomToMouse;
    public bool RememberLayout;
    public bool EnableDBEditor;
    public bool[] VisibleDefaults = [];
    public bool ShowRelationLines;
    public HoverInfoMode ShowHoverInfo;
    public Yaz0Wrapper.CompressionLevel Yaz0Compression;

    public SystemSettings() => Reset();

    public void AddRecentlyOpenedPath(string path)
    {
        // Force item to always be the first on the list.
        RecentlyOpenedPaths.Remove(path);
        RecentlyOpenedPaths.Insert(0, path);
    }

    public void Reset()
    {
        RestoreNativeFileDialogs = false;
        SkipWelcomeDialog = false;
        OpenLastProject = true;
        EnableVSync = true;
        AlwaysPreviewStageLights = true;
        Theme = "ImGui Dark";
        MouseSpeed = 20;
        UseWASD = false;
        UseMiddleMouse = true;
        ZoomToMouse = false;
        RememberLayout = true;
        EnableDBEditor = false;
        ShowRelationLines = true;
        ShowHoverInfo = HoverInfoMode.Disabled;
        Yaz0Compression = Yaz0Wrapper.CompressionLevel.Medium;

        // Areas, CameraAreas, Rails, Grid, TransparentWall
        VisibleDefaults = [false, true, true, true, false];
    }
}
