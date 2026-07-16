using System.Collections.Generic;
using Kope.Component;
using Kope.Core.Collections.Hashes;
using Kope.Core.EntityComponentRegistry;
using Kope.EntityIdentity;

namespace Kope.AI.ContextNew {

	/// <summary>
	/// Provides read-only access to an entity's context and its known targets.
	/// <para>
	/// <b>IMPORTANT:</b> This is a reference-based contract, not an immutable one.
	/// Although this interface only exposes read-only members, the underlying objects
	/// are still mutable reference types. Modifying data obtained through this interface
	/// violates the intended ECS architecture and may result in unpredictable or
	/// non-deterministic AI behavior.
	/// </para>
	/// <para>
	/// This interface exists to communicate and enforce read-only intent at the API level.
	/// Any code that deliberately circumvents this contract assumes responsibility for
	/// any resulting side effects.
	/// </para>
	/// </summary>
	public interface IReadOnlyContextNew {
		FieldOfViewData FieldOfViewData { get; }

		/// <summary>
		/// Provides read-only access to the current entity's component registry.
		/// <para>
		/// The returned object is a reference type and therefore not truly immutable.
		/// Consumers should treat the registry as read-only and must not modify its state
		/// through this reference. Doing so violates the intended contract.
		/// </para>
		/// </summary>
		IReadOnlyComponentRegistry SelfReadOnlyEntityContext { get; }

		/// <summary>
		/// Attempts to retrieve a read-only target context associated with the specified
		/// entity type and unique identifier.
		/// <para>
		/// Returns true when the target is found; otherwise false.
		/// </para>
		/// <para>
		/// The returned registry is exposed through a read-only contract, but the underlying
		/// object remains mutable. Consumers must not modify the target through this reference.
		/// </para>
		/// </summary>
		bool TryGetTarget(EntityType type, HashedTag uid, out IReadOnlyComponentRegistry target);

		/// <summary>
		/// Attempts to retrieve a collection of read-only target contexts matching the
		/// specified entity type and query.
		/// <para>
		/// Returns true when matching targets are found; otherwise false.
		/// </para>
		/// <para>
		/// The returned registries are exposed through a read-only contract, but the
		/// underlying objects remain mutable. Consumers must not modify target state
		/// through these references.
		/// </para>
		/// </summary>
		bool TryGetTargets<TQuery>(EntityType type, TQuery query, out IReadOnlyList<IReadOnlyComponentRegistry> targets)
			where TQuery : struct;
	}
}