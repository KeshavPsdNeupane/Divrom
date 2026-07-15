using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Kope.Logging {
	public class FileLogger : ILogger {
		private const string LogFolder = "logs";
		private static readonly Regex StripHtmlRegex = new("<[^>]*>", RegexOptions.Compiled);

		private static string LogFilePath {
			get {
				if (!Directory.Exists(LogFolder))
					Directory.CreateDirectory(LogFolder);

				return Path.Combine(LogFolder, $"ErrorLog_{DateTime.UtcNow:yyyy-MM-dd}.txt");
			}
		}

		/// <summary>
		/// Gets the current timestamp in UTC format for log entries.
		/// Using UTC ensures that logs are consistent across different time zones and avoids
		/// issues with daylight saving time changes. Or even region based time changes. This 
		/// is especially important for applications that may be used globally or in environments 
		/// where the local time may change unexpectedly.
		/// </summary>
		private static string TimeStamp =>
			DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

		public void LogError(string message, UnityEngine.Object context = null) =>
			AppendLog($"[{TimeStamp}] : {message}");

		public void LogWarning(string message, UnityEngine.Object context = null) =>
			AppendLog($"[{TimeStamp}] : {message}");

		public void Log(string message, UnityEngine.Object context = null) =>
			AppendLog($"[{TimeStamp}] : {message}");

		private void AppendLog(string message) {
			string cleanMessage = StripHtmlRegex.Replace(message, string.Empty);
			File.AppendAllText(LogFilePath, cleanMessage + "\n");
		}
	}
}