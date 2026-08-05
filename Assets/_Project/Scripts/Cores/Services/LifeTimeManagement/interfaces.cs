
namespace Kope.Core.LifeTimeManagement {
	using System.Collections.Generic;
	public interface IInitializable {
		/// <summary>
		/// Gets a value indicating whether this instance is initialized.
		/// </summary>
		bool IsInitialized { get; }
		/// <summary>
		/// Initializes the component.
		/// </summary>
		void Init();
		/// <summary>
		/// Checks the initialization status of the component.
		/// </summary>
		void CheckInit();

	}
	/// <summary>
	/// Interface for components that require update logic. Implementing this interface allows the
	/// component to be managed by a lifecycle manager that handles updates.<br/>
	/// Far superior to using Unity's Update() method, as it allows for better control 
	/// over the update order and can be paused or stopped as needed. And there is no wild west of Update()
	///  methods being called on every MonoBehaviour in the scene, which can lead to performance 
	/// issues and hard-to-track bugs.
	/// </summary>
	public interface IUpdatable {
		/// <summary>
		/// Called every frame by the lifecycle manager to update the component's state.
		/// Implement this method to define the component's behavior during the update cycle.
		/// </summary>
		void OnUpdate();
	}
	/// <summary>
	/// Interface for components that require fixed update logic. Implementing this interface allows
	/// the component to be managed by a lifecycle manager that handles fixed updates. <br/>
	/// Far superior to using Unity's FixedUpdate() method, as it allows for better control
	/// over the update order and can be paused or stopped as needed. And there is no wild west 
	/// of FixedUpdate() methods being called on every MonoBehaviour in the scene, which can lead
	/// to performance issues and hard-to-track bugs.
	/// </summary>
	public interface IFixedUpdatable {
		/// <summary>
		/// Called at a fixed interval by the lifecycle manager to update the component's state.
		/// Implement this method to define the component's behavior during the fixed update cycle.
		/// </summary>
		void OnFixedUpdate();
	}

	/// <summary>
	/// Interface for components that can contain nested InitializableBaseNew components.
	/// This allows for hierarchical initialization and update management within a parent component.
	/// </summary>
	public interface IInitializableContainer {
		IEnumerable<InitializableBase> GetNestedComponents();
	}
}