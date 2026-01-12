using Kope.Core.CompilerServices;
using ServiceLocatorPattern;
using UnityEngine;

public static class GlobalBootStrap
{
    static GlobalServiceLocator GS => GlobalServiceLocator.Instance;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        //  MyLogger.Configure();
        GS.RegisterService(() => new GameObject().AddComponent<InputManager>());

        GS.Lock();

    }
}
