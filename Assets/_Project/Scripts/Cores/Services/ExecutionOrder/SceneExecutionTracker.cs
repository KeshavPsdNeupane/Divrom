using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ZLinq;
using Kope.Core.Attribute;

namespace Kope.Core.Execution {
	[ExecuteAlways]
	public class SceneExecutionOrderTracker : MonoBehaviour {
		[Serializable]
		public struct TrackedComponent {
			[ReadOnly] public string typeName;
			[ReadOnly] public int order;
			[ReadOnly] public string gameObjectName;
		}

		[Tooltip("Automatically populated with all MonoBehaviours in the scene using CustomExecutionOrderAttribute")]
		[SerializeField, ReadOnly] private List<TrackedComponent> trackedComponents = new();
		// Public read-only access at runtime
		public IReadOnlyList<TrackedComponent> TrackedComponents => trackedComponents;

		void Awake() {
			this.trackedComponents.Clear();

			// Find all MonoBehaviours in the scene, including inactive
			var allMonos = FindObjectsByType<MonoBehaviour>(
				findObjectsInactive: FindObjectsInactive.Include);

			foreach (var mono in allMonos) {
				var type = mono.GetType();
				var attr = type.GetCustomAttribute<CustomExecutionOrderAttribute>();
				if (attr != null) {
					this.trackedComponents.Add(new TrackedComponent {
						typeName = type.Name,
						order = attr.order,
						gameObjectName = mono.gameObject.name
					});
				}
			}

			// Sort from lowest to highest execution order
			this.trackedComponents = trackedComponents.AsValueEnumerable()
				.OrderBy(tc => tc.order)
				.ToList();
		}
	}
}