using ServiceLocatorPattern;
using UnityEngine;

public static class GlobalBootStrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Logger.Configure();
        var gs = GlobalServiceLocator.Instance;
        gs.RegisterService<InputManager>();
        gs.Lock();
    }
}
