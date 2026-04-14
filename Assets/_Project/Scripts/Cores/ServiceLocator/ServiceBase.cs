using UnityEngine;
using Kope.Core.CompilerServices;

namespace ServiceLocatorPattern {
	public class ServiceBase : MonoBehaviour {
		private bool isInitialized = false;
		public bool IsInitialized => isInitialized;
		public void InitializeService() {
			if (this.isInitialized) return;
			this.isInitialized = OnInitializeService();
		}
		protected virtual bool OnInitializeService() {
			return true;
		}
	}

	public class GlobalServiceBase : ServiceBase { }
	public class SceneServiceBase : ServiceBase {

		public void Awake() {
			CheckForDuplicates();
		}
		private void CheckForDuplicates() {
			// Finds all instances of the specific concrete class (e.g., AudioManager)
			var type = GetType();
			var instances = FindObjectsByType(type, FindObjectsSortMode.None);

			if (instances.Length > 1) {
				MyLogger.Warn($"[ServiceLocator] Multiple instances of <b>{type.Name}</b> found on scene! For now Game will use first one...", this.gameObject);
			}
		}


	}

}