using System.Runtime.CompilerServices;
using UnityEngine;
namespace Kope.Core.CompilerServices
{
#if UNITY_EDITOR
#endif

	public static class MyLogger
	{
		private static ILogger _logger;
		private static ILogger Instance => _logger ??= new UnityLogger();

		static MyLogger()
		{
			Configure();
		}


		public static void Configure(bool useFileLogger = false)
		{
			_logger = useFileLogger ? new FileLogger() : new UnityLogger();
		}

		public static void Log(string message, Object context = null,
			[CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
		{
			string fileName = System.IO.Path.GetFileName(filePath);
			string formattedMessage = $"<color=yellow><b>[Info]</b> </color> {message} <color=#888888>(at {fileName}:{lineNumber})</color>";

#if UNITY_EDITOR
			// Log even in Edit mode
			Debug.Log(formattedMessage, context);
#else
        Instance.Log(formattedMessage, context);
#endif
		}

		public static void Warn(string message, Object context = null,
			[CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
		{
			string fileName = System.IO.Path.GetFileName(filePath);
			string formattedMessage = $"<color=orange><b>[Warning]</b> </color> {message} <color=#888888>(at {fileName}:{lineNumber})</color>";

#if UNITY_EDITOR
			Debug.LogWarning(formattedMessage, context);
#else
        Instance.Warn(formattedMessage, context);
#endif
		}

		public static void Error(string message, Object context = null,
			[CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
		{
			string fileName = System.IO.Path.GetFileName(filePath);
			string formattedMessage = $"<color=red><b>[Error]</b> </color> {message} <color=#888888>(at {fileName}:{lineNumber})</color>";

#if UNITY_EDITOR
			Debug.LogError(formattedMessage, context);
#else
        Instance.Error(formattedMessage, context);
#endif
		}
	}
}