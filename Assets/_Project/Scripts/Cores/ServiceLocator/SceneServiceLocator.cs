using UnityEngine;
using System.Collections.Generic;
using System;
using System.Diagnostics.CodeAnalysis;
using Kope.Core.CompilerServices;
using Kope.Core.Execution;

namespace Kope.Core.ServiceLocator {
	/// <summary>
	/// Scene-scoped Service Locator
	/// This locator manages services that are specific to a particular scene.
	/// Services can be registered via the Inspector or dynamically at runtime.
	/// It ensures that only one instance of each service type exists within the scene.
	/// If a service is requested but not found in the registry, it will search the scene for an existing instance.
	/// If multiple instances are found, a warning is logged and the first instance in the hierarchy is used.
	/// Services registered through this locator are not persistent across scene loads.
	/// Services are renamed upon registration to indicate their source (Inspector or Found on Demand).
	/// This helps with debugging and maintaining clarity in the scene hierarchy.
	/// Services should inherit from SceneServiceBase to be compatible with this locator.
	/// </summary>
	[CustomExecutionOrder(-50)]
	public class SceneServiceLocator : ServiceLocator<SceneServiceLocator, SceneServiceBase> {
		[SerializeField] private List<SceneServiceBase> sceneServices = new();

		protected override void Awake() {
			this.isPersistent = false;
			base.Awake();

			foreach (var service in this.sceneServices) {
				if (service != null)
					RegisterService(service);
			}
		}

		public void RegisterService<TService>(TService service) where TService : SceneServiceBase {
			if (service == null) return;

			var type = service.GetType();
			if (services.ContainsKey(type)) {
				if (!ReferenceEquals(services[type], service)) {
					MyLogger.Warn($"[Locator] Duplicate {type.Name} detected. The secondary instance will be destroyed to maintain the Singleton pattern.");
					Destroy(service.gameObject);
				}
				return;
			}
			RenameAndRegister(service, " [Scene]_Registered_Through_Inspector", "via SceneService Registered Service", false);
		}

		public bool TryGetService<TService>([MaybeNullWhen(false)] out TService service) where TService : SceneServiceBase {
			var type = typeof(TService);
			service = null;

			// 1. Primary: Dictionary check
			if (services.TryGetValue(type, out var existing)) {
				service = (TService)existing;
				return true;
			}

			TService[] instances = FindObjectsByType<TService>(FindObjectsSortMode.None);

			if (instances.Length > 0) {
				Array.Sort(instances, (a, b) => GetHierarchyDepth(a.transform).CompareTo(GetHierarchyDepth(b.transform)));
				TService mainInstance = instances[0];

				RenameAndRegister(mainInstance, " [Scene]_Found_On_Scene_Not_Registered", "Found on Demand, Please Add the " + type.Name +
				" Service to the Inspector List, it will improve performance \n" +
				"and clarity.But for now it will find and use first Instance found on Scene", true);
				service = mainInstance;
				return true;
			}

			MyLogger.Error($"[ServiceLocator] {type.Name} requested but not found! It must be in the Scene or Inspector list.");
			return false;
		}

		private void RenameAndRegister<TService>(TService service, string postFix, string message, bool isWarn) where TService : SceneServiceBase {
			string goName = service.gameObject.name;
			if (goName.Contains("[Scene]_"))
				goName = goName[..goName.IndexOf("[Scene]_")];

			service.gameObject.name = goName + postFix;
			Register(service, postFix + " " + message, isWarn);
		}

		private void Register(SceneServiceBase service, string message, bool isWarn = false) {
			var type = service.GetType();
			if (services.ContainsKey(type)) return;

			services[type] = service;
			//	Debug.Log($"{(isWarn ? "[ServiceLocator][Warning]" : "[ServiceLocator]")} Registered {type.Name}: {service.gameObject.name} via {message}", service.gameObject);
			service.InitializeService();
		}
	}
}