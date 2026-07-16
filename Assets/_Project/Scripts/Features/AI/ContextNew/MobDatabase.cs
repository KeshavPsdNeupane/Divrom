using System;
using System.Collections.Generic;
using Kope.Core.Collections.Hashes;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Identity;
using Kope.EntityIdentity;

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
		private readonly Dictionary<RaceEnum, List<IReadOnlyComponentRegistry>> _raceCache = new();

		/// <summary>
		/// Cache of mobs grouped by relation.
		/// Enables efficient categorical queries without scanning all entities.
		/// </summary>
		private readonly Dictionary<EntityRelation, List<IReadOnlyComponentRegistry>> _relationCache = new();

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
		/// If the provided query are single attribute then the query is O(1) time complexity and 
		/// returns the internally maintained cache list.
		/// If the provided query contains multiple attributes then the query is O(n+m) time complexity and 
		/// returns a new list containing the intersection of the relevant caches.
		/// <para>
		/// Query evaluation behavior:
		/// <list type="bullet">
		/// <item><description>Race only → race cache lookup.</description></item>
		/// <item><description>Relation only → relation cache lookup.</description></item>
		/// <item><description>Race + Relation → AND intersection lookup.</description></item>
		/// </list>
		/// </para>
		/// <para>
		/// Single-attribute queries return the internally maintained cache lists.
		/// Multi-attribute queries generate an intersection from the relevant caches.
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
				this._raceCache.TryGetValue(query.Race.Value, out var raceList)) {

				result = raceList;
				return true;
			}

			if (query.Relation.HasValue &&
				this._relationCache.TryGetValue(query.Relation.Value, out var relationList)) {

				result = relationList;
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
		public void RemoveMob(MobEntityDetail mobDetail) {
			if (!this._mobRegistry.Remove(mobDetail.UniqueID.HashedTag, out var registry))
				return;

			RemoveFromCache(this._raceCache, mobDetail.MobConfig.Race, registry);
			RemoveFromCache(this._relationCache, mobDetail.MobConfig.Relation, registry);

			mobDetail.EventProvider.OnEntityDiedOrPooledEvent(RemoveMob, false);
		}

		/// <summary>
		/// Builds the intersection of two categorical caches.
		/// <para>
		/// The resulting collection contains only entities that belong to both
		/// the specified race and relation classifications.
		/// </para>
		/// <para>
		/// The smaller cache is iterated first to minimize lookup cost.
		/// </para>
		/// </summary>
		private List<IReadOnlyComponentRegistry> BuildIntersection(
			RaceEnum race,
			EntityRelation relation) {

			if (!this._raceCache.TryGetValue(race, out var raceList) ||
				!this._relationCache.TryGetValue(relation, out var relationList)) {

				return new List<IReadOnlyComponentRegistry>();
			}

			var result = new List<IReadOnlyComponentRegistry>(
				Math.Min(raceList.Count, relationList.Count));

			if (raceList.Count <= relationList.Count) {
				var lookup = new HashSet<IReadOnlyComponentRegistry>(relationList);

				foreach (var registry in raceList) {
					if (lookup.Contains(registry))
						result.Add(registry);
				}
			} else {
				var lookup = new HashSet<IReadOnlyComponentRegistry>(raceList);

				foreach (var registry in relationList) {
					if (lookup.Contains(registry))
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
		/// dictionary churn during frequent registration/removal cycles.
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