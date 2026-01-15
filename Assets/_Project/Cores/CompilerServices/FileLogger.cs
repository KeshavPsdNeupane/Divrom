using System;
using System.IO;
namespace Kope.Core.CompilerServices
{
    public class FileLogger : ILogger
    {
        private const string LogFolder = "logs";

        private static string LogFilePath
        {
            get
            {
                if (!Directory.Exists(LogFolder))
                    Directory.CreateDirectory(LogFolder);

                return Path.Combine(LogFolder, $"ErrorLog_{DateTime.UtcNow:yyyy-MM-dd}.txt");
            }
        }

        private static string TimeStamp =>
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        public void Error(string message, UnityEngine.Object context = null) =>
            AppendLog($"[{TimeStamp}] : {message}");

        public void Warn(string message, UnityEngine.Object context = null) =>
            AppendLog($"[{TimeStamp}] : {message}");

        public void Log(string message, UnityEngine.Object context = null) =>
            AppendLog($"[{TimeStamp}] : {message}");

        private void AppendLog(string message)
        {
            File.AppendAllText(LogFilePath, message + "\n");
        }
    }
}