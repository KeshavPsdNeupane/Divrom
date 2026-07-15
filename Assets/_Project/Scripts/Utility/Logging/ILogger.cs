namespace Kope.Logging {
	public interface ILogger {
		void Log(string message, UnityEngine.Object context = null);
		void LogWarning(string message, UnityEngine.Object context = null);
		void LogError(string message, UnityEngine.Object context = null);
	}
}
