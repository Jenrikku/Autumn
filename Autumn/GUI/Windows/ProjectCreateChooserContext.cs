using Autumn.Context;
using ImGuiNET;

namespace Autumn.GUI.Windows;

internal class ProjectCreateChooserContext : ProjectChooserContext
{
    public ProjectCreateChooserContext(ContextHandler contextHandler, WindowManager windowManager)
        : base(contextHandler, windowManager) { }

    protected override bool IsTargetValid()
    {
        string path = Path.Join(CurrentDirectory, SelectedFile);
        DirectoryInfo dirInfo = new(CurrentDirectory);

        if (!string.IsNullOrEmpty(SelectedFile))
        {
            if (File.Exists(path))
            {
                PathError = "The path already exists and is a file.";
                return false;
            }

            if (Directory.Exists(path))
            {
                if (IsDirRomFS[SelectedFile])
                {
                    PathError = "The path already contains a project in it.";
                    return false;
                }

                return true;
            }
        }
        else if (dirInfo.EnumerateFileSystemInfos().Any()) // Directory is not empty
        {
            PathError = "The path must be empty.";
            return false;
        }

        if (dirInfo.Attributes.HasFlag(FileAttributes.ReadOnly))
        {
            PathError = "The path is read only.";
            return false;
        }

        return true;
    }

    protected override void OkButtonAction()
    {
        string path = Path.Join(CurrentDirectory, SelectedFile);

        if (string.IsNullOrEmpty(SelectedFile) || !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            InvokeSuccessCallback([path]);
            return;
        }

        ChangeDirectory(path);
    }

    protected override bool IsDisabled(string name) => IsDirRomFS[name];
}
