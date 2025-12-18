using UnityEngine;


public class UnityLogger : ILogger
{
    public void LogError(string message)
    => Debug.LogError(message);
    public void LogWarning(string message)
    => Debug.LogWarning(message);
    public void Log(string message)
    => Debug.Log(message);
}
