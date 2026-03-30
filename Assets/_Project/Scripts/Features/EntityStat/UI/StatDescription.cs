using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using Kope.Core.CompilerServices;
using Kope.Core.Init;
using Kope.Character.Stats;
using Kope.Core.EntityComponentSystem;
using Kope.Component.Health;


public class StatDescription : InitializableBase {
	[SerializeField] private GameObject statDescriptionUIPanel;
	[SerializeField] private EntityComponentsRegistry ecr;

	[SerializeField] private TMP_FontAsset fontAsset;

	private CharacterStatsSystem _characterStats;
	private IHealthComponent _healthComponent;

	private readonly Dictionary<CharacterStatType, float> _statsValues = new();
	private readonly Dictionary<CharacterStatType, UnityAction<float>> _statsCallbacksDict = new();
	private readonly Dictionary<CharacterStatType, TextMeshProUGUI> _statTextObjects = new();
	private float _currentHealth;

	private RectTransform _panelRect; // cache panelRect to avoid fetching multiple times

	protected override bool OnInit() {
		return Validate();
	}
	private void SetCurrentHp(float hp) => this._currentHealth = hp;


	void OnEnable() {
		if (this._characterStats != null &&
			this._characterStats.CurrentStats != null &&
			this.statDescriptionUIPanel != null &&
			this._healthComponent != null) { // with Init , no need to Validate again here since OnEnable will only be called after successful Init, but we do need to check for nulls to avoid errors in case of unexpected issues.
			CreateTMPObjects();
			SubscribeToStats();
		}
	}

	void OnDisable() {
		if (this._characterStats == null || this._healthComponent == null) return;
		this._healthComponent.OnCurrentHealthChanged -= SetCurrentHp;
		foreach (var kvp in this._statsCallbacksDict)
			this._characterStats.StatsUnsubscribe(kvp.Key, kvp.Value);

		this._statsCallbacksDict.Clear();
	}

	private bool Validate() {
		if (ecr == null) {
			MyLogger.Error("EntityComponentStore reference was missing," +
		  " unable to retrieve CharacterStatsSystem." + GetParentGameObjectHeirarchyMessage());
			return false;
		}
		// using tryGet since this only shows the stats on UI and does not modify the stats,
		//  so we don't need mutatable access here. so TryGetComponent is sufficient for semantic clarity.
		if (!ecr.ComponentRegistry.TryGetReadOnlyComponent<CharacterStatsSystem>(out var statsSystem)) {
			MyLogger.Error("No CharacterStatsSystem found in EntityComponentStore for StatDescription" + GetParentGameObjectHeirarchyMessage());
			return false;

		}
		this._characterStats = statsSystem;

		if (!ecr.ComponentRegistry.TryGetReadOnlyComponent<IHealthComponent>(out var healthComponent)) {
			MyLogger.Error("No IHealthComponent found in EntityComponentStore for StatDescription" + GetParentGameObjectHeirarchyMessage());
			return false;

		}
		this._healthComponent = healthComponent;

		if (this.statDescriptionUIPanel == null) {
			MyLogger.Error("StatDescriptionUIPanel is not assigned." + GetParentGameObjectHeirarchyMessage());
			return false;
		}
		if (this._panelRect == null && this.statDescriptionUIPanel != null) {
			this._panelRect = this.statDescriptionUIPanel.GetComponent<RectTransform>();
			if (this._panelRect == null)
				MyLogger.Error("Stat Panel requires RectTransform." + GetParentGameObjectHeirarchyMessage());
		}
		return true;
	}

	private void CreateTMPObjects() {
		if (this._panelRect == null) return;

		int numberOfStats = Enum.GetValues(typeof(CharacterStatType)).Length;
		float panelHeight = this._panelRect.rect.height;
		float lineHeight = panelHeight / (numberOfStats + 1);

		int index = 0;
		foreach (CharacterStatType type in Enum.GetValues(typeof(CharacterStatType))) {
			if (_statTextObjects.ContainsKey(type)) continue;

			GameObject textGO = new(type.ToString());
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
			rt.sizeDelta = new Vector2(_panelRect.rect.width - 20f, lineHeight);

			tmp.enableAutoSizing = true;
			tmp.fontSizeMin = 4;
			tmp.fontSizeMax = 200;

			_statTextObjects[type] = tmp;
			index++;
		}
	}

	private void SubscribeToStats() {
		if (this._characterStats == null || this._characterStats.CurrentStats == null
		|| this._healthComponent == null) return;
		// initial fetch of current health to display in UI immediately, and subscribe to changes for future updates.
		this._currentHealth = this._healthComponent.CurrentHealth;
		this._healthComponent.OnCurrentHealthChanged += SetCurrentHp;

		foreach (CharacterStatType type in Enum.GetValues(typeof(CharacterStatType))) {
			// check if the stat exists to avoid errors
			if (!this._characterStats.CurrentStats.ContainsKey(type)) continue;

			this._statsValues[type] = this._characterStats.GetStatValue(type);

			void callback(float val) {
				this._statsValues[type] = val;
				if (this._statTextObjects.TryGetValue(type, out var tmp))
					tmp.text = $"{type}: {val:0}";
			}

			// Unsubscribe previous callback if exists
			if (this._statsCallbacksDict.TryGetValue(type, out var oldCallback))
				this._characterStats.StatsUnsubscribe(type, oldCallback);

			this._statsCallbacksDict[type] = callback;
			this._characterStats.StatsSubscribe(type, callback);

			// Immediately update TMP text
			if (_statTextObjects.TryGetValue(type, out var text)) {
				string display;
				if (type == CharacterStatType.HP) {
					display = $"{type}: {this._currentHealth:0}/{this._statsValues[type]:0}";
				} else {
					display = $"{type}: {this._statsValues[type]:0}";
				}
				text.text = display;
			}


		}
	}

}
