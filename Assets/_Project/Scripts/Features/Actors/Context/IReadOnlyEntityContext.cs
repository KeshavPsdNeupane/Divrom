using UnityEngine;


/// <summary>
/// IReadOnlyEntityContext<br/>
/// Stores "Read-Only" context of an entity.
/// Since the is a reference type, the underlying data can still be mutated via this reference.
/// Please dont mutate data via this reference. 
/// Using this Interface to hint that the context should be treated as read-only.
/// If some one breaks this rule, then it is their responsibility. since they opted into this contract.
/// </summary>
public interface IReadOnlyEntityContext
{
    /// <summary>
    /// Gives "Read-Only" access to the entity's Transform.
    /// Since the is a reference type, the underlying data can still be mutated via this reference.
    /// Please do not mutate data via this reference. If u do, 
    /// it is your responsibility since you opted into this contract.
    /// </summary>
    Transform EntityTransform { get; }
    /// <summary>
    /// Gives "Read-Only" access to the entity's State Machine.
    /// Since the is a reference type, the underlying data can still be mutated via this reference.
    /// Please do not mutate data via this reference. If u do, 
    /// it is your responsibility since you opted into this contract.
    /// </summary>
    EntityStates States { get; }

    /// <summary>
    /// Gives "Read-Only" access to the entity's State Machine.
    /// Since the is a reference type, the underlying data can still be mutated via this reference.
    /// Please do not mutate data via this reference. If u do, 
    /// it is your responsibility since you opted into this contract.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="component"></param>
    /// <returns></returns>
    bool TryGetComponent<T>(out T component);

}