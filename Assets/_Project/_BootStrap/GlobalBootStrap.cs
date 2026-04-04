using Kope.Core.SaveSystem;
using ServiceLocatorPattern;
using UnityEngine;

public static class GlobalBootStrap {
	static GlobalServiceLocator GS => GlobalServiceLocator.Instance;
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void Init() {
		//  MyLogger.Configure();
		GS.RegisterService(() => new GameObject().AddComponent<InputManager>());
		GS.RegisterService(() => new GameObject().AddComponent<GlobalSaveSystem>());

		if (GS.TryGetService<GlobalSaveSystem>(out var saveSystem)) {
			saveSystem.Print();
		}

		GS.Lock();

	}
}
