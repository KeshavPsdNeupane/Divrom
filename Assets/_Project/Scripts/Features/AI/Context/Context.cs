using System;
using System.Collections.Generic;
using ZLinq;
using Kope.Component;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Collections.Hashes;

/// <summary>
/// Stores the operational context of an entity and its collection of targets.
/// <para>
/// <b>Self-Identity:</b> Provides mutable access to the current entity's registry for state changes.
/// <br/><b>Targets:</b> Provides strictly read-only access to target entities to prevent unintended external mutations.
/// </para>
/// </summary>
public class Context : IReadOnlyContext {
	private FieldOfViewData _fieldOfViewData;
	public FieldOfViewData FieldOfViewData => this._fieldOfViewData;

	private readonly ComponentRegistry _currentEntityContext;
	/// <summary>
	/// Look up structure for very specific target retrieval: [CommonTag] -> [IndividualTag] -> TargetRegistry.
	/// This is designed for maximum flexibility and performance, allowing both broad category access and specific entity access without any runtime allocations.
	/// The outer dictionary categorizes entities by their common tag (e.g., "Player"), while the inner dictionary allows for direct access to specific entities by their unique ID, all with O(1) complexity.
	/// The IReadOnlyComponentRegistry values ensure that target entities are protected from unintended modifications, enforcing a clear separation of concerns where the Context manages entity state while external systems can only read target states without altering them. 
	/// The design also includes an efficient caching mechanism for lists of targets under a common tag to support considerations that need to evaluate all entities of
	/// </summary>
	private readonly Dictionary<HashedTag, Dictionary<HashedTag, IReadOnlyComponentRegistry>> _targetEntityContexts = new();

	/// <summary>
	/// Look of for all targets under a common tag, e.g. "Player", regardless of their individual tags/IDs.
	/// This is useful for actions/considerations that want to consider all entities of a certain type/category without caring about their individual identities.
	/// The inner IReadOnlyComponentRegistry list is a cached list of the values from the inner dictionary of _targetEntityContexts for O(1) retrieval
	/// </summary>
	private readonly Dictionary<HashedTag, List<IReadOnlyComponentRegistry>> _listCache = new();

	public ComponentRegistry CurrentMutableEntityContext => this._currentEntityContext;
	public IReadOnlyComponentRegistry SelfReadOnlyEntityContext => this._currentEntityContext;
	public void SetFieldOfViewData(FieldOfViewData data) {
		this._fieldOfViewData = data;
	}

	public int GetTotalEntityCount() {
		return this._targetEntityContexts.Count == 0 ? 0 : this._targetEntityContexts.Values.AsValueEnumerable()
		.Sum(innerDict => innerDict.Count);
	}

	public Context(ComponentRegistry currentEntityContext) {
		this._currentEntityContext = currentEntityContext ?? throw new ArgumentNullException(nameof(currentEntityContext));
	}

	public void RegisterEntityContext(EntityDetail entityDetail) {
		var commonTag = entityDetail.CommonEntityHashedTag;
		var individualTag = entityDetail.UniqueID.HashedTag;
		var targetContext = entityDetail.ComponentRegistry;

		// Ensure the common category exists
		if (!_targetEntityContexts.TryGetValue(commonTag, out var innerDict)) {
			innerDict = new Dictionary<HashedTag, IReadOnlyComponentRegistry>();
			_targetEntityContexts[commonTag] = innerDict;
			_listCache[commonTag] = new List<IReadOnlyComponentRegistry>();
		}

		// Only add if it's a new individual entity
		if (!innerDict.ContainsKey(individualTag)) {
			innerDict[individualTag] = targetContext;
			_listCache[commonTag].Add(targetContext);
			// only subscribe to the entity's death/pooled event if it's a new entry to prevent multiple subscriptions
			// for the same entity
			entityDetail.EventProvider.OnEntityDiedOrPooled += RemoveEntityDueToSignal;
		}
		// If it exists, the reference is already shared. Do nothing.
	}
	public void RemoveTargetEntityContext(EntityDetail entityDetail) {
		var commonTag = entityDetail.CommonEntityHashedTag;
		var individualTag = entityDetail.UniqueID;
		if (_targetEntityContexts.TryGetValue(commonTag, out var innerDict)) {
			var individualHashedTag = individualTag.HashedTag;
			if (innerDict.TryGetValue(individualHashedTag, out var IReadOnlyEntityRegistry)) {
				// Remove from cache list
				if (_listCache.TryGetValue(commonTag, out var cacheList)) {
					cacheList.Remove(IReadOnlyEntityRegistry);
					//Debug.Log($"[Context] Removed target entity from cache: CommonTag={commonTag}, IndividualTag={individualTag}");
				}

				innerDict.Remove(individualHashedTag);
				if (innerDict.Count == 0) {
					//	Debug.Log($"[Context] All entities removed from category: CommonTag={commonTag}");
					_targetEntityContexts.Remove(commonTag);
					_listCache.Remove(commonTag);

				}
				// Unsubscribe from the entity's death/pooled event to prevent memory leaks and unintended callbacks
				entityDetail.EventProvider.OnEntityDiedOrPooled -= RemoveEntityDueToSignal;
			}
		}
	}

	/// <summary>
	/// Now 100% allocation-free and O(1).
	/// </summary>
	public bool TryGetReadOnlyTargetContext(HashedTag commonTag, HashedTag individualTag, out IReadOnlyComponentRegistry targetEntityContext) {
		if (this._targetEntityContexts.TryGetValue(commonTag, out var dict)) {
			return dict.TryGetValue(individualTag, out targetEntityContext);
		}

		targetEntityContext = null;
		return false;
	}

	/// <summary>
	/// Returns the cached list. No "new List" allocation at runtime.
	/// </summary>
	public bool TryGetReadOnlyTargetContexts(HashedTag commonTag, out IReadOnlyList<IReadOnlyComponentRegistry> targetEntityContexts) {
		if (this._listCache.TryGetValue(commonTag, out var cache)) {
			targetEntityContexts = cache;
			return true;
		}
		targetEntityContexts = Array.Empty<IReadOnlyComponentRegistry>();
		return false;
	}


	private void RemoveEntityDueToSignal(EntityDetail entityDetail) {
		//Debug.Log($"[Context] Received signal to remove entity: UniqueID={entityDetail.UniqueID}, CommonTag={entityDetail.CommonEntityHashedTag}");
		RemoveTargetEntityContext(entityDetail);
	}
}