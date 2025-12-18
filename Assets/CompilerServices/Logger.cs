using System.Runtime.CompilerServices;
using UnityEngine;

public static class Logger
{
    private static ILogger _logger;
    public static bool IsFileLogger => _logger is FileLogger;
    public static void Configure(bool useFileLogger = false)
    {
        _logger = useFileLogger
            ? new FileLogger()
            : new UnityLogger();
    }

    public static void LogError(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        string fileName = System.IO.Path.GetFileName(filePath);
        _logger.LogError($"[Error] {message} : at {fileName} line {lineNumber}");
    }

    public static void LogWarning(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        string fileName = System.IO.Path.GetFileName(filePath);
        _logger.LogWarning($"[Warning] {message} : at {fileName} line {lineNumber}");
    }
    public static void Log(string message,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
    {
        string fileName = System.IO.Path.GetFileName(filePath);
        _logger.Log($"[Info] {message} : at {fileName} line {lineNumber}");
    }
}


public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Logger.Configure();
    }
}
