namespace Autumn.Context;

internal class LayeredSettings(params Dictionary<string, object?>[] settings)
{
    #region Setting definition

    public bool UseClassNames => GetSetting<bool>(nameof(UseClassNames));
    public string? RomFSPath => GetSetting<string>(nameof(RomFSPath));

    #endregion

    private T? GetSetting<T>(string key)
    {
        foreach (var dictionary in settings)
        {
            if (!dictionary.TryGetValue(key, out object? value) || value is not T)
                continue;

            return (T)value;
        }

        return default;
    }
}
