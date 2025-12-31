using UnityEngine;


public class UnityLogger : ILogger
{
    public void Error(string message)
    => Debug.LogError(message);
    public void Warn(string message)
    => Debug.LogWarning(message);
    public void Log(string message)
    => Debug.Log(message);
}
