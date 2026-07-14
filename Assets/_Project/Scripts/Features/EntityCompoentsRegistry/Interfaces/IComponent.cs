using Kope.Core.LifeTimeManagement;

namespace Kope.EntityComponentSystem {
	/// <summary>
	/// Represents a data component within the Kope ECS framework.
	/// </summary>
	/// <remarks>
	/// This is a marker interface used to identify types that can be stored, queried,
	/// and managed by the ECS. It intentionally defines no members, allowing components
	/// to remain lightweight and focused solely on holding data.
	/// <br/><br/>
	/// Components are typically implemented as small, modular types that describe a
	/// specific aspect of an entity, such as health, movement, inventory, or state.
	/// </remarks>
	public interface IComponent : IInitializable { }

	/// <summary>
	/// Serves as the mandatory concrete serialization bridge and lifecycle foundation 
	/// for all Kope ECS components.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Because Unity's built-in serializer cannot natively serialize interface fields, 
	/// this class acts as the concrete base type required to expose components in the 
	/// Inspector or persist them through Unity's serialization system.
	/// </para>
	/// <para>
	/// <b>Architectural Justification (Inheritance vs. Composition):</b><br/>
	/// While composition is often preferred for behavioral reuse, inheritance is intentionally 
	/// utilized here. Initialization is not an optional, composable capability—it is a fundamental, 
	/// non-negotiable requirement of every single ECS component. 
	/// </para>
	/// <para>
	/// Because all components share the exact same setup states, safety try-catch loops, 
	/// path-based hierarchy exception logging, and post-initialization validation, centralizing 
	/// this engine in a common base class completely eliminates redundant boilerplate. Relying solely 
	/// on raw interface implementation would force every component to copy-paste state-tracking and 
	/// logging code. 
	/// </para>
	/// <para>
	/// Given that Unity serialization already demands a concrete base class type, utilizing 
	/// <see cref="ComponentBase"/> to inherit from <see cref="InitializableBase"/> represents the 
	/// simplest, most maintainable, and most robust solution for enforcing a unified lifecycle.
	/// </para>
	/// <para>
	/// <b>Lifecycle Hierarchy:</b>
	/// <list type="bullet">
	/// <item>
	/// <description><see cref="IInitializable"/> is a primitive, low-level contract. System managers 
	/// or data assets can be initializable <i>without</i> being ECS components.</description>
	/// </item>
	/// <item>
	/// <description>A component, however, can never bypass the explicit initialization loop. All 
	/// components must derive from this class to guarantee consistent lifecycle integration and 
	/// diagnostics.</description>
	/// </item>
	/// </list>
	/// </para>
	/// <para>
	///  <b>IMPORTANT ARCHITECTURAL ADVICE</b>:<br/>
	/// Never inherit from <see cref="ComponentBase"/> or implement <see cref="IComponent"/> on a UI script. 
	/// User Interface (UI) elements exist outside the domain boundaries of the ECS system. They are presentation 
	/// layer scripts, not gameplay entity components, and should not be registered or treated as part of the core ECS.
	/// </para>
	/// </remarks>
	public abstract class ComponentBase : InitializableBase, IComponent { }
}