namespace LayoutSwitcher.Core.Config;

/// <summary>An entry without a directory separator matches the exe file name; otherwise the full path.</summary>
public sealed class ExclusionList
{
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);

    public ExclusionList(IEnumerable<string> entries)
    {
        foreach (var raw in entries)
        {
            var entry = Normalize(raw.Trim());
            if (entry.Length == 0)
                continue;
            if (entry.Contains('\\'))
                _paths.Add(entry);
            else
                _names.Add(entry);
        }
    }

    public bool IsExcluded(string exePath)
    {
        var path = Normalize(exePath);
        return _paths.Contains(path) || _names.Contains(Path.GetFileName(path));
    }

    private static string Normalize(string path) => path.Replace('/', '\\');
}
