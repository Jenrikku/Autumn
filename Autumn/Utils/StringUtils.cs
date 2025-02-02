namespace Autumn.Utils;

internal static class StringUtils
{
    /// <summary>
    /// Splits a string using the separator except when \\ prefixes it.
    /// </summary>
    /// <param name="separator">The separator to split the string</param>
    /// <returns>An array with the resulting splitted strings</returns>
    public static string[] SplitExcept(this string str, char separator)
    {
        string[] splitted = str.Split(
            separator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        List<string> amended = new(splitted.Length);

        for (int i = 0; i < splitted.Length; i++)
        {
            string current = splitted[i];

            while (current.EndsWith('\\') && current[^2] != '\\' && i < splitted.Length - 1)
                current += splitted[++i];

            amended.Add(current.Replace("\\\\", "\\"));
        }

        return amended.ToArray();
    }
}
