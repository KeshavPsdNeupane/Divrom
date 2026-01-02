using System.Runtime.CompilerServices;
using UnityEngine;

public static class Logger
{
    private static ILogger _logger;
    private static ILogger Instance => _logger ??= new UnityLogger();

    public static void Configure(bool useFileLogger = false)
    {
        _logger = useFileLogger ? new FileLogger() : new UnityLogger();
    }

    public static void Log(string message, Object context = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        string fileName = System.IO.Path.GetFileName(filePath);
        Instance.Log($"<color=yellow><b>[Info]</b> </color> {message} <color=#888888>(at {fileName}:{lineNumber})</color>", context);
    }

    public static void Warn(string message, Object context = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        string fileName = System.IO.Path.GetFileName(filePath);
        Instance.Warn($"<color=orange><b>[Warning]</b> </color> {message} <color=#888888>(at {fileName}:{lineNumber})</color>", context);
    }

    public static void Error(string message, Object context = null,
        [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        string fileName = System.IO.Path.GetFileName(filePath);
        Instance.Error($"<color=red><b>[Error]</b> </color> {message} <color=#888888>(at {fileName}:{lineNumber})</color>", context);
    }
}