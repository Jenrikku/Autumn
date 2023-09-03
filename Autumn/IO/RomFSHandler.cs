namespace Autumn.IO;

internal static class RomFSHandler
{
    public static string? RomFSPath { get; set; }

    public static bool RomFSAvailable => RomFSPath is not null;

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
