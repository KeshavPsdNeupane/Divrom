using System.Runtime.CompilerServices;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kope.Logging {

#if UNITY_EDITOR
	[InitializeOnLoad]
#endif
	public static class KLog {
		private static ILogger _logger;

		private static ILogger Instance => _logger ??= new UnityLogger();

		const string EDITOR_PREFS_KEY = "KLog_UseFileLogger";

		static KLog() {
			Init();
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Init() {
#if UNITY_EDITOR
			bool useFile = EditorPrefs.GetBool(EDITOR_PREFS_KEY, false);
			_logger = useFile ? new FileLogger() : new UnityLogger();
#else
			_logger = new UnityLogger();
#endif
		}

		public static void Configure(bool useFileLogger = false) {
			_logger = useFileLogger ? new FileLogger() : new UnityLogger();

#if UNITY_EDITOR
			// Save the state so it doesn't reset on script compilation!
			EditorPrefs.SetBool(EDITOR_PREFS_KEY, useFileLogger);
#endif
		}


		[HideInCallstack]
		public static void Log(string message, Object context = null,
			[CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) {

			string fileName = System.IO.Path.GetFileName(filePath);
			string formattedMessage = $"<color=yellow><b>[Info]</b> </color> {message} <color=#888888>(at {fileName}:{lineNumber})</color>";

			Instance.Log(formattedMessage, context);
		}


		[HideInCallstack]
		public static void LogWarning(string message, Object context = null,
					[CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) {

			string fileName = System.IO.Path.GetFileName(filePath);
			string formattedMessage = $"<color=orange><b>[Warning]</b> </color> {message} <color=#888888>(at {fileName}:{lineNumber})</color>";

			Instance.LogWarning(formattedMessage, context);
		}



		[HideInCallstack]
		public static void LogError(string message, Object context = null,
					[CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) {

			string fileName = System.IO.Path.GetFileName(filePath);
			string formattedMessage = $"<color=red><b>[Error]</b> </color> {message} <color=#888888>(at {fileName}:{lineNumber})</color>";

			Instance.LogError(formattedMessage, context);
		}
	}
}