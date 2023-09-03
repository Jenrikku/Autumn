using System.Security.Cryptography;
using System.Text;

namespace Autumn.Utils;

public class HashUtils
{
    // Original code from: https://stackoverflow.com/a/15683147
    public static string CreateMd5ForFolder(string path)
    {
        string[] files = Directory
            .GetFiles(path, "*", SearchOption.AllDirectories)
            .OrderBy(p => p)
            .ToArray();

        using MD5 md5 = MD5.Create();

        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];

            // Hash path:
            string relativePath = Path.GetRelativePath(path, file);
            byte[] pathBytes = Encoding.Unicode.GetBytes(relativePath.Replace("\\", "/"));
            md5.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

            // Hash contents:
            byte[] contentBytes = File.ReadAllBytes(file);
            if (i == files.Length - 1)
                md5.TransformFinalBlock(contentBytes, 0, contentBytes.Length);
            else
                md5.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
        }

        return BitConverter.ToString(md5.Hash ?? Array.Empty<byte>()).Replace("-", "").ToLower();
    }
}
