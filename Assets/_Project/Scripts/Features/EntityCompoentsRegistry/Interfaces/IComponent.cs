namespace Kope.EntityComponentSystem {
	/// <summary>
	/// A lightweight marker interface that identifies a type as an ECS component.
	/// </summary>
	/// <remarks>
	/// This interface serves as a common base allowing the ECS framework to recognize, 
	/// register, and manage components. Since it defines no members, it can be implemented 
	/// by lightweight structs or classes to define custom, modular data structures.
	/// </remarks>
	public interface IComponent { }


	/// <summary>
	/// An abstract base class implementation of <see cref="IComponent"/> used to enable 
	/// Unity Inspector serialization.
	/// </summary>
	/// <remarks>
	/// Because Unity's built-in serializer cannot natively serialize interface fields, 
	/// this class acts as a serializable concrete wrapper. Component classes requiring 
	/// exposure in the Unity Inspector should inherit from this base instead of 
	/// implementing <see cref="IComponent"/> directly.
	/// </remarks>
	public abstract class ComponentBase : IComponent { }
}