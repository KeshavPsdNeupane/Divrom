using System.Collections.Generic;
using UnityEngine;
using ZLinq;

namespace Kope.Core.EntityComponentRegistry {
	[CreateAssetMenu(fileName = "EntityComponentRegistryConfig", menuName = "Scriptable Objects/Actors/EntityComponentRegistryConfig", order = 1)]
	public class EntityComponentRegistryConfig : ScriptableObject {

		[Header("Excluded Types")]
		[SerializeField, Tooltip("Full type names (Namespace.TypeName) to exclude from component registration.")]
		private List<string> excludedTypeNames = new();

		// Remove 'readonly' as Unity serialization can interfere with it on ScriptableObjects.
		// We will initialize these lazily or during Init.
		private HashSet<System.Type> excludedTypeSet;

		public HashSet<System.Type> ExcludedTypeSet {
			get {
				if (excludedTypeSet == null) InitType();
				return excludedTypeSet;
			}
		}

		private void OnEnable() => InitType();
		private void OnValidate() => InitType();

		private void InitType() {
			// Initialize if null, otherwise clear to reuse memory
			if (this.excludedTypeSet == null) this.excludedTypeSet = new HashSet<System.Type>();
			else excludedTypeSet.Clear();

			// Resolve the types using the helper function
			ResolveAndPopulateTypes(this.excludedTypeNames, this.excludedTypeSet, "Excluded Type");
		}

		/// <summary>
		/// Shared helper that resolves string type names into System.Type references and adds them to a destination set.
		/// </summary>
		private void ResolveAndPopulateTypes(List<string> sourceNames, HashSet<System.Type> destinationSet, string contextLabel) {
			if (sourceNames == null || sourceNames.Count == 0)
				return;

			var assemblies = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();

			foreach (var typeName in sourceNames) {
				bool found = false;
				var strippedTypeName = typeName.Trim();
				if (string.IsNullOrWhiteSpace(strippedTypeName)) continue;

				// 1. Attempt Exact Matching (Namespace.TypeName)
				foreach (var assembly in assemblies) {
					var type = assembly.GetType(strippedTypeName);

					if (type != null) {
						destinationSet.Add(type);
						found = true;
						break;
					}
				}

				// 2. Fallback Matching: Search by Class Name only if Namespace has changed
				if (!found && strippedTypeName.Contains('.')) {
					string classOnly = strippedTypeName.Substring(strippedTypeName.LastIndexOf('.') + 1);

					foreach (var assembly in assemblies) {
						try {
							var type = assembly.GetTypes().AsValueEnumerable()
								.FirstOrDefault(t => t.Name == classOnly);
							if (type != null) {
								destinationSet.Add(type);
								found = true;

								// Warning: Let the designer know the namespace in the config is stale
								Debug.LogWarning(
									$"[Registry Config] Stale namespace configuration for ({contextLabel})!\n" +
									$"The class '{classOnly}' was located, but its namespace has moved.\n" +
									$"Copy this corrected name to update your config asset:\n" +
									$"{type.FullName}\n\n", this);

								break;
							}
						} catch (System.Reflection.ReflectionTypeLoadException) {
							// Safe-guard against dynamic assemblies that cannot be queried
							continue;
						}
					}
				}
				// 3. Absolute Failure: Log a highly visible error
				if (!found) {
					Debug.LogError(
						$"<color=#ff4d4d><b>[Registry Config] CRITICAL ERROR:</b></color> {contextLabel} '{strippedTypeName}' " +
						$"could not be found in any loaded assembly! Verification failed. Check spelling or assembly references.", this);
				}
			}
		}
	}
}