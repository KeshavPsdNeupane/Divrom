namespace Kope.Core.CompilerServices
{
    public interface ILogger
    {
        void Log(string message, UnityEngine.Object context = null);
        void Warn(string message, UnityEngine.Object context = null);
        void Error(string message, UnityEngine.Object context = null);
    }
}
