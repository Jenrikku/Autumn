using NARCSharp;
using System.IO;

namespace AutumnSceneGL.IO {
    internal class SZSWrapper {
        public static NARCFileSystem? ReadFile(string path) {
            byte[] compressed;
            NARC result;

            try {
                compressed = File.ReadAllBytes(path);
                result = NARCParser.Read(Yaz0.Decompress(compressed));
            } catch { return null; }

            return result.AsFileSystem();
        }

        public static NARCFileSystem? ReadFile(byte[] data) {
            try {
                return NARCParser.Read(Yaz0.Decompress(data)).AsFileSystem();
            } catch { return null; }
        }

        public static bool TryReadFile(string path, out NARCFileSystem? result) {
            result = ReadFile(path);
            return result is not null;
        }

        public static bool TryReadFile(byte[] data, out NARCFileSystem? result) {
            result = ReadFile(data);
            return result is not null;
        }

        /// <summary>
        /// Checks if the passed file is a Yaz0-compressed NARC archive.
        /// </summary>
        public static bool ValidateFile(string path) {
            byte[] compressed;

            try {
                compressed = File.ReadAllBytes(path);
                return NARCParser.Identify(Yaz0.Decompress(compressed));
            } catch { return false; }
        }
    }
}
