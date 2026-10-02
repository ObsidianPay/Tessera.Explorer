namespace Tessera.Explorer.Services;

public enum LogLevel { Info, Ok, Warn, Error, Data, Trace }

public sealed record LogEntry(DateTime Time, LogLevel Level, string Op, string Text)
{
    public override string ToString() => $"{Time:HH:mm:ss.fff} {Level.ToString().ToUpperInvariant(),-5} {Op,-22} {Text}";
}

/// <summary>The persistent output stream every page writes to. Thread safe; raises <see cref="Added"/> on the UI thread.</summary>
public static class OutputLog
{
    private const int Capacity = 5000;
    private static readonly List<LogEntry> Entries = [];

    public static event Action<LogEntry>? Added;
    public static event Action? Cleared;

    public static IReadOnlyList<LogEntry> Snapshot()
    {
        lock (Entries) return Entries.ToArray();
    }

    public static void Write(LogLevel level, string op, string text)
    {
        var entry = new LogEntry(DateTime.Now, level, op, text);
        lock (Entries)
        {
            Entries.Add(entry);
            if (Entries.Count > Capacity) Entries.RemoveRange(0, Entries.Count - Capacity);
        }
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) Added?.Invoke(entry);
        else dispatcher.BeginInvoke(() => Added?.Invoke(entry));
    }

    public static void Info(string op, string text) => Write(LogLevel.Info, op, text);
    public static void Ok(string op, string text) => Write(LogLevel.Ok, op, text);
    public static void Warn(string op, string text) => Write(LogLevel.Warn, op, text);
    public static void Error(string op, string text) => Write(LogLevel.Error, op, text);
    public static void Data(string op, string text) => Write(LogLevel.Data, op, text);

    public static void Clear()
    {
        lock (Entries) Entries.Clear();
        Cleared?.Invoke();
    }
}
