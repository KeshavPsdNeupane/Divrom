using System.Runtime.CompilerServices;

public static class Logger
{
    private static ILogger _logger;

    // Ensures _logger is always ready
    private static ILogger Instance => _logger ??= new UnityLogger();

    public static bool IsFileLogger => _logger is FileLogger;

    public static void Configure(bool useFileLogger = false)
    {
        _logger = useFileLogger
            ? new FileLogger()
            : new UnityLogger();
    }

    public static void Error(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        string fileName = System.IO.Path.GetFileName(filePath);
        Instance.Error($"[Error] {message} : at {fileName} line {lineNumber}");
    }

    public static void Warn(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        string fileName = System.IO.Path.GetFileName(filePath);
        Instance.Warn($"[Warning] {message} : at {fileName} line {lineNumber}");
    }

    public static void Log(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        string fileName = System.IO.Path.GetFileName(filePath);
        Instance.Log($"[Info] {message} : at {fileName} line {lineNumber}");
    }
}
