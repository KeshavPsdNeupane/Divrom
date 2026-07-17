using System;
using System.Collections.Generic;
using Kope.Core.Collections.Hashes;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Identity;
using Kope.EntityIdentity;

namespace Kope.AI {

	/// <summary>
	/// Represents a categorical query used to retrieve collections of props.
	/// <para>
	/// This query is intentionally limited to shared classification data rather than
	/// unique identifiers. Direct UID lookups are handled by dedicated methods that
	/// provide O(1) access to a specific entity.
	/// </para>
	/// <para>
	/// Prop queries currently support filtering by PropType, allowing efficient
	/// retrieval through cached lookup tables rather than full collection scans.
	/// </para>
	/// </summary>
	[Serializable]
	public readonly struct PropQuery {
		public readonly PropType? PropType;

		public PropQuery(PropType? propType = null) {
			this.PropType = propType;
		}
	}

	/// <summary>
	/// Maintains indexed access to prop entities and their queryable classifications.
	/// <para>
	/// The database serves as a lightweight lookup layer that provides:
	/// <list type="bullet">
	/// <item><description>O(1) direct retrieval by unique identifier.</description></item>
	/// <item><description>Cached collection retrieval by prop type.</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// The database does not own entity lifetimes. Instead, it maintains references
	/// to active entities and automatically removes them when lifecycle events indicate
	/// that the entity has died or been returned to a pool.
	/// </para>
	/// </summary>
	public class PropDatabase {

		/// <summary>
		/// Primary index used for direct entity retrieval by unique identifier.
		/// </summary>
		private readonly Dictionary<HashedTag, IReadOnlyComponentRegistry> _propRegistry = new();

		/// <summary>
		/// Cache of props grouped by their configured PropType.
		/// Enables efficient categorical queries without requiring iteration over
		/// all registered props.
		/// </summary>
		private readonly Dictionary<PropType, List<IReadOnlyComponentRegistry>> _propTypeCache = new();

		public int TotalProps => this._propRegistry.Count;

		/// <summary>
		/// Attempts to retrieve a prop using its unique identifier.
		/// Expected lookup complexity is O(1).
		/// </summary>
		public bool TryGetProp(HashedTag uid, out IReadOnlyComponentRegistry registry)
			=> this._propRegistry.TryGetValue(uid, out registry);

		/// <summary>
		/// Attempts to retrieve a cached collection of props matching the supplied query.
		/// <para>
		/// Retrieval is performed through existing cache structures and does not allocate
		/// new collections. Returned collections are the internally maintained cache
		/// instances exposed through an IReadOnlyList contract.
		/// </para>
		/// <para>
		/// If no matching cache exists, an empty collection is returned.
		/// </para>
		/// </summary>
		public bool TryGetProps(PropQuery query, out IReadOnlyList<IReadOnlyComponentRegistry> result) {
			if (query.PropType.HasValue &&
				this._propTypeCache.TryGetValue(query.PropType.Value, out var propList)) {

				result = propList;
				return true;
			}

			result = Array.Empty<IReadOnlyComponentRegistry>();
			return false;
		}

		// ---------------------------------------------------------------------
		// Registration & Lifecycle
		// ---------------------------------------------------------------------

		/// <summary>
		/// Registers a prop into all relevant lookup indexes and caches.
		/// <para>
		/// The entity is indexed by:
		/// <list type="bullet">
		/// <item><description>Unique identifier.</description></item>
		/// <item><description>Prop type classification.</description></item>
		/// </list>
		/// </para>
		/// <para>
		/// A lifecycle callback is registered so the database can automatically
		/// remove the entity when it dies or is returned to a pool.
		/// </para>
		/// </summary>
		public void RegisterProp(PropEntityDetail propDetail) {
			var registry = propDetail.ComponentRegistry;
			var uid = propDetail.UniqueID.HashedTag;

			this._propRegistry[uid] = registry;

			AddValueToCache(
				this._propTypeCache,
				propDetail.PropConfig.PropType,
				registry);

			propDetail.EventProvider.OnEntityDiedOrPooledEvent(RemoveProp, true);
		}

		/// <summary>
		/// Removes a prop from all lookup indexes and caches.
		/// <para>
		/// This method is typically invoked automatically through the entity lifecycle
		/// event system when the entity dies or is returned to a pool.
		/// </para>
		/// </summary>
		public void RemoveProp(EntityDetailBase entityDetail) {
			// if not a prop, ignore
			// since registration is only done for props, this should be safe
			// and the registration function args will ensure that only props are registered
			if (entityDetail is not PropEntityDetail propDetail)
				return;

			if (!this._propRegistry.Remove(entityDetail.UniqueID.HashedTag, out var registry))
				return;
			RemoveFromCache(
				this._propTypeCache,
				propDetail.PropConfig.PropType,
				registry);

			entityDetail.EventProvider.OnEntityDiedOrPooledEvent(RemoveProp, false);
		}

		// ---------------------------------------------------------------------
		// Cache Helpers
		// ---------------------------------------------------------------------

		/// <summary>
		/// Adds a registry reference to a categorized cache.
		/// Creates the cache bucket if it does not already exist.
		/// </summary>
		private static void AddValueToCache<TKey>(
			Dictionary<TKey, List<IReadOnlyComponentRegistry>> cache,
			TKey key,
			IReadOnlyComponentRegistry registry) {

			if (!cache.TryGetValue(key, out var list)) {
				list = new List<IReadOnlyComponentRegistry>();
				cache[key] = list;
			}

			list.Add(registry);
		}

		/// <summary>
		/// Removes a registry reference from a categorized cache.
		/// Empty cache buckets are intentionally retained to avoid unnecessary
		/// dictionary reallocations during frequent registration and removal cycles.
		/// </summary>
		private static void RemoveFromCache<TKey>(
			Dictionary<TKey, List<IReadOnlyComponentRegistry>> cache,
			TKey key,
			IReadOnlyComponentRegistry registry) {

			if (cache.TryGetValue(key, out var list)) {
				list.Remove(registry);
			}
		}
	}
}