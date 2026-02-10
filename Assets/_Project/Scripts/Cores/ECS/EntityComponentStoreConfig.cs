using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kope.Core.EntityComponentSystem
{

    /// <summary>
    /// Configuration ScriptableObject for EntityComponentStore.
    /// Allows specifying types to exclude from component registration.
    /// This is useful to avoid registering common Unity types like MonoBehaviour, Component, etc.
    /// since the EntityComponentStore uses reflection to register  all base types and  all interfaces of components.
    /// </summary>
    [CreateAssetMenu(fileName = "EntityComponentStoreConfig", menuName = "Scriptable Objects/Actors/EntityComponentStoreConfig", order = 1)]
    public class EntityComponentStoreConfig : ScriptableObject
    {
        [Header("Excluded Types Configuration." +
            " Provide the full type names (including namespace) of types to exclude from component registration." +
            "already default excluded types are MonoBehaviour, Component, Behaviour and ScriptableObject to avoid unnecessary registrations.")]
        [SerializeField] private List<string> excludedTypeNames = null;

        private HashSet<Type> excludedTypeSet;
        /// <summary>
        /// Gets the set of excluded types based on the provided type names.
        /// Search through all loaded assemblies to find matching types.
        /// Is a bit heavy operation, so the result is cached after the first call.
        /// </summary>
        public HashSet<Type> ExcludedTypeSet
        {
            get
            {
                // Return the cached set if already initialized.
                if (this.excludedTypeSet != null)
                    return this.excludedTypeSet;

                // If not cached and no type names provided, return empty set.
                if (this.excludedTypeNames == null || this.excludedTypeNames.Count == 0)
                    return this.excludedTypeSet ??= new HashSet<Type>();

                // create the set and populate it.
                this.excludedTypeSet = new HashSet<Type>();
                // find all the assemblies in the current domain and try to get the type from each assembly.
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var typeName in this.excludedTypeNames)
                {
                    foreach (var assembly in assemblies)
                    {
                        // try to get the type by name on each assembly.
                        var type = assembly.GetType(typeName);
                        if (type != null)
                        {
                            this.excludedTypeSet.Add(type);
                            break;
                        }
                    }
                }
                return this.excludedTypeSet;
            }


        }
    }

}