using Kope.Core;
using Kope.Core.ObjectPooling;
using Kope.SaveSystem;
using ServiceLocatorPattern;
using UnityEngine;

public static class GlobalBootStrap {
	static GlobalServiceLocator GS => GlobalServiceLocator.Instance;
	static readonly string PREFAB_TO_PRELOAD_PATH = "ScriptableObjects/PoolSettings";

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void Init() {
		GlobalServiceLocator.InjectDimension(AxisMode.TwoD);
		GS.RegisterService(() => new GameObject().AddComponent<InputManager>());
		GS.RegisterService(() => new GameObject().AddComponent<GlobalSaveSystem>());
		GS.RegisterService(() => new GameObject().AddComponent<ObjectPooler>());

		if (GS.TryGetService<ObjectPooler>(out var pooler)) {
			var poolDatas = Resources.LoadAll<PoolingObjectData>(PREFAB_TO_PRELOAD_PATH);
			pooler.Preload(poolDatas);
		}
		GS.Lock();

	}
}
