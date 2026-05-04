using System.Collections.Generic;
using Kope.Actor.New;
using Kope.Component.Animation;
using Kope.Component.Attack;
using Kope.Component.Movement;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.Init;
using Kope.Core.Type.EnumAsset;
using UnityEngine;

namespace Kope.Feature.AnimationState {
	public class EntityStateManagement : InitializableBase {
		[SerializeField] private EntityComponentsRegistry ecr;
		[SerializeField] private AnimationData animationData;

		[Space(4), Header("Animation State Mapping")]
		[Tooltip("The Alias of the Enum must match the State Name in the Animator Controller.")]
		[SerializeField] private EnumTable<AnimationStateProfileForHash> animationStateHashTable;

		[SerializeField] private bool loadOnStart = true;

		private IDirectionProvider _lastDirectionProvider;
		private IAnimationComponent _animationComponent;
		private IAttackComponent _attackComponent;

		// Final runtime lookups: Optimized for O(1) hash-based state switching.
		private readonly Dictionary<int, AnimationStateProfileData> _animationStateLookup = new();
		private readonly Dictionary<int, AnimationStateProfileData> _animationStateHashLookup = new();

		private void Awake() {
			if (this.loadOnStart) Init();
		}

		protected override bool OnInit() {
			// 1. Dependency Validation
			if (!ValidateDependencies()) return false;

			// 2. Hydrate Standalone Animation Data
			foreach (var profile in this.animationData.Value) {
				if (!this._animationStateLookup.ContainsKey(profile.Hash)) {
					this._animationStateLookup.Add(profile.Hash, profile);
				} else {
					Debug.LogWarning($"[Kope] Duplicate Hash detected in AnimationData: {profile.Name}.", this);
				}
			}

			// 3. Hydrate Centralized EnumTable Data
			// This converts Designer-friendly UI entries into Immutable Runtime Structs
			foreach (var kvp in this.animationStateHashTable.BindLookup) {
				int handle = kvp.Key.InternalValue;
				// Using the Alias (string) from the EnumInstance to generate the Hash internally
				var profileData = kvp.Value.ToData(kvp.Key.Alias);

				if (!this._animationStateHashLookup.ContainsKey(handle)) {
					this._animationStateHashLookup.Add(handle, profileData);
				}
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
				Debug.LogError($"[Kope] Critical Component missing in Registry on {gameObject.name}");
				return false;
			}

			return true;
		}

		protected override void OnUpdate() {
			base.OnUpdate();
		}
	}
}