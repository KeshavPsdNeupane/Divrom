using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kope.Core.Entity {
	[CreateAssetMenu(fileName = "EntityComponentRegistryConfig", menuName = "Scriptable Objects/Actors/EntityComponentRegistryConfig", order = 1)]
	public class EntityComponentRegistryConfig : ScriptableObject {

		[Header("Excluded Types")]
		[SerializeField, Tooltip("Full type names (Namespace.TypeName) to exclude from component registration.")]
		private List<string> excludedTypeNames = new();

		// Remove 'readonly' as Unity serialization can interfere with it on ScriptableObjects.
		// We will initialize these lazily or during Init.
		private HashSet<Type> excludedTypeSet;

		private static readonly Type[] commonTypes =
		{
			typeof(MonoBehaviour),
			typeof(Component),
			typeof(Behaviour),
			typeof(ScriptableObject),
			typeof(UnityEngine.Object)
		};

		public HashSet<Type> ExcludedTypeSet {
			get {
				if (excludedTypeSet == null) InitType();
				return excludedTypeSet;
			}
		}

		private void OnEnable() => InitType();
		private void OnValidate() => InitType();

		private void InitType() {
			// Initialize if null, otherwise clear to reuse memory
			if (this.excludedTypeSet == null) this.excludedTypeSet = new HashSet<Type>();
			else excludedTypeSet.Clear();

			foreach (var type in commonTypes) {
				this.excludedTypeSet.Add(type);
			}

			if (this.excludedTypeNames == null || this.excludedTypeNames.Count == 0)
				return;

			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (var typeName in this.excludedTypeNames) {
				if (string.IsNullOrWhiteSpace(typeName)) continue;

				foreach (var assembly in assemblies) {
					var type = assembly.GetType(typeName);
					if (type != null) {
						this.excludedTypeSet.Add(type);
						break;
					}
				}
			}
		}


	}
}