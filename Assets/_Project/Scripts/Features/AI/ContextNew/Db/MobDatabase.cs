using System;
using System.Collections.Generic;
using Kope.Core.Collections.Hashes;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Identity;
using Kope.EntityIdentity;

/**
 * ARCHITECTURE: Materialized View Pattern
 * ---------------------------------------
 * This database employs a "1 + N" indexing strategy:
 * - 1 Primary Index: O(1) retrieval by Unique Identifier (HashedTag).
 * - N Categorical Indexes: O(min(N, M)) intersection queries using dual-storage 
 *   (List for iteration, HashSet for O(1) membership).
 *
 * DESIGN NOTE: Potential for Generic Refactoring
 * ----------------------------------------------
 * Because the structure (Registry + 1 Primary Dictionary + N Categorical Dictionaries)
 * is identical across databases, this can be abstracted into a generic base:
 * 
 * public abstract class CategorizedDatabase<TRegistry, TKey1, TKey2> 
 *     where TRegistry : IReadOnlyComponentRegistry 
 * {
 *     protected readonly Dictionary<HashedTag, TRegistry> Registry = new();
 *     protected readonly Dictionary<TKey1, (HashSet<TRegistry>, List<TRegistry>)> Cache1 = new();
 *     protected readonly Dictionary<TKey2, (HashSet<TRegistry>, List<TRegistry>)> Cache2 = new();
 *     
 *     protected List<TRegistry> BuildIntersection(TKey1 k1, TKey2 k2) { ... }
 * }
 * 
 * Refactoring would centralize intersection and index-management logic while keeping 
 * specialized Query structs to preserve a clean, type-safe API. This is for future 
 * consideration and is not necessary for the current implementation, which is 
 * focused on a single entity type (MobEntityDetail).
 */


namespace Kope.AI {

	/// <summary>
	/// Represents a categorical query used to retrieve collections of mobs.
	/// <para>
	/// This query is intentionally limited to shared classification data such as
	/// race and relation. Unique identifiers are excluded because UID lookups are
	/// handled by dedicated direct-access methods that return a single entity in O(1).
	/// </para>
	/// <para>
	/// Query behavior:
	/// <list type="bullet">
	/// <item><description>Single populated field = direct cache lookup.</description></item>
	/// <item><description>Multiple populated fields = intersection query (AND operation).</description></item>
	/// </list>
	/// </para>
	/// </summary>
	[Serializable]
	public readonly struct MobQuery {

		/// <summary>
		/// Optional relation filter.
		/// </summary>
		public readonly EntityRelation? Relation;

		/// <summary>
		/// Optional race filter.
		/// </summary>
		public readonly RaceEnum? Race;

		public MobQuery(
			EntityRelation? relation = null,
			RaceEnum? race = null) {

			this.Relation = relation;
			this.Race = race;
		}

		/// <summary>
		/// Returns true when multiple query criteria are supplied and
		/// an intersection (AND) lookup should be performed.
		/// </summary>
		public bool IsMultiQuery => this.Relation.HasValue && this.Race.HasValue;

		/// <summary>
		/// Returns true when the query contains no filtering criteria.
		/// </summary>
		public bool IsEmpty => !this.Relation.HasValue && !this.Race.HasValue;
	}

	/// <summary>
	/// Maintains indexed access to mob entities and their queryable classifications.
	/// <para>
	/// The database acts as a lightweight lookup layer over registered mobs, providing:
	/// <list type="bullet">
	/// <item><description>O(1) direct retrieval by unique identifier.</description></item>
	/// <item><description>Cached collection retrieval by race.</description></item>
	/// <item><description>Cached collection retrieval by relation.</description></item>
	/// <item><description>Intersection retrieval by race and relation.</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// The database does not own entity lifetimes. Instead, it tracks active entities
	/// and automatically removes references when entities die or are returned to a pool,
	/// preventing stale references from remaining in query caches.
	/// </para>
	/// </summary>
	public class MobDatabase {

		/// <summary>
		/// Primary index used for direct entity retrieval by unique identifier.
		/// </summary>
		private readonly Dictionary<HashedTag, IReadOnlyComponentRegistry> _mobRegistry = new();

		/// <summary>
		/// Cache of mobs grouped by race.
		/// Enables efficient categorical queries without scanning all entities.
		/// </summary>
		private readonly Dictionary<RaceEnum,
		(HashSet<IReadOnlyComponentRegistry>, List<IReadOnlyComponentRegistry>)> _raceCache = new();

		/// <summary>
		/// Cache of mobs grouped by relation.
		/// Enables efficient categorical queries without scanning all entities.
		/// </summary>
		private readonly Dictionary<EntityRelation,
		(HashSet<IReadOnlyComponentRegistry>, List<IReadOnlyComponentRegistry>)> _relationCache = new();

		public int TotalMobs => this._mobRegistry.Count;
		/// <summary>
		/// Attempts to retrieve a mob using its unique identifier.
		/// Expected lookup complexity is O(1).
		/// </summary>
		public bool TryGetMob(
			HashedTag uid,
			out IReadOnlyComponentRegistry registry)
			=> this._mobRegistry.TryGetValue(uid, out registry);

		/// <summary>
		/// Attempts to retrieve a collection of mobs matching the supplied query.
		/// <para>
		/// Evaluation behavior:
		/// <list type="bullet">
		/// <item><description>Single-attribute (Race/Relation): O(1) cache lookup.</description></item>
		/// <item><description>Multi-attribute (Race + Relation): O(min(N, M)) intersection lookup.</description></item>
		/// </list>
		/// </para>
		/// </summary>
		public bool TryGetMobs(
			MobQuery query,
			out IReadOnlyList<IReadOnlyComponentRegistry> result) {

			if (query.IsMultiQuery) {
				var compositeList = BuildIntersection(
					query.Race.Value,
					query.Relation.Value);

				result = compositeList;
				return compositeList.Count > 0;
			}

			if (query.Race.HasValue &&
				this._raceCache.TryGetValue(query.Race.Value, out var raceCache)) {

				result = raceCache.Item2;
				return true;
			}

			if (query.Relation.HasValue &&
				this._relationCache.TryGetValue(query.Relation.Value, out var relationCache)) {

				result = relationCache.Item2;
				return true;
			}

			result = Array.Empty<IReadOnlyComponentRegistry>();
			return false;
		}

		/// <summary>
		/// Registers a mob into all relevant lookup indexes and caches.
		/// <para>
		/// The entity is indexed by:
		/// <list type="bullet">
		/// <item><description>Unique identifier.</description></item>
		/// <item><description>Race classification.</description></item>
		/// <item><description>Relation classification.</description></item>
		/// </list>
		/// </para>
		/// <para>
		/// A lifecycle callback is also registered so the database can automatically
		/// remove the entity when it dies or is returned to a pool.
		/// </para>
		/// </summary>
		public void RegisterMob(MobEntityDetail mobDetail) {
			var registry = mobDetail.ComponentRegistry;
			var uid = mobDetail.UniqueID.HashedTag;

			this._mobRegistry[uid] = registry;

			AddValueToCache(this._raceCache, mobDetail.MobConfig.Race, registry);
			AddValueToCache(this._relationCache, mobDetail.MobConfig.Relation, registry);

			mobDetail.EventProvider.OnEntityDiedOrPooledEvent(RemoveMob, true);
		}

		/// <summary>
		/// Removes a mob from all lookup indexes and caches.
		/// <para>
		/// This method is typically invoked automatically through the entity lifecycle
		/// event system when the entity dies or is returned to a pool.
		/// </para>
		/// </summary>
		public void RemoveMob(EntityDetailBase entityDetail) {
			// if not mob, ignore 
			// since registration is only done for mobs, this should be safe
			// and the registration function args will ensure that only mobs are registered
			if (entityDetail is not MobEntityDetail mobDetail)
				return;

			if (!this._mobRegistry.Remove(entityDetail.UniqueID.HashedTag, out var registry))
				return;
			RemoveFromCache(this._raceCache, mobDetail.MobConfig.Race, registry);
			RemoveFromCache(this._relationCache, mobDetail.MobConfig.Relation, registry);

			mobDetail.EventProvider.OnEntityDiedOrPooledEvent(RemoveMob, false);
		}
		/// <summary>
		/// Builds the intersection of two categorical caches.
		/// <para>
		/// Uses the pre-existing HashSet from the cache for O(1) lookups,
		/// avoiding new allocations during the intersection process.
		/// </para>
		/// </summary>
		private List<IReadOnlyComponentRegistry> BuildIntersection(
			RaceEnum race,
			EntityRelation relation) {

			if (!this._raceCache.TryGetValue(race, out var raceCache) ||
				!this._relationCache.TryGetValue(relation, out var relationCache)) {
				return new List<IReadOnlyComponentRegistry>();
			}

			var raceList = raceCache.Item2;
			var raceSet = raceCache.Item1;
			var relList = relationCache.Item2;
			var relSet = relationCache.Item1;

			// Choose the smaller list to iterate, and the larger set to perform lookups
			// This ensures O(min(N, M)) performance
			var result = new List<IReadOnlyComponentRegistry>(Math.Min(raceList.Count, relList.Count));

			if (raceList.Count <= relList.Count) {
				// Iterate Race, Lookup in Relation Set
				foreach (var registry in raceList) {
					if (relSet.Contains(registry))
						result.Add(registry);
				}
			} else {
				// Iterate Relation, Lookup in Race Set
				foreach (var registry in relList) {
					if (raceSet.Contains(registry))
						result.Add(registry);
				}
			}

			return result;
		}

		/// <summary>
		/// Adds a registry reference to a categorized cache.
		/// Creates the cache bucket if it does not already exist.
		/// </summary>
		private static void AddValueToCache<TKey>(
			Dictionary<TKey, (HashSet<IReadOnlyComponentRegistry>, List<IReadOnlyComponentRegistry>)> cache,
			TKey key,
			IReadOnlyComponentRegistry registry) {

			if (!cache.TryGetValue(key, out var tuple)) {
				tuple = (new HashSet<IReadOnlyComponentRegistry>(), new List<IReadOnlyComponentRegistry>());
				cache[key] = tuple;
			}

			tuple.Item2.Add(registry);
			tuple.Item1.Add(registry);
		}

		/// <summary>
		/// Removes a registry reference from a categorized cache.
		/// Empty cache buckets are intentionally retained to avoid unnecessary
		/// dictionary churn during frequent registration/removal cycles.
		/// </summary>
		private static void RemoveFromCache<TKey>(
			Dictionary<TKey, (HashSet<IReadOnlyComponentRegistry>, List<IReadOnlyComponentRegistry>)> cache,
			TKey key,
			IReadOnlyComponentRegistry registry) {

			if (cache.TryGetValue(key, out var tuple)) {
				tuple.Item2.Remove(registry);
				tuple.Item1.Remove(registry);
			}
		}
	}
}