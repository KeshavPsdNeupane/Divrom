using System;
using System.Collections.Generic;
using Kope.Component;
using Kope.Core.Collections.Hashes;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Identity;
using Kope.EntityIdentity;

namespace Kope.AI.AIBlackBoard {

	/// <summary>
	/// Manages the operational context for an entity, bridging the gap between 
	/// the entity's internal state and external environmental data.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Self-Context:</b> Provides direct, mutable access to the owning entity's <see cref="ComponentRegistry"/>,
	/// enabling the entity to modify its own internal state.
	/// </para>
	/// <para>
	/// <b>Environmental Awareness:</b> Acts as a facade for multiple specialized databases 
	/// (e.g., Mobs, Props). By managing these databases locally, this class ensures 
	/// a single authoritative source of truth for target data, eliminating the need for 
	/// fragmented caching and simplifying lifecycle synchronization.
	/// </para>
	/// <para>
	/// <b>Safety:</b> External entity data is exposed exclusively through <see cref="IReadOnlyComponentRegistry"/>, 
	/// enforcing strict read-only access to prevent side effects and accidental state corruption.
	/// </para>
	/// </remarks>
	public class Context : IReadOnlyContext {
		private FieldOfViewData _fieldOfViewData;
		private readonly ComponentRegistry _currentEntityContext;
		private readonly MobDatabase _mobDb;
		private readonly PropDatabase _propDb;

		/// <inheritdoc />
		public FieldOfViewData FieldOfViewData => this._fieldOfViewData;

		/// <inheritdoc />
		public IReadOnlyComponentRegistry SelfReadOnlyEntityContext => this._currentEntityContext;

		/// <summary>
		/// Gets the mutable component registry of the owning entity.
		/// </summary>
		public ComponentRegistry CurrentMutableEntityContext => this._currentEntityContext;

		/// <summary>
		/// Initializes a new instance of the <see cref="Context"/> class.
		/// </summary>
		/// <param name="currentEntityContext">The component registry owned by this entity.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="currentEntityContext"/> is null.</exception>
		public Context(ComponentRegistry currentEntityContext) {
			this._currentEntityContext = currentEntityContext ?? throw new ArgumentNullException(nameof(currentEntityContext));

			// Databases are instantiated per-context to maintain authoritative ownership 
			// and simplify lifecycle management.
			this._mobDb = new MobDatabase();
			this._propDb = new PropDatabase();
		}

		/// <summary>
		/// Updates the current field-of-view data for this context.
		/// </summary>
		/// <param name="data">The new FOV information.</param>
		public void SetFieldOfViewData(FieldOfViewData data) => this._fieldOfViewData = data;

		public int TotalSizeOfDatabases() {
			// this will never throw since the databases are always initialized in the constructor
			return this._mobDb.TotalMobs + this._propDb.TotalProps;
		}


		/// <summary>
		/// Registers an entity detail with the appropriate underlying database.
		/// </summary>
		/// <typeparam name="TDetail">The type of the entity detail (e.g., MobEntityDetail or PropEntityDetail).</typeparam>
		/// <param name="detail">The detail object containing entity identification and lifecycle data.</param>
		public void RegisterEntityContext(EntityDetailBase detail) {
			if (detail is MobEntityDetail mob) _mobDb.RegisterMob(mob);
			else if (detail is PropEntityDetail prop) _propDb.RegisterProp(prop);
		}

		/// <summary>
		/// Removes an entity from its respective database, triggering cleanup of cached 
		/// references and lifecycle subscriptions.
		/// </summary>
		/// <typeparam name="TDetail">The type of the entity detail.</typeparam>
		/// <param name="detail">The detail object used to identify the entity for removal.</param>
		public void RemoveEntityContext(EntityDetailBase detail) {
			if (detail is MobEntityDetail mob) _mobDb.RemoveMob(mob);
			else if (detail is PropEntityDetail prop) _propDb.RemoveProp(prop);
		}

		// ---------------------------------------------------------------------
		// Retrieval Logic
		// ---------------------------------------------------------------------

		/// <summary>
		/// Attempts to retrieve a specific entity registry by its type and unique identifier.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This method provides O(1) lookup performance via underlying database indexes.
		/// </para>
		/// <para>
		/// Results are returned via <see cref="IReadOnlyComponentRegistry"/> to enforce 
		/// read-only access and prevent accidental modification of external entity states.
		/// </para>
		///  <b>Note:</b> This method requires an <see cref="MobEntityDetail"/> or 
		/// <see cref="PropEntityDetail"/> struct (as used for registration 
		/// and removal) to provide the necessary identification, such as
		///  the unique identifier and entity type.
		/// </remarks>
		/// <param name="type">The <see cref="EntityType"/> to filter by.</param>
		/// <param name="uid">The unique <see cref="HashedTag"/> of the entity.</param>
		/// <param name="target">When this method returns, holds the registry if found; otherwise, null.</param>
		/// <returns>True if the entity was successfully retrieved; otherwise, false.</returns>
		public bool TryGetTarget(EntityType type, HashedTag uid, out IReadOnlyComponentRegistry target) {
			if (type == EntityType.MOB) return this._mobDb.TryGetMob(uid, out target);
			if (type == EntityType.PROP) return this._propDb.TryGetProp(uid, out target);

			target = null;
			return false;
		}

		/// <summary>
		/// Queries the appropriate database for a collection of entities matching the provided criteria.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Centralizing query logic within the databases eliminates the need for 
		/// per-context cache dictionaries, reducing duplicate references and avoiding 
		/// stale cache synchronization issues.
		/// </para>
		/// <para>
		/// Query results are returned through pre-existing collections, enabling efficient 
		/// retrieval without additional runtime heap allocations.
		/// </para>
		/// </remarks>
		/// <param name="query">The <see cref="EntityQuery"/> criteria.</param>
		/// <param name="targets">When this method returns, contains the list of matching registries.</param>
		/// <returns>True if the query returned results; otherwise, false.</returns>
		public bool TryGetTargets(
			EntityQuery query,
			out IReadOnlyList<IReadOnlyComponentRegistry> targets) {

			if (query.Type == EntityType.MOB) {
				return this._mobDb.TryGetMobs(query.GetMobQuery(), out targets);
			} else if (query.Type == EntityType.PROP) {
				return this._propDb.TryGetProps(query.GetPropQuery(), out targets);
			}

			targets = Array.Empty<IReadOnlyComponentRegistry>();
			return false;
		}
	}
}