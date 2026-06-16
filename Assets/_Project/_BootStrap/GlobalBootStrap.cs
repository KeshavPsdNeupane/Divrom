using Kope.Core;
using Kope.Core.ObjectPooling;
using Kope.SaveSystem;
using Kope.Core.ServiceLocator;
using UnityEngine;

public static class GlobalBootStrap {
	static GlobalServiceLocator GS => GlobalServiceLocator.Instance;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void Init() {
		GlobalServiceLocator.InjectDimension(AxisMode.TwoD);
		GS.RegisterService(() => new GameObject().AddComponent<InputManager>());
		GS.RegisterService(() => new GameObject().AddComponent<GlobalSaveSystem>());
		GS.RegisterService(() => new GameObject().AddComponent<ObjectPooler>());
		GS.Lock();

	}
}
