using System;
using System.Collections.Generic;

namespace Kope.Character.Stats {
	public class ArmorAndBuffAndDeBuffEffectStat : IBaseStatProvider {
		private IBaseStatProvider baseStat;
		private readonly List<BuffDebuffArmorStatusModifier> modifiers = new();

		private bool isDirty = true;
		private float cachedValue;

		public event Action OnDirtyEventAction;
		public void SetBase(IBaseStatProvider baseStat) => this.baseStat = baseStat;

		public void MarkDirty() {
			this.isDirty = true;
			this.OnDirtyEventAction?.Invoke();
		}

		private void CanRemoveModifier(BuffDebuffArmorStatusModifier csm)
		=> csm.canRemove = true;

		public void RemoveModifier(BuffDebuffArmorStatusModifier csm) {
			this.modifiers.Remove(csm);
			MarkDirty();
		}

		public void Update() {
			foreach (var mod in this.modifiers) {
				mod.durationCountDownTimer?.Tick(UnityEngine.Time.deltaTime);
			}
			for (int i = this.modifiers.Count - 1; i >= 0; i--) {
				var mod = this.modifiers[i];
				if (mod.canRemove) {
					RemoveModifier(mod);
				}
			}

		}

		public bool AddModifier(StatModifier effect) {
			if (!effect.IsDebuff) {
				return AddToList(effect);
			}

			bool isEnemy = effect.isDebuffFromEnemy;
			bool isArmor = effect.isDebuffFromArmor;
			bool isEnv = !isEnemy && !isArmor;

			if (isEnemy || isEnv) {
				var existing = modifiers.Find(m => m.IsDebuff &&
												   ((isEnemy && m.IsDebuffFromEnemy) ||
												   (isEnv && !m.IsDebuffFromEnemy && !m.IsDebuffFromArmor)));
				if (existing != null) {
					if (effect.debuffPriority >= existing.DebuffPriority)
						RemoveModifier(existing);
					else
						return false;
				}
			}
			return AddToList(effect);

		}

		private bool AddToList(StatModifier effect) {
			// Prevent  duplicate permanent buff modifiers from same source
			bool alreadyApplied = modifiers.Exists(
				m => m.Source == effect.source &&
				m.ModifierAmount == effect.modifierAmount &&
				m.IsPercentage == effect.isPercentage);

			if (alreadyApplied)
				return false;

			var mod = new BuffDebuffArmorStatusModifier(effect);

			// Subscribe to timer stop if temporary
			if (!mod.IsPermanentBuff) {
				mod.InitializeTimer();
				mod.durationCountDownTimer.OnTimerStop += () => CanRemoveModifier(mod);
				mod.StartTimer();
			}
			this.modifiers.Add(mod);
			MarkDirty();
			return true;
		}


		public void RemoveAllModifiersFromSource(string sourceName) {
			this.modifiers.RemoveAll(m => m.Source == sourceName);
			MarkDirty();
		}

		public void RemoveAllModifiers() {
			this.modifiers.Clear();
			MarkDirty();
		}


		public float GetValue() {
			if (!this.isDirty) return this.cachedValue;

			float baseValue = this.baseStat?.GetValue() ?? 0f;
			float value = baseValue;

			foreach (var m in modifiers) {
				if (m.IsPercentage)
					value += baseValue * (m.ModifierAmount * 0.01f);
				else
					value += m.ModifierAmount;
			}

			this.cachedValue = value;
			this.isDirty = false;
			return value;
		}
	}
}