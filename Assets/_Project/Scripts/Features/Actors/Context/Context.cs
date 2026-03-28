using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Kope.Core.EntityComponentSystem;
/// <summary>
/// Stores the operational context of an entity and its collection of targets.
/// <para>
/// <b>Self-Identity:</b> Provides mutable access to the current entity's registry for state changes.
/// <br/><b>Targets:</b> Provides strictly read-only access to target entities to prevent unintended external mutations.
/// </para>
/// </summary>
public class Context : IReadOnlyContext {
	private readonly ComponentRegistry currentEntityContext;

	// Primary Storage: Nested dictionary for O(1) individual lookups
	private readonly Dictionary<HashedTag, Dictionary<HashedTag, IReadOnlyComponentRegistry>> targetEntityContexts = new();

	private readonly Dictionary<HashedTag, List<IReadOnlyComponentRegistry>> listCache = new();

	public ComponentRegistry CurrentMutableEntityContext => this.currentEntityContext;
	public IReadOnlyComponentRegistry ReadOnlyEntityContext => this.currentEntityContext;

	public int GetTotalEntityCount() {
		return this.targetEntityContexts.Count == 0 ? 0 : this.targetEntityContexts.Values.Sum(innerDict => innerDict.Count);
	}

	public Context(ComponentRegistry currentEntityContext) {
		this.currentEntityContext = currentEntityContext ?? throw new ArgumentNullException(nameof(currentEntityContext));
	}

	public void RegisterEntityContext(EntityDetail entityDetail) {
		var commonTag = entityDetail.CommonEntityHashedTag;
		var individualTag = entityDetail.UniqueID.HashedTag;
		var targetContext = entityDetail.EntityComponentRegistry.ComponentRegistry;

		// Ensure the common category exists
		if (!targetEntityContexts.TryGetValue(commonTag, out var innerDict)) {
			innerDict = new Dictionary<HashedTag, IReadOnlyComponentRegistry>();
			targetEntityContexts[commonTag] = innerDict;
			listCache[commonTag] = new List<IReadOnlyComponentRegistry>();
		}

		// Only add if it's a new individual entity
		if (!innerDict.ContainsKey(individualTag)) {
			innerDict[individualTag] = targetContext;
			listCache[commonTag].Add(targetContext);
			// only subscribe to the entity's death/pooled event if it's a new entry to prevent multiple subscriptions
			// for the same entity
			entityDetail.EntityDiedOrPooledHandler.OnEntityDiedOrPooled += RemoveEntityDueToSignal;
		}
		// If it exists, the reference is already shared. Do nothing.
	}
	public void RemoveTargetEntityContext(UniqueID individualTag, HashedTag commonTag) {
		//Debug.Log($"[Context] Removing target entity context: CommonTag={commonTag}, IndividualTag={individualTag}");
		if (targetEntityContexts.TryGetValue(commonTag, out var innerDict)) {
			var individualHashedTag = individualTag.HashedTag;
			if (innerDict.TryGetValue(individualHashedTag, out var context)) {
				// Remove from cache list
				if (listCache.TryGetValue(commonTag, out var cacheList)) {
					cacheList.Remove(context);
					//Debug.Log($"[Context] Removed target entity from cache: CommonTag={commonTag}, IndividualTag={individualTag}");
				}

				innerDict.Remove(individualHashedTag);
				if (innerDict.Count == 0) {
					//	Debug.Log($"[Context] All entities removed from category: CommonTag={commonTag}");
					targetEntityContexts.Remove(commonTag);
					listCache.Remove(commonTag);

				}
				//context.E.EntityDiedOrPooledHandler.OnEntityDiedOrPooled -= RemoveEntityDueToSignal;
			}
		}
	}

	/// <summary>
	/// Now 100% allocation-free and O(1).
	/// </summary>
	public bool TryGetReadOnlyTargetContext(HashedTag commonTag, HashedTag individualTag, out IReadOnlyComponentRegistry targetEntityContext) {
		if (this.targetEntityContexts.TryGetValue(commonTag, out var dict)) {
			return dict.TryGetValue(individualTag, out targetEntityContext);
		}

		targetEntityContext = null;
		return false;
	}

	/// <summary>
	/// Returns the cached list. No "new List" allocation at runtime.
	/// </summary>
	public bool TryGetReadOnlyTargetContexts(HashedTag commonTag, out IReadOnlyList<IReadOnlyComponentRegistry> targetEntityContexts) {
		if (this.listCache.TryGetValue(commonTag, out var cache)) {
			targetEntityContexts = cache;
			return true;
		}

		targetEntityContexts = Array.Empty<IReadOnlyComponentRegistry>();
		return false;
	}


	private void RemoveEntityDueToSignal(UniqueID individualTag, HashedTag commonTag) {
		Debug.Log($"[Context] Received entity death/pooled signal: CommonTag={commonTag}, IndividualTag={individualTag}");
		RemoveTargetEntityContext(individualTag, commonTag);
	}
}