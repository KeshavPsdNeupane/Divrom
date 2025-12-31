using ServiceLocatorPattern;
using UnityEngine;

public static class GlobalBootStrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Logger.Configure();
        GlobalServiceLocator.Instance.RegisterService<InputManager>();
    }
}
