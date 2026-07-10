using Kope.Core.Types.Extensions;
using UnityEngine;
namespace Kope.Core.LifeTimeManagement {
	/// <summary>
	/// <br/>
	/// <b>InitializableBase.cs</b><br/>
	/// Convenience base class for MonoBehaviours that participate in InitManager lifecycle.
	/// Derive from this so components automatically implement IInitializable.
	/// Make sure your are placing the Init() call in the correct order in InitLifecycleManager.
	/// U can think of this as Kope's version of MonoBehaviour.Awake/Start but with explicit Init()/Shutdown() calls.
	/// <br/>
	/// <inheritdoc cref="IInitializable"/>
	/// </summary>
	public abstract class InitializableBase : MonoBehaviour, IInitializable {
		/// <summary>
		/// Indicates whether this component has been fully initialized. 
		/// This is set to true after Init() is called and OnInit() returns true.
		/// This garuntees that the component is ready to be used, 
		/// and prevents Update/FixedUpdate logic from running before initialization.
		/// </summary>
		public bool IsInitialized { get; protected set; } = false;

		///<summary> 
		/// this flag is used to log warning only once when Update() is called on uninitialized component
		/// so we dont spam the console every frame
		/// </summary>
		private bool hasLoggedNotInitializedWarning = false;


		private string parentGameObjectStackTrace = string.Empty;

		public string GetParentGameObjectHeirarchyMessage() {
			if (string.IsNullOrEmpty(this.parentGameObjectStackTrace)) {
				this.parentGameObjectStackTrace = this.GetGameObjectHierarchyPath();
				if (string.IsNullOrEmpty(this.parentGameObjectStackTrace)) {
					this.parentGameObjectStackTrace = "Could not determine GameObject hierarchy.";

				}
			}
			return $" (GameObjectPath): {this.parentGameObjectStackTrace}";
		}

		/// <summary>
		/// Sets the IsInitialized boolean value.
		/// Default is true, set to false to mark uninitialized.
		/// Use with caution; prefer calling Init()/Shutdown() instead.
		/// </summary>
		/// <param name="value"></param>
		public void SetInitBoolean(bool value = true) => this.IsInitialized = value;

		public void Init() {
			try {
				if (this.IsInitialized) return;
				this.parentGameObjectStackTrace = this.GetGameObjectHierarchyPath();
				this.hasLoggedNotInitializedWarning = false;

				this.IsInitialized = OnInit();
			} catch (System.Exception ex) {
				Debug.LogError($"Exception during Init of {this.GetType().Name} on GameObject {gameObject.name}: {ex}");
				this.IsInitialized = false;
			}
		}

		public void CheckInit() {
			if (!this.IsInitialized) {
				Debug.LogWarning($"[The component {this.GetType().Name}] has not been initialized. " +
				$"Call Init() on {this.GetParentGameObjectHeirarchyMessage()}."
				+ $" Is it registered in InitLifecycleManager?", this);
			}
		}

		/// <summary>
		/// Called during initialization. Override this instead of Init().
		/// Init() will call this after setting IsInitialized = true.
		/// The base implementation does nothing.
		/// this method is completely optional to override.
		/// Just being used as Template Method pattern.
		/// so child classes can hook into Init without overriding it.
		/// </summary>
		protected virtual bool OnInit() {
			return true;
		}


		protected virtual void OnDestroy() {
			if (!this.IsInitialized) return;
			// why not onDisable? because OnDisable is called when the object is disabled, 
			// but OnDestroy is called when the object is destroyed.
			// we want to make sure that the object is fully destroyed before we mark it as uninitialized, 
			// so that we don't have any race conditions with
			// tech not needed to set this variables since if this is going to be destroyed, it 
			// will be destroyed anyway, but just to be safe.
			this.IsInitialized = false;
			this.hasLoggedNotInitializedWarning = false;
			this.parentGameObjectStackTrace = string.Empty;
		}



		/// <summary>
		/// Update method called every frame.
		/// Do NOT override Update() directly. Override OnUpdate() instead.
		/// </summary>
		protected void Update() {
			if (!this.IsInitialized) {
				if (!this.hasLoggedNotInitializedWarning) {
					this.parentGameObjectStackTrace = this.GetGameObjectHierarchyPath();
					this.hasLoggedNotInitializedWarning = true;
					Debug.LogWarning($"[{this.GetType().Name}] Update called " +
					$"before Init() on {this.parentGameObjectStackTrace}. Is " +
					"it registered in InitLifecycleManager?", this);
				}
				return;
			}
			OnUpdate();
		}

		protected void FixedUpdate() {
			if (!this.IsInitialized) return;
			OnFixedUpdate();
		}

		/// <summary>
		/// Called every frame after IsInitialized check. Override this instead of Update().
		/// The base implementation does nothing.
		/// this method is completely optional to override.
		/// Just being used as Template Method pattern.
		/// so child classes can hook into Update without overriding it.
		/// </summary>
		protected virtual void OnUpdate() { }

		/// <summary>
		/// Called every fixed frame after IsInitialized check. Override this instead of FixedUpdate().
		/// The base implementation does nothing.
		/// this method is completely optional to override.
		/// Just being used as Template Method pattern.
		/// so child classes can hook into FixedUpdate without overriding it.
		/// </summary>
		protected virtual void OnFixedUpdate() { }


	}


	/// <summary>
	/// <br/>
	/// <b>InitializableBaseNew.cs</b><br/>
	/// This abstract class acts as a convenient foundation for all MonoBehaviours participating in the InitManager lifecycle.
	/// It is explicitly designed as an abstract class because manually implementing <see cref="IInitializable"/> across 
	/// dozens of individual components would introduce annoying, redundant boilerplate code. 
	/// <br/><br/>
	/// This abstracts away state tracking (<see cref="IsInitialized"/>), safety verification (<see cref="CheckInit"/>), 
	/// and path-based hierarchy exception logging.
	/// <br/><br/>
	/// Think of this as Kope's explicit version of MonoBehaviour.Awake/Start. 
	/// Note that frame updates (<c>Update</c>/<c>FixedUpdate</c>) are deliberately excluded here to eliminate Unity's 
	/// native dynamic dispatch overhead; introduce update functionality to your child classes by implementing 
	/// <c>IUpdatable</c> or <c>IFixedUpdatable</c> interfaces instead.
	/// <br/>
	/// </summary>
	public abstract class InitializableBaseNew : MonoBehaviour, IInitializable {
		/// <summary>
		/// Indicates whether this component has been successfully initialized.
		/// Automatically set to true after <see cref="Init"/> runs and the underlying <see cref="OnInit"/> sequence returns true.
		/// Use this flag to safely prevent external gameplay modules from accessing this component before it is fully ready.
		/// </summary>
		public bool IsInitialized { get; protected set; } = false;

		/// <summary>
		/// Cached string representation of the full GameObject transform tree route to this component.
		/// </summary>
		private string parentGameObjectStackTrace = string.Empty;

		/// <summary>
		/// Generates or retrieves a formatted string representing the deep transform hierarchy path of this GameObject.
		/// Used primarily to make debugging and lifecycle mismatch tracking easy in complex scenes.
		/// </summary>
		public string GetParentGameObjectHeirarchyMessage() {
			if (string.IsNullOrEmpty(this.parentGameObjectStackTrace)) {
				this.parentGameObjectStackTrace = this.GetGameObjectHierarchyPath();
				if (string.IsNullOrEmpty(this.parentGameObjectStackTrace)) {
					this.parentGameObjectStackTrace = "Could not determine GameObject hierarchy.";
				}
			}
			return $" (GameObjectPath): {this.parentGameObjectStackTrace}";
		}

		/// <summary>
		/// Directly updates the underlying initialization boolean state.
		/// Primarily utilized by external custom lifecycle configurations or unit tests. Use with caution.
		/// </summary>
		public void SetInitBoolean(bool value = true) => this.IsInitialized = value;

		/// <summary>
		/// Core lifecycle execution method called by the framework's central lifecycle manager.
		/// Handles safety checks, records the hierarchy trace path, and evaluates the user-defined <see cref="OnInit"/> logic.
		/// Wraps child implementation routines in standard try-catch blocks to keep lifecycle setup failures isolated.
		/// </summary>
		public void Init() {
			try {
				if (this.IsInitialized) return;
				this.parentGameObjectStackTrace = this.GetGameObjectHierarchyPath();
				this.IsInitialized = OnInit();
			} catch (System.Exception ex) {
				Debug.LogError($"Exception during Init of {this.GetType().Name} on GameObject {gameObject.name}: {ex}");
				this.IsInitialized = false;
			}
		}

		/// <summary>
		/// One-shot diagnostic call to evaluate whether this component was missed during the system setup execution sequence.
		/// Logs a detailed warnings tracking path if called while <see cref="IsInitialized"/> remains false.
		/// </summary>
		public void CheckInit() {
			if (!this.IsInitialized) {
				Debug.LogWarning($"[The component {this.GetType().Name}] has not been initialized. " +
				$"Call Init() on {this.GetParentGameObjectHeirarchyMessage()}." +
				$" Is it registered in InitLifecycleManager?", this);
			}
		}

		/// <summary>
		/// Framework Template Method hook. Child classes should override this method to perform their custom setup logic.
		/// Defaults to returning true, which marks the component initialization sequence as successful.
		/// Overriding this method is entirely optional.
		/// </summary>
		protected virtual bool OnInit() {
			return true;
		}
	}
}


