using System;
using System.Collections.Generic;
using Kope.AI;
using Kope.Component;
using Kope.Core.Collections.Hashes;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Identity;
using Kope.EntityIdentity;

namespace Kope.AI.ContextNew {

	/// <summary>
	/// Stores the operational context of an entity along with access to its known targets.
	/// <para>
	/// <b>Self Context:</b> Exposes mutable access to the owning entity's registry,
	/// allowing the entity to update its own state.
	/// </para>
	/// <para>
	/// <b>Target Access:</b> Uses dedicated databases to provide read-only views of
	/// other entities. Target data is not locally duplicated or cached within the
	/// context, ensuring a single authoritative source of truth while minimizing
	/// memory overhead and synchronization complexity.
	/// </para>
	/// </summary>
	public class ContextNew : IReadOnlyContextNew {
		private FieldOfViewData _fieldOfViewData;
		private readonly ComponentRegistry _currentEntityContext;
		private readonly MobDatabase _mobDb;
		private readonly PropDatabase _propDb;

		public FieldOfViewData FieldOfViewData => this._fieldOfViewData;
		public IReadOnlyComponentRegistry SelfReadOnlyEntityContext => this._currentEntityContext;

		/// <summary>
		/// Provides mutable access to the current entity's component registry.
		/// Used when the owning entity needs to update or mutate its own state.
		/// </summary>
		public ComponentRegistry CurrentMutableEntityContext => this._currentEntityContext;

		public ContextNew(ComponentRegistry currentEntityContext) {
			this._currentEntityContext = currentEntityContext ?? throw new ArgumentNullException(nameof(currentEntityContext));

			// Each entity owns its own Context instance, so these databases are intentionally
			// created per context rather than shared globally. The databases are lightweight,
			// making the memory cost negligible while simplifying ownership and lifecycle
			// management. This design also preserves a single authoritative source of target
			// data within each context.
			this._mobDb = new MobDatabase();
			this._propDb = new PropDatabase();
		}

		public void SetFieldOfViewData(FieldOfViewData data) => this._fieldOfViewData = data;


		/// <summary>
		/// Registers an entity with the appropriate database.
		/// The database is responsible for maintaining internal caches and managing
		/// lifecycle event subscriptions required for automatic cleanup.
		/// </summary>
		public void RegisterEntity<TDetail>(TDetail detail) where TDetail : class {
			if (detail is MobEntityDetail mob) _mobDb.RegisterMob(mob);
			else if (detail is PropEntityDetail prop) _propDb.RegisterProp(prop);
		}

		/// <summary>
		/// Removes an entity from the appropriate database.
		/// This also removes any cached references and releases lifecycle subscriptions
		/// maintained by the database.
		/// </summary>
		public void RemoveEntity<TDetail>(TDetail detail) where TDetail : class {
			if (detail is MobEntityDetail mob) _mobDb.RemoveMob(mob);
			else if (detail is PropEntityDetail prop) _propDb.RemoveProp(prop);
		}

		// ---------------------------------------------------------------------
		// Retrieval Logic
		// ---------------------------------------------------------------------

		/// <summary>
		/// Routes a target lookup request to the appropriate database based on entity type.
		/// <para>
		/// Lookups are expected to be O(1) through the underlying database indexes.
		/// </para>
		/// <para>
		/// Targets are returned through the IReadOnlyComponentRegistry contract to
		/// communicate read-only intent and discourage accidental modification of
		/// external entity state.
		/// </para>
		/// </summary>
		public bool TryGetTarget(EntityType type, HashedTag uid, out IReadOnlyComponentRegistry target) {
			if (type == EntityType.MOB) return this._mobDb.TryGetMob(uid, out target);
			if (type == EntityType.PROP) return this._propDb.TryGetProp(uid, out target);

			target = null;
			return false;
		}

		/// <summary>
		/// Routes a query request to the appropriate database and returns the matching
		/// target collection.
		/// <para>
		/// Centralizing query storage within the databases eliminates the need for
		/// per-context cache dictionaries, reducing duplicate references and avoiding
		/// stale cache synchronization issues.
		/// </para>
		/// <para>
		/// Query results are returned through pre-existing collections, enabling efficient
		/// retrieval without additional runtime allocations.
		/// </para>
		/// </summary>
		public bool TryGetTargets<TQuery>(
			EntityType type,
			TQuery query,
			out IReadOnlyList<IReadOnlyComponentRegistry> targets)
			where TQuery : struct {

			if (type == EntityType.MOB && query is MobQuery mobQuery) {
				return this._mobDb.TryGetMobs(mobQuery, out targets);
			}

			if (type == EntityType.PROP && query is PropQuery propQuery) {
				return this._propDb.TryGetProps(propQuery, out targets);
			}

			targets = Array.Empty<IReadOnlyComponentRegistry>();
			return false;
		}
	}
}