using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelSystemConfig))]
public class LevelSystemConfigEditor : Editor {

	private bool showTable = false;
	private bool showDelta = false;
	private Vector2 scrollPosition;

	// Variables to hold inspector state for the lookup tool
	private float testXpInput = 0f;
	private int testResultLevel = -1;

	public override void OnInspectorGUI() {
		// Draw standard fields (maxLevel, baseExp, etc.)
		DrawDefaultInspector();

		LevelSystemConfig config = (LevelSystemConfig)target;
		float[] tableData = config.ExpRequiredForLevel;

		if (tableData == null || tableData.Length == 0) {
			EditorGUILayout.HelpBox("Table will populate when the asset initializes.", MessageType.Info);
			return;
		}

		EditorGUILayout.Space(15);

		// --- NEW: REVERSE SEARCH LOOKUP TESTER ---
		EditorGUILayout.BeginVertical(EditorStyles.helpBox);
		EditorGUILayout.LabelField("🔬 Test Reverse Search Lookup", EditorStyles.boldLabel);

		EditorGUILayout.BeginHorizontal();
		// 1. User Inputs an EXP Value
		testXpInput = EditorGUILayout.FloatField("Enter Cumulative EXP:", testXpInput);

		// 2. Button Triggers the Binary Search
		if (GUILayout.Button("Get Level", GUILayout.Width(100))) {
			testResultLevel = config.GetLevelFromCumulativeXp(testXpInput);
		}
		EditorGUILayout.EndHorizontal();

		// 3. Display the Output Results Dynamically
		if (testResultLevel != -1) {
			EditorGUILayout.Space(2);
			string resultText = $"Result: An EXP of <b>{testXpInput:N0}</b> puts the player at <b>Lv. {testResultLevel}</b>";

			GUIStyle richLabelStyle = new GUIStyle(EditorStyles.label) { richText = true };
			EditorGUILayout.LabelField(resultText, richLabelStyle);
		}
		EditorGUILayout.EndVertical();

		EditorGUILayout.Space(10);

		// --- HEADER UTILITY ROW ---
		EditorGUILayout.BeginHorizontal();

		// Left Side: Dropdown arrow
		showTable = EditorGUILayout.Foldout(showTable, "View EXP Table", true, EditorStyles.foldoutHeader);

		// Middle-Right: Toggle button for EXP step differences
		string toggleText = showDelta ? "📊 Hide Growth Δ" : "📊 Show Growth Δ";
		if (GUILayout.Button(toggleText, GUILayout.ExpandWidth(false))) {
			showDelta = !showDelta;
		}

		// Far-Right: Copy Button
		if (GUILayout.Button("📋 Copy as Markdown", GUILayout.ExpandWidth(false))) {
			CopyTableToClipboard(tableData);
		}

		EditorGUILayout.EndHorizontal();

		// --- RENDER TABLE ---
		if (showTable) {
			EditorGUI.indentLevel++;
			scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(350));

			float levelWidth = 70f;
			float expNeedWidth = showDelta ? 100f : 120f;
			float deltaWidth = 90f;

			// --- TABLE HEADERS ---
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("NextLevel", EditorStyles.boldLabel, GUILayout.Width(levelWidth));
			EditorGUILayout.LabelField("ExpNeed", EditorStyles.boldLabel, GUILayout.Width(expNeedWidth));

			if (showDelta) {
				EditorGUILayout.LabelField("Growth Δ", EditorStyles.boldLabel, GUILayout.Width(deltaWidth));
			}

			EditorGUILayout.LabelField("CumulativeExp", EditorStyles.boldLabel);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space(3);

			// --- ROW 1: BASELINE (Lv. 1) ---
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Lv. 1", GUILayout.Width(levelWidth));
			EditorGUILayout.LabelField("0 XP", GUILayout.Width(expNeedWidth));

			if (showDelta) {
				EditorGUILayout.LabelField("-", GUILayout.Width(deltaWidth));
			}

			EditorGUILayout.LabelField("0 XP");
			EditorGUILayout.EndHorizontal();

			// --- DYNAMIC ROWS ---
			float previousExpNeed = 0f;

			for (int i = 1; i < tableData.Length; i++) {
				int targetLevel = i + 1;
				float expNeed = tableData[i] - tableData[i - 1];
				float growthDelta = expNeed - previousExpNeed;

				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField($"Lv. {targetLevel}", GUILayout.Width(levelWidth));
				EditorGUILayout.LabelField(string.Format("{0:N0} XP", expNeed), GUILayout.Width(expNeedWidth));

				if (showDelta) {
					string deltaString = (i == 1) ? "0 XP" : string.Format("+{0:N0} XP", growthDelta);
					EditorGUILayout.LabelField(deltaString, GUILayout.Width(deltaWidth));
				}

				EditorGUILayout.LabelField(string.Format("{0:N0} XP", tableData[i]));
				EditorGUILayout.EndHorizontal();

				previousExpNeed = expNeed;
			}

			EditorGUILayout.EndScrollView();
			EditorGUI.indentLevel--;
		}
	}

	private void CopyTableToClipboard(float[] tableData) {
		StringBuilder sb = new StringBuilder();

		if (showDelta) {
			sb.AppendLine("| NextLevel | ExpNeed | Growth Δ | CumulativeExp |");
			sb.AppendLine("| :--- | :--- | :--- | :--- |");
			sb.AppendLine("| Lv. 1 | 0 | - | 0 |");
		} else {
			sb.AppendLine("| NextLevel | ExpNeed | CumulativeExp |");
			sb.AppendLine("| :--- | :--- | :--- |");
			sb.AppendLine("| Lv. 1 | 0 | 0 |");
		}

		float previousExpNeed = 0f;

		for (int i = 1; i < tableData.Length; i++) {
			int targetLevel = i + 1;
			float expNeed = tableData[i] - tableData[i - 1];
			float growthDelta = expNeed - previousExpNeed;

			if (showDelta) {
				string deltaStr = (i == 1) ? "0" : $"+{(int)growthDelta}";
				sb.AppendLine($"| Lv. {targetLevel} | {(int)expNeed} | {deltaStr} | {(int)tableData[i]} |");
			} else {
				sb.AppendLine($"| Lv. {targetLevel} | {(int)expNeed} | {(int)tableData[i]} |");
			}

			previousExpNeed = expNeed;
		}

		EditorGUIUtility.systemCopyBuffer = sb.ToString();
		this.Repaint();
		Debug.Log("<color=green><b>LevelSystemConfig:</b></color> Markdown table copied cleanly to clipboard!");
	}
}