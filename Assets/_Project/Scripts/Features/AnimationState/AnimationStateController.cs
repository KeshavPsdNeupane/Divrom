using System.Collections.Generic;
using Kope.Actor.New;
using Kope.Component.Animation;
using Kope.Component.Attack;
using Kope.Component.Movement;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using Kope.Core.Type.EnumAsset;
using Unity.VisualScripting;
using UnityEngine;

namespace Kope.Feature.AnimationState {
	public class EntityStateManagement : InitializableBase {
		[SerializeField] private EntityComponentsRegistry ecr;
		[Space(4), Header("Animation State Mapping")]
		[Tooltip("The Alias of the Enum must match the State Name in the Animator Controller.")]
		[SerializeField] private EnumTable<AnimationStateMappedProfile> animationStateHashTable;

		[SerializeField] private bool loadOnStart = true;

		private IDirectionProvider _lastDirectionProvider;
		private IAnimationComponent _animationComponent;
		private IAttackComponent _attackComponent;

		// Final runtime lookups: Optimized for O(1) hash-based state switching.
		private readonly Dictionary<int, AnimationStateProfileData> _animationStateHashLookup = new();

		private AnimationStateProfileData _idleStateData;

		private void Awake() {
			if (this.loadOnStart) Init();
		}

		protected override bool OnInit() {
			// 1. Dependency Validation
			if (!ValidateDependencies()) return false;

			// 2. Hydrate Default/Idle State
			// We handle the 'Idle' state first to ensure it's prioritized and excluded from the loop.
			var (idleInstance, idleProfile) = this.animationStateHashTable.GetDefaultBinding();
			if (idleInstance == null || idleProfile == null) {
				Debug.LogError($"[AnimationStateController] Initialization failed: Default Binding (ID 0) not found in {name}.");
				return false;
			}
			this._idleStateData = idleProfile.ToData(idleInstance.Alias);

			// 3. Hydrate Lookup Table
			// Using a clear loop to process remaining states. 
			// We use the 'BindLookup' directly as it contains the source of truth.
			foreach (var (instance, profile) in this.animationStateHashTable.BindLookup) {
				// Skip idle because we've already cached it specifically in _idleStateData
				if (instance == idleInstance) continue;

				int handle = instance.InternalValue;
				var profileData = profile.ToData(instance.Alias);

				if (!this._animationStateHashLookup.TryAdd(handle, profileData)) {
					Debug.LogWarning($"[AnimationStateController] Duplicate Handle detected: {instance.Alias} ({handle}). Skipping.");
					continue;
				}
				Debug.Log($"[AnimationStateController] Registered: {instance.Alias} | ID: {handle}");
			}

			return true;
		}

		private bool ValidateDependencies() {
			if (this.ecr == null) {
				Debug.LogError($"[Kope] Registry missing on {gameObject.name}");
				return false;
			}

			bool hasAnim = this.ecr.ComponentRegistry.TryGetMutatableComponent(out this._animationComponent);
			bool hasAttack = this.ecr.ComponentRegistry.TryGetMutatableComponent(out this._attackComponent);
			bool hasDir = this.ecr.ComponentRegistry.TryGetMutatableComponent(out this._lastDirectionProvider);

			if (!hasAnim || !hasAttack || !hasDir) {
				Debug.LogError($"[Kope] Critical Component missing in Registry on {gameObject.name}," +
				GetParentGameObjectHeirarchyMessage());
				return false;
			}

			return true;
		}
	}
}