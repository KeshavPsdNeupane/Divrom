using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using Kope.Core.CompilerServices;
using Kope.Core.Init;
using Kope.Character.Stats;
using Kope.Core.EntityComponentSystem;

public class StatDescription : InitializableBase {
	[SerializeField] private GameObject statDescriptionUIPanel;
	[SerializeField] private EntityComponentsRegistry ecr;

	[SerializeField] private TMP_FontAsset fontAsset;

	private CharacterStatsSystem characterStats;
	private readonly Dictionary<CharacterStatType, float> statsValues = new();
	private readonly Dictionary<CharacterStatType, UnityAction<float>> statsCallbacksDict = new();
	private readonly Dictionary<CharacterStatType, TextMeshProUGUI> statTextObjects = new();

	private RectTransform panelRect; // cache panelRect to avoid fetching multiple times

	protected override bool OnInit() {
		return Validate();
	}



	void OnEnable() {
		if (this.characterStats != null &&
			this.characterStats.CurrentStats != null &&
			this.statDescriptionUIPanel != null) { // with Init , no need to Validate again here 
			CreateTMPObjects();
			SubscribeToStats();
		}
	}

	void OnDisable() {
		if (this.characterStats == null) return;

		foreach (var kvp in this.statsCallbacksDict)
			this.characterStats.StatsUnsubscribe(kvp.Key, kvp.Value);

		this.statsCallbacksDict.Clear();
	}

	private bool Validate() {
		if (this.characterStats == null) {
			if (ecr == null) {
				MyLogger.Error("EntityComponentStore reference was missing," +
			  " unable to retrieve CharacterStatsSystem." + GetParentGameObjectHeirarchyMessage());
				return false;
			}
			// using tryGet since this only shows the stats on UI and does not modify the stats,
			//  so we don't need mutatable access here. so TryGetComponent is sufficient for semantic clarity.
			if (ecr.ComponentRegistry.TryGetComponent<CharacterStatsSystem>(out var statsSystem)) {
				this.characterStats = statsSystem;
			} else {
				MyLogger.Error("No CharacterStatsSystem found in EntityComponentStore for StatDescription" + GetParentGameObjectHeirarchyMessage());
				return false;
			}
		}
		if (this.statDescriptionUIPanel == null) {
			MyLogger.Error("StatDescriptionUIPanel is not assigned." + GetParentGameObjectHeirarchyMessage());
			return false;
		}
		if (this.panelRect == null && this.statDescriptionUIPanel != null) {
			this.panelRect = this.statDescriptionUIPanel.GetComponent<RectTransform>();
			if (this.panelRect == null)
				MyLogger.Error("Stat Panel requires RectTransform." + GetParentGameObjectHeirarchyMessage());
		}
		return true;
	}

	private void CreateTMPObjects() {
		if (this.panelRect == null) return;

		int numberOfStats = Enum.GetValues(typeof(CharacterStatType)).Length;
		float panelHeight = this.panelRect.rect.height;
		float lineHeight = panelHeight / (numberOfStats + 1);

		int index = 0;
		foreach (CharacterStatType type in Enum.GetValues(typeof(CharacterStatType))) {
			if (statTextObjects.ContainsKey(type)) continue;

			GameObject textGO = new GameObject(type.ToString());
			textGO.transform.SetParent(statDescriptionUIPanel.transform, false);

			TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
			tmp.font = fontAsset;
			tmp.alignment = TextAlignmentOptions.Left;
			tmp.color = Color.black;
			tmp.text = $"{type}: 0";

			RectTransform rt = tmp.GetComponent<RectTransform>();
			rt.anchorMin = new Vector2(0, 1);
			rt.anchorMax = new Vector2(0, 1);
			rt.pivot = new Vector2(0, 1);
			rt.anchoredPosition = new Vector2(10f, -index * lineHeight);
			rt.sizeDelta = new Vector2(panelRect.rect.width - 20f, lineHeight);

			tmp.enableAutoSizing = true;
			tmp.fontSizeMin = 4;
			tmp.fontSizeMax = 200;

			statTextObjects[type] = tmp;
			index++;
		}
	}

	private void SubscribeToStats() {
		if (this.characterStats == null || this.characterStats.CurrentStats == null) return;

		foreach (CharacterStatType type in Enum.GetValues(typeof(CharacterStatType))) {
			// check if the stat exists to avoid errors
			if (!this.characterStats.CurrentStats.ContainsKey(type)) continue;

			this.statsValues[type] = this.characterStats.GetStatValue(type);

			void callback(float val) {
				this.statsValues[type] = val;
				if (this.statTextObjects.TryGetValue(type, out var tmp))
					tmp.text = $"{type}: {val:0}";
			}

			// Unsubscribe previous callback if exists
			if (this.statsCallbacksDict.TryGetValue(type, out var oldCallback))
				this.characterStats.StatsUnsubscribe(type, oldCallback);

			this.statsCallbacksDict[type] = callback;
			this.characterStats.StatsSubscribe(type, callback);

			// Immediately update TMP text
			if (statTextObjects.TryGetValue(type, out var text))
				text.text = $"{type}: {statsValues[type]:0}";
		}
	}

}
