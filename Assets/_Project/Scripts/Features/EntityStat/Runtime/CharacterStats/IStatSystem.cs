using UnityEngine.Events;

namespace Kope.Character.Stats {
	/// <summary>
	/// Provides a decoupled entry point for reading stats and applying modifiers.
	/// This allows the HurtBox and CombatProcessor to interact with stats 
	/// without knowing the internal dictionary logic.
	/// </summary>
	public interface IStatSystem {
		// --- Reading Values ---
		float GetStatValue(CharacterStatType type);
		float GetResistanceValue(DamageType type);

		// --- Reactive Updates (Observer Pattern) ---
		void StatsSubscribe(CharacterStatType type, UnityAction<float> callback);
		void StatsUnsubscribe(CharacterStatType type, UnityAction<float> callback);

		void ResistanceSubscribe(DamageType type, UnityAction<float> callback);
		void ResistanceUnsubscribe(DamageType type, UnityAction<float> callback);

		// --- Modification (The Pipe Entry Point) ---
		/// <summary>
		/// Directly injects a modifier (Buff/Debuff) into the stat's internal lifecycle.
		/// </summary>
		bool AddStatModifier(BaseStatModifier modifier);
		bool AddResistanceModifier(ResistanceStatModifier modifier);
		void InitialLevelSetup(int level);
		void LevelUp(int newLevel);
	}
}