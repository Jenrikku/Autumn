namespace Autumn.Context;

internal class Project
{
    public string SavePath { get; set; }

    public string ContentsPath => SavePath;
    public string ProjectFile => Path.Join(SavePath, "autumnproj.yml");

    public Dictionary<string, object?> ProjectSettings { get; set; } = new();

    public Project(string savePath) => SavePath = Path.GetFullPath(savePath);
}
