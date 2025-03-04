using System.IO.Compression;
using Autumn.Wrappers;

namespace Autumn.Context;

/// <summary>
/// Class used to store values for system settings.<br/>
/// System settings are separated from regular settings
/// so that they may not be saved to the project.
/// </summary>
internal class SystemSettings
{
    public List<string> RecentlyOpenedPaths = new();
    public string LastProjectOpenPath = string.Empty;
    public bool SkipWelcomeDialog = false;
    public bool OpenLastProject = false;
    public string LastOpenedProject = string.Empty;
    public bool EnableVSync = false;
    public int Theme = 0;
    public int MouseSpeed = 20;
    public bool UseWASD = false;
    public bool UseMiddleMouse = false;
    public bool EnableDBEditor = false;
    public Yaz0Wrapper.CompressionLevel Yaz0Compression = Yaz0Wrapper.CompressionLevel.Medium;

    public void AddRecentlyOpenedPath(string path)
    {
        // Force item to always be the first on the list.
        RecentlyOpenedPaths.Remove(path);
        RecentlyOpenedPaths.Insert(0, path);
    }
}
