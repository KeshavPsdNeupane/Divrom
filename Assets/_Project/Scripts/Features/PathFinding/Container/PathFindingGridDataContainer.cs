using System.Collections.Generic;
using Kope.Feature.PathFinding;
using Project.Scripts.Features.PathFinding.GraphManager;
using UnityEngine;
using Kope.Core.Collections;
using Kope.Core.Attribute;


#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ScriptableObject container that stores baked pathfinding grid data assets.
/// </summary>
/// <remarks>
/// Functions as the primary serialized data payload generated during the baking pipeline.
/// </remarks>
[CreateAssetMenu(fileName = "PathFindingGridDataContainer",
 menuName = "Scriptable Objects/PathFinding/Grid Data Container", order = 1)]
public class PathFindingGridDataContainer : ScriptableObject {
	[Message(
	"Note: Due to Unity Inspector limitations with dynamic-height elements, " +
	"these fields cannot be fully locked with ReadOnly and can technically be mutated.\n\n" +
	"Manual modification is strongly discouraged. This data is exposed strictly " +
	"for debugging and verifying data integrity. Please leave these fields alone, " +
	"as any manual edits will be overwritten on the next bake."
)]
	[Header("Baked Data")]
	[SerializeField] private PathFindingGridData _gridData;

	/// <summary>
	/// Gets the baked pathfinding grid data stored within this asset.
	/// </summary>
	public PathFindingGridData GridData => this._gridData;

	/// <summary>
	/// Populates and serializes the pathfinding grid datasets.
	/// </summary>
	public void SetGridData(
		SerializableDictionary<Vec2Int, MicroGridNode> microGridNodeDict,
		SerializableDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
		SerializableDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList
	) {

		// FIX 1: Create DEEP COPIES of the dictionaries using your custom constructor.
		// This ensures the ScriptableObject owns its own independent data memory.
		this._gridData = new PathFindingGridData(
			new SerializableDictionary<Vec2Int, MicroGridNode>(microGridNodeDict),
			new SerializableDictionary<BoundingBox, MacroGridNode>(macroGridNodeDict),
			new SerializableDictionary<BoundingBox, List<MacroConnectionData>>(macroAdjacencyList)
		);

		Debug.Log($"PathFindingGridDataContainer: Grid data set with {microGridNodeDict.Count} micro" +
		$" nodes, {macroGridNodeDict.Count} macro nodes, and {macroAdjacencyList.Count} macro connections.");

#if UNITY_EDITOR
		EditorUtility.SetDirty(this);
		AssetDatabase.SaveAssets();
#endif
	}
}