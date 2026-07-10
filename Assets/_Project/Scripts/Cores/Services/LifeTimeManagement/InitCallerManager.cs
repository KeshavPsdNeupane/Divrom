using System.Collections.Generic;
using UnityEngine;
using ZLinq;
using Kope.Core.Execution;

namespace Kope.Core.LifeTimeManagement {
	/// <summary>
	/// Simple manager that only calls Init()/Shutdown() on listed IInitializable components.
	/// No DI, no injection — just lifecycle ordering by the `initializables` list.
	/// </summary>

	[CustomExecutionOrder(-30)]
	public class InitLifecycleManager : InitializableBaseNew {
		[Tooltip("Order matters: earlier items initialize first.")]
		public List<InitializableBase> initializables = new();
		[Tooltip("If true, Init() is called in Awake(). Otherwise, Init() must be called manually.")]
		[SerializeField] private bool canCallInAwake = true;

		[Tooltip("If true, auto-populate `initializables` from this GameObject (and optionally children) before Init runs.")]
		[SerializeField] private bool autoPopulate = false;

		[Tooltip("If true, include child GameObjects when auto-populating.")]
		[SerializeField] private bool includeChildren = true;

		public enum TraversalMode {
			ParentFirst,
			ChildrenFirst,
			SiblingPath,
		}

		[Tooltip("How discovered components are ordered when populating the `initializables` list.")]
		public TraversalMode traversal = TraversalMode.ParentFirst;

		private readonly List<IInitializable> _initializable = new();
		private readonly List<IUpdatable> _updatables = new();
		private readonly List<IFixedUpdatable> _fixedUpdatables = new();

		protected virtual void Awake() {
			if (this.canCallInAwake) {
				Init();
			}
		}

		protected override bool OnInit() {

			try {
				if (this.autoPopulate)
					PopulateInitializables();

				this._initializable.Clear();
				foreach (var mono in this.initializables) {
					if (mono == null) continue;
					if (mono is IInitializable initable) {
						if (initable.IsInitialized) {
							Debug.LogWarning($"{mono.name} is already initialized " +
							" and will be skipped by InitCallerManager.");
							continue;
						}
						if (!this._initializable.Contains(initable)) {
							this._initializable.Add(initable);
							if (initable is IUpdatable updatable && !this._updatables.Contains(updatable)) {
								this._updatables.Add(updatable);
							}
							if (initable is IFixedUpdatable fixedUpdatable && !this._fixedUpdatables.Contains(fixedUpdatable)) {
								this._fixedUpdatables.Add(fixedUpdatable);
							}
						}

					} else {
						Debug.LogWarning($"{mono.name} does not implement IInitializable and will be skipped by InitCallerManager.");
					}
				}

				// Call Init in order
				foreach (var item in this._initializable) {
					try { item.Init(); } catch (System.Exception ex) {
						Debug.LogError($"InitCallerManager: " +
					   $"Exception in Init of {item.GetType().Name}: {ex}");
					}
				}
				return true;
			} catch (System.Exception ex) {
				Debug.LogError($"InitCallerManager: Exception during OnInit: {ex}" + GetParentGameObjectHeirarchyMessage());
				return false;
			}
		}

		[ContextMenu("Populate Initializables")]
		public void PopulateInitializables() {
			IEnumerable<InitializableBase> found = this.includeChildren
				? GetComponentsInChildren<InitializableBase>(false)
				: GetComponents<InitializableBase>();

			// Exclude this manager if present
			var list = found.AsValueEnumerable().Where(c => c != this).ToList();
			IEnumerable<InitializableBase> orderedList = this.traversal switch {
				TraversalMode.ChildrenFirst => list.AsValueEnumerable().OrderByDescending(m =>
								GetDepth(m.transform, this.transform)).ToList(),
				TraversalMode.SiblingPath => list.AsValueEnumerable().OrderBy(m =>
								GetSiblingPathKey(m.transform, this.transform)).ToList(),
				_ => list,
			};

			this.initializables.Clear();
			foreach (var mb in orderedList.AsValueEnumerable().OfType<InitializableBase>())
				this.initializables.Add(mb);
		}

		[ContextMenu("Debug: Print Init Tree")]
		public void DebugInitTree() {
			// for not this is commented until the migration to the new InitializableBaseNew is complete and tested.

			// var sb = new System.Text.StringBuilder();
			// sb.AppendLine($"=== Init Tree: {this.gameObject.name} ===");
			// sb.AppendLine($"CanCallInAwake: {this.canCallInAwake}");
			// sb.AppendLine($"Initializables ({this.initializables.Count}):");

			// for (int i = 0; i < this.initializables.Count; i++) {
			// 	var item = this.initializables[i];
			// 	if (item == null) {
			// 		sb.AppendLine($"  [{i}] <null>");
			// 		continue;
			// 	}

			// 	var isManager = item is InitLifecycleManager;
			// 	string marker = isManager ? " [Manager]" : "";

			// 	sb.AppendLine($"  [{i}] {item.GetType().Name} ({item.gameObject.name}){marker}");

			// 	// If it's a nested manager, show its children indented
			// 	if (isManager) {
			// 		var nestedManager = (InitLifecycleManager)item;
			// 		for (int j = 0; j < nestedManager.initializables.Count; j++) {
			// 			var child = nestedManager.initializables[j];
			// 			if (child == null) {
			// 				sb.AppendLine($"      [{j}] <null>");
			// 			} else {
			// 				var isNestedManager = child is InitLifecycleManager;
			// 				string nestedMarker = isNestedManager ? " [Manager]" : "";
			// 				sb.AppendLine($"      [{j}] {child.GetType().Name} ({child.gameObject.name}){nestedMarker}");
			// 			}
			// 		}
			// 	}
			// }

			// sb.AppendLine("===================");
			// Debug.Log(sb.ToString());
		}

		private int GetDepth(Transform t, Transform root) {
			int d = 0;
			while (t != null && t != root) {
				d++;
				t = t.parent;
			}
			return d;
		}

		private string GetSiblingPathKey(Transform t, Transform root) {
			var parts = new List<int>();
			while (t != null && t != root) {
				parts.Add(t.GetSiblingIndex());
				t = t.parent;
			}
			parts.Reverse();
			return string.Join(".", parts);
		}
	}
}