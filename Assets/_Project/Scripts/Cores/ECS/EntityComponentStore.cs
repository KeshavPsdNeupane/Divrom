using System.Collections.Generic;
using Kope.Core.Init;
using UnityEngine;

namespace Kope.Core.EntityComponentSystem
{

    /// <summary>
    /// Stores a collection of components associated with an entity and initializes them.
    /// Components registered here do **not** need to be initialized elsewhere; 
    /// providing this class to the InitManager handles their initialization automatically.
    /// Using this class is optional — components can still be initialized individually in 
    /// InitManager if desired. 
    /// However, using EntityComponentStore is recommended for better organization and management of entity components.
    /// Centralizes initialization logic and avoids duplicate registration/Init calls.
    /// <br/>
    /// <inheritdoc cref="InitializableBase"/>
    /// </summary>

    public class EntityComponentStore : InitializableBase, IHashTagProvider
    {
        [SerializeField, Tooltip("Make sure this is same for all instances of the same type of entity. This is used for debugging and optimization purposes. " +
        "For example, all Goblin entities should have the same store name 'GoblinStore' so that we can easily identify them in the hierarchy and logs. " +
        "This is NOT used as a unique identifier for the entity, since multiple entities can share the same store name. " +
        "The unique identifier for the entity is determined by its HashedTag, which is generated from the store name and other factors at runtime.")]
        private string storeName;
        [SerializeField] private Transform entityTransform;
        [SerializeField, Tooltip("Indicates whether this EntityComponentStore contains state/AI/sensor components." +
        "So that the EntityComponentRegistry can optimize its registrations accordingly. and other systems can query this info easily.")]
        private bool hasBehavioralComponents = false;
        [SerializeField] private EntityComponentStoreConfig config;
        /// <summary>
        /// The list of components stored in this EntityComponentStore.
        /// </summary>
        [SerializeField, Tooltip("Order matters! \n\nIf you can't avoid circular dependencies (#skillIssue), refactor your life choices.")]
        private List<InitializableBase> components = new();
        private EntityComponentRegistry componentRegistry;

        private HashedTag hashedTag;

        /// <summary>
        /// Runtime registry of this EntityComponentStore.
        /// </summary>
        public EntityComponentRegistry ComponentRegistry => componentRegistry;
        public string StoreName => storeName;


        /// <summary>
        /// Mainly used for putting this EntityComponentStore into the Context and other systems that require a hashed tag for identification and optimization purposes.
        /// The HashedTag is generated from the store name and other factors at runtime, so it is not guaranteed to be the same across different runs or builds.
        /// However, it is guaranteed to be the same for the same store name within the same run, so it can be used for efficient lookups and comparisons.
        /// </summary>
        public HashedTag HashedTag
        {
            get
            {
                if (this.hashedTag == default)
                {
                    this.hashedTag = new HashedTag(this.storeName);
                }
                return this.hashedTag;
            }
        }

        public override void OnInit()
        {
            base.OnInit();
            if (this.config == null)
            {
                Debug.LogError($"EntityComponentStore '{this.storeName}' is missing its config reference. Cannot initialize component registry.");
                return;
            }
            this.componentRegistry = new EntityComponentRegistry(
                this.entityTransform,
                this.hasBehavioralComponents,
                this.config.ExcludedTypeSet
            );

            // First register all components
            foreach (var c in components)
            {
                componentRegistry.AddComponent(c);
            }
            // Then init all components, this will ensure that dependencies are resolved during Init
            // since all components are already registered. and no runtime race conditions occur.
            // but still order of components in the list matters anyway 
            foreach (var c in components)
            {
                c.Init();
            }

        }
    }

}