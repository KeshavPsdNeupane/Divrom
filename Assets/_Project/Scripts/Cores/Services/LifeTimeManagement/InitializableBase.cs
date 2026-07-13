using Kope.Core.Collections.Extensions;
using UnityEngine;
namespace Kope.Core.LifeTimeManagement {

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
	public abstract class InitializableBase : MonoBehaviour, IInitializable {
		/// <summary>
		/// Indicates whether this component has been successfully initialized.
		/// Automatically set to true after <see cref="Init"/> runs and the underlying <see cref="OnInit"/> sequence returns true.
		/// Use this flag to safely prevent external gameplay modules from accessing this component before it is fully ready.
		/// </summary>
		public bool IsInitialized { get; protected set; } = false;

		/// <summary>
		/// Cached string representation of the full GameObject transform tree route to this component.
		/// </summary>
		private string _hiearchyPath = string.Empty;
		/// <summary>
		/// Returns a formatted string representing the full GameObject transform hierarchy 
		/// path to this component.
		/// This is primarily used for debugging and logging purposes, especially when tracking down 
		/// initialization issues in complex scenes with many nested GameObjects.
		/// </summary>
		public string HieararchyPath {
			get {
				if (string.IsNullOrEmpty(this._hiearchyPath)) {
					GeneratePath();
					if (string.IsNullOrEmpty(this._hiearchyPath)) {
						this._hiearchyPath = "Could not determine GameObject hierarchy.";
					}
				}
				return this._hiearchyPath;
			}
		}


		#region Lifecycle Management
		protected virtual void OnDestroy() {
			if (!this.IsInitialized) return;
			this.IsInitialized = false;
			this._hiearchyPath = string.Empty;
		}
		#endregion

		#region IInitializable Implementation
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
				GeneratePath();
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
		public virtual void CheckInit() {
			if (!this.IsInitialized) {
				Debug.LogWarning($"[The component {this.GetType().Name}] has not been initialized. " +
				$"Call Init() on {this.HieararchyPath}." +
				$" Is it registered in InitLifecycleManager?", this);
			}
		}
		#endregion

		#region  Template Method Hook
		/// <summary>
		/// Framework Template Method hook. Child classes should override this method to perform their custom setup logic.
		/// Defaults to returning true, which marks the component initialization sequence as successful.
		/// Overriding this method is entirely optional.
		/// </summary>
		protected virtual bool OnInit() {
			return true;
		}
		#endregion


		#region  Helper Methods
		private void GeneratePath() {
			this._hiearchyPath = $"(GameObject):{this.GetGameObjectHierarchyPath()}";
		}
		#endregion
	}
}


