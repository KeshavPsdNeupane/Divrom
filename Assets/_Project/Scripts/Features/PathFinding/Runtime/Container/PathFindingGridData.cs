using System;
using System.Collections.Generic;
using Kope.Core.Attribute;
using Kope.Core.Collections;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFinding.GraphManager;
using UnityEngine;

[Serializable]
public struct MacroConnectionListWrapper {

	[SerializeField, ReadOnly]
	private List<MacroConnectionData> _connections;

	public readonly List<MacroConnectionData> Connections => this._connections;

	public MacroConnectionListWrapper(List<MacroConnectionData> connections) {
		this._connections = connections ?? new();
	}
}


/// <summary>
/// Serializable container holding the full micro and macro pathfinding grid state.
/// </summary>
[Serializable]
public struct PathFindingGridData {

	[SerializeField, DictionaryPageSize(50)]
	private List<Vec2Int> _regionAnchorPoints;


	[SerializeField, DictionaryPageSize(50)]
	private SerializableDictionary<Vec2Int, MicroGridNodeSaveData> _microGridNodeSaveDataDict;

	[SerializeField, DictionaryPageSize(50)]
	private SerializableDictionary<BoundingBox, MacroGridNode> _macroGridNodeDict;

	[SerializeField, DictionaryPageSize(50)]
	private SerializableDictionary<BoundingBox, MacroConnectionListWrapper> _macroAdjacencyListWrapper;



	public readonly List<Vec2Int> RegionAnchorPoints => this._regionAnchorPoints;
	public readonly SerializableDictionary<Vec2Int, MicroGridNodeSaveData> MicroGridNodeSaveDataDict
		=> this._microGridNodeSaveDataDict;
	public readonly SerializableDictionary<BoundingBox, MacroGridNode> MacroGridNodeDict
		=> this._macroGridNodeDict;
	public readonly SerializableDictionary<BoundingBox, MacroConnectionListWrapper> MacroAdjacencyListWrapper
		=> this._macroAdjacencyListWrapper;

	public PathFindingGridData(
		SerializableDictionary<Vec2Int, MicroGridNodeSaveData> microGridNodeSaveDataDict,
		SerializableDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
		SerializableDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList,
		List<Vec2Int> regionAnchorPoints) {

		this._regionAnchorPoints = regionAnchorPoints ?? new();
		this._macroGridNodeDict = macroGridNodeDict ?? new();

		this._macroAdjacencyListWrapper = new();
		this._microGridNodeSaveDataDict = new(macroGridNodeDict.Count);

		this._microGridNodeSaveDataDict = microGridNodeSaveDataDict ?? new();
		foreach (var kvp in macroAdjacencyList) {
			this._macroAdjacencyListWrapper[kvp.Key] = new MacroConnectionListWrapper(kvp.Value);
		}
	}
}
