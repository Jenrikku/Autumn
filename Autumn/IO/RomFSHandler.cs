using AutumnSceneGL.Storage;
using AutumnStageEditor.Storage.StageObj;
using AutumnStageEditor.Storage.StageObj.Interfaces;
using Jenrikku.BYAML;
using NARCSharp;
using SPICA.Formats.CtrGfx;
using SPICA.Formats.CtrH3D;
using System.Diagnostics;
using System.Text;

namespace AutumnSceneGL.IO {
    internal static class RomFSHandler {
        public static string? RomFSPath { get; set; }

        public static bool RomFSAvailable => RomFSPath is not null;

        public static byte[] RequestFile(string relPath, bool isSZS = true) {
            if(isSZS)
                relPath += ".szs";

            if(Project.TryGetFile(relPath, out byte[]? result))
                return result ?? Array.Empty<byte>();

            if(!RomFSAvailable || !File.Exists(Path.Join(RomFSPath, relPath)))
                return Array.Empty<byte>();

            try {
                return File.ReadAllBytes(Path.Join(RomFSPath, relPath));
            } catch {
                return Array.Empty<byte>();
            }
        }

        public static H3D? RequestModel(string name) {
            byte[] data = RequestFile(Path.Join("ObjectData", name));

            if(data.Length <= 0 || !SZSWrapper.TryReadFile(data, out NARCFileSystem? result))
                return null;

            byte[] gfx = result!.GetFile(name + ".bcmdl");

            if(gfx.Length <= 0)
                return null;

            using MemoryStream stream = new(gfx);

            return Gfx.Open(stream).ToH3D();
        }
    }
}
