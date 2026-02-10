using System;
using System.Collections.Generic;
using ZLinq;
using Kope.Core.EntityComponentSystem;

/// <summary>
/// Stores the context of an entity and its targets.
/// Since the is a reference type, the underlying data can be mutated via this reference.<br/>
/// <inheritdoc cref="IReadOnlyContext"/>
/// </summary>
public class Context : IReadOnlyContext
{
    private readonly EntityComponentRegistry currentEntityContext;

    // Mutable target contexts
    private readonly Dictionary<HashedTag, List<EntityComponentRegistry>> targetEntityContexts = new();

    // Cached read-only wrappers
    private readonly Dictionary<HashedTag, IReadOnlyList<IReadOnlyEntityRegistry>> cachedReadOnly = new();

    public EntityComponentRegistry CurrentMutableEntityContext => this.currentEntityContext;

    //<inheritdoc/>
    public IReadOnlyEntityRegistry ReadOnlyEntityContext => this.currentEntityContext;

    public Context(EntityComponentRegistry currentEntityContext)
    {
        this.currentEntityContext = currentEntityContext;
    }

    /// <summary>
    /// Adds a target entity context. Clears cache to maintain consistency.
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="targetContext"></param>
    public void AddTargetEntityContext(HashedTag tag, EntityComponentRegistry targetContext)
    {
        if (!this.targetEntityContexts.TryGetValue(tag, out var list))
        {
            list = new List<EntityComponentRegistry>();
            this.targetEntityContexts[tag] = list;
        }

        list.Add(targetContext);

        // Invalidate cached read-only wrapper
        this.cachedReadOnly.Remove(tag);
    }

    /// <summary>
    /// Removes a target entity context. Clears cache to maintain consistency.
    /// </summary>
    public void RemoveTargetEntityContext(HashedTag tag, EntityComponentRegistry targetContext)
    {
        if (this.targetEntityContexts.TryGetValue(tag, out var list))
        {
            list.Remove(targetContext);
            if (list.Count == 0)
                this.targetEntityContexts.Remove(tag);
        }

        this.cachedReadOnly.Remove(tag);
    }

    /// <summary>
    /// Tries to get the mutable target contexts associated with the given tag.
    /// Returns true if found, false otherwise.
    /// Recommended to use only when it is extremely necessary to mutate target contexts.
    /// Otherwise, use IReadOnlyContext.TryGetTargetContext to get "Read-Only" access.
    /// Not Recommended to mutate target contexts directly as it may lead to inconsistent states.
    /// And may break the assumptions made by AI algorithms and actions.
    /// And most of time in Execute Method of any child of BaseActionSO, we only need read-only access.
    /// Even though the Context is passed there since we might need to read other target contexts.
    /// and mutate only the current entity's context. so recommended to use IReadOnlyContext in Execute methods too.
    ///  PLEASE BE WARNED. USE THIS METHOD ONLY WHEN IT IS EXTREMELY NECESSARY TO MUTATE TARGET CONTEXTS.
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="targetContext"></param>
    /// <returns></returns>
    public bool TryGetMutableTargetContext(HashedTag tag, out List<EntityComponentRegistry> targetContext)
    {
        return this.targetEntityContexts.TryGetValue(tag, out targetContext);
    }

    //<inheritdoc/>
    public bool TryGetReadOnlyTargetContext(HashedTag tag, out IReadOnlyList<IReadOnlyEntityRegistry> targetEntityContexts)
    {
        // Return cached wrapper if available
        if (this.cachedReadOnly.TryGetValue(tag, out targetEntityContexts)) return true;


        if (this.targetEntityContexts.TryGetValue(tag, out var mutableList))
        {
            targetEntityContexts = mutableList.AsValueEnumerable().Cast<IReadOnlyEntityRegistry>().ToList();
            this.cachedReadOnly[tag] = targetEntityContexts;
            return true;
        }

        targetEntityContexts = Array.Empty<IReadOnlyEntityRegistry>();
        return false;
    }
}
