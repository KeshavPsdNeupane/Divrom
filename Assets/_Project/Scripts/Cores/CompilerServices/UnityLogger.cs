namespace Kope.Core.CompilerServices
{
    public class UnityLogger : ILogger
    {
        public void Log(string message, UnityEngine.Object context = null) => UnityEngine.Debug.Log(message, context);
        public void Warn(string message, UnityEngine.Object context = null) => UnityEngine.Debug.LogWarning(message, context);
        public void Error(string message, UnityEngine.Object context = null) => UnityEngine.Debug.LogError(message, context);
    }
}