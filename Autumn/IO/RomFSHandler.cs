using Autumn.GUI;

namespace Autumn.IO;

internal static class RomFSHandler
{
    private static string? s_romfsPath = null;
    public static string? RomFSPath
    {
        get => s_romfsPath;
        set
        {
            s_romfsPath = value;

            if (value is not null)
                SettingsHandler.SetValue("RomFSPath", value);
        }
    }

    public static bool RomFSAvailable => !string.IsNullOrEmpty(RomFSPath);

    public static void LoadFromSettings() =>
        s_romfsPath = SettingsHandler.GetValue<string>("RomFSPath");

    private static readonly List<(string, byte)> s_stageNames = new();
    public static List<(string, byte)> StageNames
    {
        get
        {
            if (!RomFSAvailable)
                return new();

            if (s_stageNames.Count <= 0)
            {
                string stageDataPath = Path.Join(RomFSPath, "StageData");

                if (!Directory.Exists(stageDataPath))
                    return new();

                s_stageNames.Clear();

                foreach (var tuple in FileUtils.EnumerateStages(stageDataPath))
                    s_stageNames.Add(tuple);
            }

            return s_stageNames;
        }
    }

    public static bool VerifyRomFS()
    {
        if (!RomFSAvailable)
            return false;

        return false;

        string[] hashes = new string[]
        {
            "", // Japanese        (0004000000054100)
            "", // European        (0004000000053F00)
            "", // North American  (0004000000054000)
            "", // Korean          (0004000000089D00)
            "", // Taiwanese       (0004000000089E00)
            "", // Chinese         (0004000000089F00)
            ""
        };
    }

    // public static byte[] GetFile(string relPath, bool isSZS = true)
    // {
    //     if (isSZS)
    //         relPath += ".szs";

    //     if (!RomFSAvailable || !FileExists(relPath, false))
    //         return Array.Empty<byte>();

    //     try
    //     {
    //         return File.ReadAllBytes(Path.Join(RomFSPath, relPath));
    //     }
    //     catch
    //     {
    //         return Array.Empty<byte>();
    //     }
    // }

    // public static bool FileExists(string relPath, bool isSZS = true)
    // {
    //     if (isSZS)
    //         relPath += ".szs";

    //     return File.Exists(Path.Join(RomFSPath, relPath));
    // }

    // public static H3D? RequestModel(string name) {
    //     byte[] data = RequestFile(Path.Join("ObjectData", name));

    //     if(data.Length <= 0 || !SZSWrapper.TryReadFile(data, out NARCFileSystem? result))
    //         return null;

    //     byte[] gfx = result!.GetFile(name + ".bcmdl");

    //     if(gfx.Length <= 0)
    //         return null;

    //     using MemoryStream stream = new(gfx);

    //     return Gfx.Open(stream).ToH3D();
    // }
}
