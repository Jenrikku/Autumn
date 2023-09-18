using System.Text.RegularExpressions;

namespace Autumn.IO;

internal partial class FileUtils
{
    public static IEnumerable<(string name, byte scenario)> EnumerateStages(string path)
    {
        List<(string name, byte scenario)> stages = new();

        if (!Directory.Exists(path))
            yield break;

        Regex regex = StageFileRegex();

        foreach (string file in Directory.EnumerateFiles(path))
        {
            Match match = regex.Match(Path.GetFileName(file));

            if (!match.Success)
                continue;

            string name = match.Groups[1].Value;
            byte scenario = byte.Parse(match.Groups[3].Value);

            if (!stages.Contains((name, scenario)))
                yield return (name, scenario);
        }
    }

    [GeneratedRegex("(.*)(Design|Map|Sound)(\\d+\\b).szs")]
    private static partial Regex StageFileRegex();
}
