using System.Collections.Generic;
using Kope.Core.EntityComponentSystem;


/// <summary>
/// IReadOnlyContext<br/>
/// Gives the Read only context of an entity and its targets.
/// Since the is a reference type, the underlying data can still be mutated via this reference.
/// So please dont mutate data via this reference. 
/// Using this Interface to hint that the context should be treated as read-only.
/// If some one breaks this rule, then it is their responsibility. since they opted into this contract.
/// </summary>
public interface IReadOnlyContext
{
    /// <summary>
    /// Gives "Read-Only" access to the current entity's context.
    /// Since the is a reference type, the underlying data can still be mutated via this reference.
    /// Please do not mutate data via this reference. If u do, 
    /// it is your responsibility since you opted into this contract.
    /// </summary>
    public IReadOnlyEntityRegistry ReadOnlyEntityContext { get; }

    /// <summary>
    /// Tries to get the "Read Only" target contexts associated with the given tag.
    /// Returns true if found, false otherwise.
    /// Since the is a reference type, the underlying data can still be mutated via this reference.
    /// Please do not mutate data via the returned contexts. If u do, 
    /// it is your responsibility since you opted into this contract.
    /// </summary>
    public bool TryGetReadOnlyTargetContext(HashedTag tag, out IReadOnlyList<IReadOnlyEntityRegistry> targetEntityContexts);
}

