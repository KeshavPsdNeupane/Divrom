using Kope.Core.Extensions;
using UnityEngine;
namespace Kope.Core.Init {
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
		/// this bool just means whether Init() has been called or not yet for the instance
		/// it does not guarantee that all dependencies are injected or valid
		/// that is up to the derived class to ensure during its Init() implementation   
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
			if (this.IsInitialized) return;
			this.parentGameObjectStackTrace = this.GetGameObjectHierarchyPath();
			this.hasLoggedNotInitializedWarning = false;
			// call the virtual OnInit for actual initialization logic, and 
			// set IsInitialized based on its return value.
			this.IsInitialized = OnInit();
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


		public void Shutdown() {
			if (!this.IsInitialized) return;
			this.IsInitialized = false;
			this.hasLoggedNotInitializedWarning = false;
			this.parentGameObjectStackTrace = string.Empty;
			OnShutdown();
		}
		/// <summary>
		/// Called during shutdown. Override for teardown.
		/// Always call base.Shutdown() to set IsInitialized = false.
		/// Completely optional to override.
		/// </summary>
		protected virtual void OnShutdown() { }


		/// <summary>
		/// Update method called every frame.
		/// Do NOT override Update() directly. Override OnUpdate() instead.
		/// </summary>
		protected void Update() {
			if (!this.IsInitialized) {
				if (!this.hasLoggedNotInitializedWarning) {
					// calling FindAllParentStackString every frame is expensive, so we only do it once when we log the warning for the first time.
					// for now calling it again in this if so that we can get the correct stack trace even if the hierarchy changes after initialization. since we only log once, it should be fine.
					this.parentGameObjectStackTrace = this.GetGameObjectHierarchyPath();
					this.hasLoggedNotInitializedWarning = true;
				}
				return;
			}
			OnUpdate();
		}

		protected void FixedUpdate() {
			// just return the Update gives the error log, no need to also log in FixedUpdate. 
			// since usually if Update is not initialized, FixedUpdate will also not be initialized.
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
}