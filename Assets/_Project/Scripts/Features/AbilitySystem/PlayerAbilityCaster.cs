using System;
using System.Collections.Generic;
using Kope.Component.Ability.Targeting;
using Kope.Component.Combat.Interface;
using Kope.Component.Health.Interface;
using Kope.Component.Attack;
using Kope.Core.EntityComponentRegistry;
using Kope.Core.LifeTimeManagement;
using Kope.Core.ServiceLocator;
using UnityEngine;
using UnityEngine.InputSystem;
using Kope.Component.HitBox.Interface;
using Kope.Component.Movement;
using Kope.AbilitySystem;

namespace Kope.Component.Ability {

	public class PlayerAbilityCaster : InitializableBase, IUpdatable {
		private const int MAX_HOTBAR_SLOT = 9;
		[Header("Settings")]
		[SerializeField, Range(1, 9)] private int abilityCount = 4;
		[SerializeField] private AbilityBase[] abilityScriptableObjects = Array.Empty<AbilityBase>();
		[SerializeField] private EntityComponentsRegistry ecr;

		private readonly List<AbilityRuntime> _hotbar = new();
		private readonly List<Action<InputAction.CallbackContext>> _inputCallbacks = new();

		private int _selectedSlotIndex = -1;

		// Dependencies
		private InputManager _inputManager;
		private TargetingManager _targetingManager;
		private ILockablePlayerAttack _attackLock;

		// Contexts
		private TargetContext _casterContext;
		private EffectContext _masterEffectContext;

		private bool _isSubscribed;

		protected override bool OnInit() {
			this.abilityCount = Mathf.Min(this.abilityCount, MAX_HOTBAR_SLOT);

			if (!GlobalServiceLocator.Instance.TryGetService(out this._inputManager)) return false;

			var registry = ecr.ComponentRegistry;
			if (registry == null) return false;

			// Resolve required components from the entity registry
			if (!registry.TryGetReadOnly(out _targetingManager) ||
				!registry.TryGetReadOnly(out IAttackComponent casterAttack) ||
				!registry.TryGetReadOnly(out IHealthComponent casterHealth) ||
				!registry.TryGetReadOnly(out IHitBoxComponent casterHitBox) ||
				!registry.TryGetReadOnly(out _attackLock) ||
				!registry.TryGetReadOnly(out IMovementComponent casterMovement)) {
				Debug.LogError("PlayerAbilityCaster failed to initialize due to missing components in the" +
				$" EntityComponentsRegistry.{this.HieararchyPath}", this);
				return false;
			}

			this._casterContext = new TargetContext(casterHitBox);

			this._masterEffectContext = new EffectContext {
				Dimension = registry.Dimension,
				Caster = registry.EntityTransform.gameObject,
				CasterAttack = casterAttack,
				CasterHealth = casterHealth,
				CasterMovement = casterMovement,
				CasterLevel = 0
			};

			InitializeHotbar();
			SubscribeEvents();
			return true;
		}

		private void OnValidate() {
			this.abilityCount = Mathf.Clamp(this.abilityCount, 1, MAX_HOTBAR_SLOT);

			if (this.abilityScriptableObjects.Length != this.abilityCount) {
				Array.Resize(ref this.abilityScriptableObjects, this.abilityCount);
			}
		}

		public void OnUpdate() {
			foreach (var ability in this._hotbar) {
				ability?.TickCooldowns(Time.deltaTime);
			}
		}

		private void InitializeHotbar() {
			this._hotbar.Clear();
			int count = Mathf.Min(this.abilityCount, this.abilityScriptableObjects.Length);

			for (int i = 0; i < count; i++) {
				if (this.abilityScriptableObjects[i] == null) {
					this._hotbar.Add(null);
					continue;
				}
				AbilityRuntime instance = new(this.abilityScriptableObjects[i], 0);
				this._hotbar.Add(instance);
			}
		}

		private void SubscribeEvents() {
			if (this._inputManager == null || this._isSubscribed) return;

			for (int i = 0; i < this._hotbar.Count; i++) {
				int index = i;
				void callback(InputAction.CallbackContext ctx) => OnAbilityKeyPressed(index, ctx);
				this._inputCallbacks.Add(callback);

				this._inputManager.Subscribe(new InputActionSubscriptionLifetime<PlayerInputActionKey>(
					PlayerInputActionCollection.Player,
					PlayerInputActionKey.Ability1 + index,
					callback)
				);
			}

			// Listen for the manager to signal that targeting has ended (Fire, Dodge, or Auto-Finish)
			this._targetingManager.OnTargetingCleanupRequested += ClearSelectionAndUnlockInput;
			this._isSubscribed = true;
		}

		private void UnsubscribeEvents() {
			if (this._inputManager == null || !this._isSubscribed) return;

			for (int i = 0; i < this._inputCallbacks.Count; i++) {
				this._inputManager.UnSubscribe(new InputActionSubscriptionLifetime<PlayerInputActionKey>(
					PlayerInputActionCollection.Player,
					PlayerInputActionKey.Ability1 + i,
					this._inputCallbacks[i]));
			}

			if (this._targetingManager != null) {
				this._targetingManager.OnTargetingCleanupRequested -= ClearSelectionAndUnlockInput;
			}

			this._inputCallbacks.Clear();
			this._isSubscribed = false;
		}


		private void OnAbilityKeyPressed(int index, InputAction.CallbackContext context) {
			if (!context.performed) return;
			HandleAbilityActivation(index);
		}

		/// <summary>
		/// Selects a slot and initiates the ability's casting/targeting sequence.
		/// </summary>
		public void HandleAbilityActivation(int index) {
			if (index < 0 || index >= this._hotbar.Count) return;

			if (this._selectedSlotIndex == index && this._targetingManager.IsTargeting) return;

			if (this._selectedSlotIndex != index &&
				this._selectedSlotIndex >= 0 &&
				this._selectedSlotIndex < this._hotbar.Count) {
				this._hotbar[this._selectedSlotIndex]?.Cancel();
			}

			var ability = this._hotbar[index];

			if (ability == null) {
				ClearSelectionAndUnlockInput();
				return;
			}

			if (!ability.CanCast) {
				ClearSelectionAndUnlockInput();
				return;
			}

			this._attackLock.SetEventLock(true);
			this._selectedSlotIndex = index;

			ability.Cast(
				this._targetingManager,
				this._casterContext,
				this._masterEffectContext
			);

			if (ability.IsInstantCast || !this._targetingManager.IsTargeting) {
				ClearSelectionAndUnlockInput();
			}
		}

		private void ClearSelectionAndUnlockInput() {
			this._selectedSlotIndex = -1;
			this._attackLock.SetEventLock(false);
		}

		protected override void OnDestroy() => UnsubscribeEvents();
		private void OnEnable() { if (this.IsInitialized) SubscribeEvents(); }
		private void OnDisable() { if (this.IsInitialized) UnsubscribeEvents(); }

		private GUIStyle _cooldownStyle;

		private void DrawAbilityBox(Rect rect, string text) {
			this._cooldownStyle ??= new GUIStyle(GUI.skin.box) {
				alignment = TextAnchor.MiddleCenter,
				fontStyle = FontStyle.Bold,
				wordWrap = false
			};

			// Start with a size based on the box height.
			int maxFontSize = Mathf.RoundToInt(rect.height * 0.45f);
			this._cooldownStyle.fontSize = maxFontSize;

			// Measure text width at the maximum size.
			float textWidth = this._cooldownStyle
				.CalcSize(new GUIContent(text))
				.x;

			float availableWidth = rect.width - 10f;

			// Scale font size down if the text is too wide.
			float widthScale = textWidth > 0f
				? availableWidth / textWidth
				: 1f;

			int finalFontSize = Mathf.FloorToInt(
				maxFontSize * Mathf.Min(widthScale, 1f)
			);

			this._cooldownStyle.fontSize = Mathf.Clamp(
				finalFontSize,
				8,
				maxFontSize
			);

			GUI.Box(rect, text, this._cooldownStyle);
		}

		private void OnGUI() {
			const float width = 500f;
			const float rowHeight = 60f;
			const float padding = 10f;

			float x = Screen.width - width - padding;
			float y = Screen.height - padding - (this._hotbar.Count * rowHeight);

			for (int i = 0; i < this._hotbar.Count; i++) {
				var ability = this._hotbar[i];

				if (ability == null)
					continue;

				string text = !ability.CanCast
					? $"[{i + 1}] {ability.Config.AbilityName} : CD, {ability.CooldownRemaining:F1}s"
					: $"[{i + 1}] {ability.Config.AbilityName} : Ready";

				DrawAbilityBox(
					new Rect(
						x,
						y + (i * rowHeight),
						width,
						rowHeight
					),
					text
				);
			}
		}
	}
}