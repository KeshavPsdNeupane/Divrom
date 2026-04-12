using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Kope.Character.Stats {
	public class CharacterStatsGUI : MonoBehaviour {
		[SerializeField] private CharacterStatsSystem characterStats;
		[SerializeField] public bool canShowState = true;
		[SerializeField][Range(0.5f, 3f)] private float scale = 2f;

		private GUIStyle labelStyle;

		// Local copies of values
		private readonly Dictionary<CharacterStatType, float> statsValues = new();
		private readonly Dictionary<DamageType, float> resistanceValues = new();

		// Store callbacks for safe unsubscription
		private Dictionary<CharacterStatType, UnityAction<float>> statsCallbacksDict = new();
		private Dictionary<DamageType, UnityAction<float>> resistanceCallbacksDict = new();

		private void Awake() {
			if (characterStats == null)
				characterStats = GetComponent<CharacterStatsSystem>();
		}

		private void Start() {
			if (characterStats == null) return;

			// Subscribe normal stats
			foreach (CharacterStatType type in Enum.GetValues(typeof(CharacterStatType))) {
				statsValues[type] = characterStats.GetStatValue(type);

				void callback(float val) => statsValues[type] = val;
				statsCallbacksDict[type] = callback;

				characterStats.StatsSubscribe(type, callback);
			}

			// Subscribe resistances
			foreach (DamageType type in Enum.GetValues(typeof(DamageType))) {
				resistanceValues[type] = characterStats.GetResistanceValue(type);

				void callback(float val) => resistanceValues[type] = val;
				resistanceCallbacksDict[type] = callback;

				characterStats.ResistanceSubscribe(type, callback);
			}
		}

		private void OnDisable() {
			if (characterStats == null) return;

			// Unsubscribe normal stats
			foreach (var kvp in statsCallbacksDict)
				characterStats.StatsUnsubscribe(kvp.Key, kvp.Value);
			statsCallbacksDict.Clear();

			// Unsubscribe resistances
			foreach (var kvp in resistanceCallbacksDict)
				characterStats.ResistanceUnsubscribe(kvp.Key, kvp.Value);
			resistanceCallbacksDict.Clear();
		}

		private void OnGUI() {
			if (!canShowState) return;

			if (labelStyle == null)
				labelStyle = new GUIStyle(GUI.skin.label);

			labelStyle.fontSize = Mathf.RoundToInt(12 * scale);
			labelStyle.normal.textColor = Color.white;

			int panelWidth = Mathf.RoundToInt(250 * scale);
			int panelHeight = Mathf.RoundToInt(300 * scale);
			int startX = 10;
			int startY = 10;
			int lineHeight = Mathf.RoundToInt(20 * scale);
			int padding = Mathf.RoundToInt(10 * scale);

			GUI.Box(new Rect(startX, startY, panelWidth, panelHeight), "Character Stats");

			int yOffset = startY + padding;

			// Display normal stats
			GUI.Label(new Rect(startX + padding, yOffset, panelWidth - 2 * padding, lineHeight), "Stats:", labelStyle);
			yOffset += lineHeight;

			foreach (var kvp in statsValues) {
				GUI.Label(new Rect(startX + 2 * padding, yOffset, panelWidth - 3 * padding, lineHeight),
					$"{kvp.Key}: {kvp.Value}", labelStyle);
				yOffset += lineHeight;
			}

			yOffset += padding;
			GUI.Label(new Rect(startX + padding, yOffset, panelWidth - 2 * padding, lineHeight), "Resistances:", labelStyle);
			yOffset += lineHeight;

			foreach (var kvp in resistanceValues) {
				GUI.Label(new Rect(startX + 2 * padding, yOffset, panelWidth - 3 * padding, lineHeight),
					$"{kvp.Key}: {kvp.Value}", labelStyle);
				yOffset += lineHeight;
			}
		}
	}
}