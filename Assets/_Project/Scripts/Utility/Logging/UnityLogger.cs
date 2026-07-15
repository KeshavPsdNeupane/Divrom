namespace Kope.Logging
{
	public class UnityLogger : ILogger
	{
		public void Log(string message, UnityEngine.Object context = null) => UnityEngine.Debug.Log(message, context);
		public void LogWarning(string message, UnityEngine.Object context = null) => UnityEngine.Debug.LogWarning(message, context);
		public void LogError(string message, UnityEngine.Object context = null) => UnityEngine.Debug.LogError(message, context);
	}
}