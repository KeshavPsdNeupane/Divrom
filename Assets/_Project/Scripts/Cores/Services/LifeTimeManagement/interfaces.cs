
namespace Kope.Core.LifeTimeManagement
{
	using System.Collections.Generic;
	public interface IInitializable
	{

		bool IsInitialized { get; }
		void Init();
		void CheckInit();

	}

	public interface IUpdatable
	{
		void OnUpdate();
	}
	/// <summary>
	/// Interface for components that require fixed update logic. Implementing this interface allows the component to be managed by a lifecycle manager that handles fixed updates.
	/// </summary>
	public interface IFixedUpdatable
	{
		void OnFixedUpdate();
	}

	/// <summary>
	/// Interface for components that can contain nested InitializableBaseNew components.
	/// This allows for hierarchical initialization and update management within a parent component.
	/// </summary>
	public interface IInitializableContainer
	{
		IEnumerable<InitializableBase> GetNestedComponents();
	}
}