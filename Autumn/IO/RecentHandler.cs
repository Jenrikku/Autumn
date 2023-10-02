using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Autumn.GUI;

namespace Autumn.IO;

internal static class RecentHandler
{
    public static ObservableCollection<string> RecentOpenedPaths { get; } = new();

    private static string _lastProjectOpenPath = string.Empty;
    public static string LastProjectOpenPath
    {
        get => _lastProjectOpenPath;
        set
        {
            _lastProjectOpenPath = value;
            SettingsHandler.SetValue("LastProjectOpenPath", value);
        }
    }

    private static string _lastProjectSavePath = string.Empty;
    public static string LastProjectSavePath
    {
        get => _lastProjectSavePath;
        set
        {
            _lastProjectSavePath = value;
            SettingsHandler.SetValue("LastProjectSavePath", value);
        }
    }

    public static void LoadFromSettings()
    {
        string[] array = SettingsHandler.GetValue("RecentlyOpenedProjects", Array.Empty<string>())!;

        RecentOpenedPaths.CollectionChanged -= OnRecentPathsChanged;

        RecentOpenedPaths.Clear();

        foreach (string str in array)
            RecentOpenedPaths.Add(str);

        RecentOpenedPaths.CollectionChanged += OnRecentPathsChanged;

        _lastProjectOpenPath = SettingsHandler.GetValue("LastProjectOpenPath", string.Empty)!;
        _lastProjectSavePath = SettingsHandler.GetValue("LastProjectSavePath", string.Empty)!;
    }

    private static void OnRecentPathsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        SettingsHandler.SetValue("RecentlyOpenedProjects", RecentOpenedPaths.ToArray());
}
