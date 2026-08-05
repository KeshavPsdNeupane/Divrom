using System.Collections.Generic;
using UnityEngine;
using ZLinq;
using Kope.Core.Execution;

namespace Kope.Core.LifeTimeManagement {
	public abstract class LifecycleManagerBase : InitializableBase {
		[Header("Manager Update Settings")]
		[Tooltip("If true, this manager hooks directly into Unity's Awake/Update/FixedUpdate loops. " +
				 "Set to false if a parent manager is driving this manager instead.")]
		[SerializeField] protected bool canSelfServe = true;
	}

	/// <summary>
	/// Prefab-level manager that handles explicit initialization ordering 
	/// and manages direct centralized update loops for registered scoped components.
	/// </summary>
	[CustomExecutionOrder(-30)]
	public class LifecycleManager : LifecycleManagerBase, IUpdatable, IFixedUpdatable {
		[Header("Lifecycle Target Configuration")]
		[Tooltip("Order matters: earlier items initialize first.")]
		public List<InitializableBase> initializables = new();

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
			if (this.canSelfServe) {
				Init();
				CheckInit();
			}
		}

		protected virtual void Update() {
			if (this.canSelfServe) {
				OnUpdate();
			}
		}

		protected virtual void FixedUpdate() {
			if (this.canSelfServe) {
				OnFixedUpdate();
			}
		}

		/// <summary>
		/// Centralized frame update loop. Executes pure interface iterations.
		/// Can be called directly by parent managers if this instance is nested.
		/// </summary>
		public void OnUpdate() {
			if (!this.IsInitialized) return;
			int count = _updatables.Count;
			for (int i = 0; i < count; i++) {
				this._updatables[i].OnUpdate();

			}
		}

		/// <summary>
		/// Centralized fixed frame update loop. Executes pure interface iterations.
		/// Can be called directly by parent managers if this instance is nested.
		/// </summary>
		public void OnFixedUpdate() {
			if (!this.IsInitialized) return;
			int count = _fixedUpdatables.Count;
			for (int i = 0; i < count; i++) {
				_fixedUpdatables[i].OnFixedUpdate();
			}
		}

		protected override bool OnInit() {
			try {
				if (this.autoPopulate)
					PopulateInitializables();

				this._initializable.Clear();
				this._updatables.Clear();
				this._fixedUpdatables.Clear();

				// Phase 1: Filter and extract distinct implementations (Flattening Containers)
				foreach (var mono in this.initializables) {
					if (mono == null) continue;

					// Direct Registration Pass
					ProcessAndRegisterInitializable(mono);
				}

				// Phase 2: Sequence initialization across the full flattened topology
				foreach (var item in this._initializable) {
					try {
						if (item == null || item.IsInitialized) continue;
						item.Init();
						// Cache update targets ONLY if the component initialized successfully
						if (item.IsInitialized) {
							if (item is IUpdatable updatable) this._updatables.Add(updatable);
							if (item is IFixedUpdatable fixedUpdatable) this._fixedUpdatables.Add(fixedUpdatable);

						} else {
							Debug.LogWarning($"[{item.GetType().Name}] failed to initialize properly and will be completely excluded from update loops.", (MonoBehaviour)item);
						}
					} catch (System.Exception ex) {
						Debug.LogError($"InitLifecycleManager: Exception in Init of {item.GetType().Name}: {ex}");
					}
				}

				return true;
			} catch (System.Exception ex) {
				Debug.LogError($"InitLifecycleManager: Exception during OnInit: {ex}-{this.HieararchyPath}");
				return false;
			}
		}


		public override void CheckInit() {
			base.CheckInit();
			foreach (var item in this._initializable) {
				item.CheckInit();
			}
		}

		/// <summary>
		/// Registers components to the internal execution sequence. Unpacks sub-components if target is a container.
		/// </summary>
		private void ProcessAndRegisterInitializable(InitializableBase target) {
			if (target is IInitializable initable) {
				if (initable.IsInitialized) {
					Debug.LogWarning($"{target.name} is already initialized and will be skipped by InitLifecycleManager.");
					return;
				}

				// If it is a container (like ECR), we process the container itself first 
				// so it can register dependencies internally
				if (!this._initializable.Contains(initable)) {
					this._initializable.Add(initable);
				}

				// Dig out nested elements to register them to the central initialization tracking loop
				if (target is IInitializableContainer container) {
					foreach (var subComp in container.GetNestedComponents()) {
						if (subComp == null) continue;
						ProcessAndRegisterInitializable(subComp);
					}
				}
			} else {
				Debug.LogWarning($"{target.name} does not implement IInitializable and will be skipped by InitLifecycleManager.");
			}
		}

		[ContextMenu("Populate Initializables")]
		public void PopulateInitializables() {
			IEnumerable<InitializableBase> found = this.includeChildren
				? GetComponentsInChildren<InitializableBase>(false)
				: GetComponents<InitializableBase>();

			var list = found.AsValueEnumerable().Where(c => c != this).ToList();
			IEnumerable<InitializableBase> orderedList = this.traversal switch {
				TraversalMode.ChildrenFirst => list.AsValueEnumerable().OrderByDescending(m =>
								GetDepth(m.transform, this.transform)).ToList(),
				TraversalMode.SiblingPath => list.AsValueEnumerable().OrderBy(m =>
								GetSiblingPathKey(m.transform, this.transform)).ToList(),
				_ => list,
			};

			this.initializables.Clear();
			foreach (var mb in orderedList)
				this.initializables.Add(mb);
		}

		[ContextMenu("Debug: Print Init Tree")]
		public void DebugInitTree() {
			// Uncomment when needed for testing tree rendering
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