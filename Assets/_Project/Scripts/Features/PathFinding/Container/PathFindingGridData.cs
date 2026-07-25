using System;
using System.Collections.Generic;
using Kope.Core.Attribute;
using Kope.Core.Collections;
using Kope.Feature.PathFinding;
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

	[SerializeField]
	private SerializableDictionary<Vec2Int, MicroGridNode> _microGridNodeDict;

	[SerializeField]
	private SerializableDictionary<BoundingBox, MacroGridNode> _macroGridNodeDict;

	[SerializeField]
	private SerializableDictionary<BoundingBox, MacroConnectionListWrapper> _macroAdjacencyListWrapper;

	public readonly SerializableDictionary<Vec2Int, MicroGridNode> MicroGridNodeDict
		=> _microGridNodeDict;
	public readonly SerializableDictionary<BoundingBox, MacroGridNode> MacroGridNodeDict
		=> _macroGridNodeDict;
	public readonly SerializableDictionary<BoundingBox, MacroConnectionListWrapper> MacroAdjacencyListWrapper
		=> _macroAdjacencyListWrapper;

	public PathFindingGridData(
		SerializableDictionary<Vec2Int, MicroGridNode> microGridNodeDict,
		SerializableDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
		SerializableDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList) {

		this._microGridNodeDict = microGridNodeDict ?? new();
		this._macroGridNodeDict = macroGridNodeDict ?? new();
		this._macroAdjacencyListWrapper = new();
		foreach (var kvp in macroAdjacencyList) {
			this._macroAdjacencyListWrapper[kvp.Key] = new MacroConnectionListWrapper(kvp.Value);
		}

	}
}
