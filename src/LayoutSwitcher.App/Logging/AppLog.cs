namespace LayoutSwitcher.App.Logging;

/// <summary>
/// Plain file log, off by default. Callers pass only event kinds, counts, layout ids and timings —
/// never typed text, keystrokes or clipboard content (design §10).
/// </summary>
internal static class AppLog
{
    public enum Level
    {
        None,
        Error,
        Info,
    }

    private static readonly Lock Sync = new();
    private static Level _level = Level.None;

    public static void Configure(string? level) =>
        _level = Enum.TryParse<Level>(level, ignoreCase: true, out var parsed) ? parsed : Level.None;

    public static void Error(string message, Exception? e = null) =>
        Write(Level.Error, e is null ? message : $"{message}: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");

    public static void Info(string message) => Write(Level.Info, message);

    private static void Write(Level level, string message)
    {
        if (level > _level)
            return;
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(AppPaths.LogDirectory);
                var file = Path.Combine(AppPaths.LogDirectory, $"LayoutSwitcher-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss.fff} {level,-5} {message}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // Logging must never take the app down: it is called from the engine's own error handler.
        }
    }
}
