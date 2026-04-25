using Kope.Core;
using Kope.SaveSystem;
using ServiceLocatorPattern;
using UnityEngine;

public static class GlobalBootStrap {
	static GlobalServiceLocator GS => GlobalServiceLocator.Instance;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void Init() {
		//  MyLogger.Configure();
		GlobalServiceLocator.InjectDimension(AxisMode.TwoD);
		GS.RegisterService(() => new GameObject().AddComponent<InputManager>());
		GS.RegisterService(() => new GameObject().AddComponent<GlobalSaveSystem>());

		GS.Lock();

	}
}
