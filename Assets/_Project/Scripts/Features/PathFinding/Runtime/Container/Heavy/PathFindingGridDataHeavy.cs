using System;
using System.Collections.Generic;
using Kope.Core.Attribute;
using Kope.Core.Collections;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFinding.GraphManager;
using UnityEngine;

namespace Kope.Feature.PathFinding.Data {

	/// <summary>
	/// Serializable container holding the full micro and macro pathfinding grid state.
	/// <para>
	/// <b>DEPRECATED:</b> Preserved strictly as an educational reference and benchmark baseline. 
	/// Direct dictionary serialization causes 155.7 MiB asset bloat (8,349,636 lines of YAML text for 30,000 micro nodes).
	/// </para>
	/// </summary>
	[Serializable]
	[Obsolete(
		"PathFindingGridDataHeavy causes extreme YAML asset bloat (155.7 MiB / 8,349,636 lines for 30,000 micro nodes). " +
		"Use PathFindingGridDataOptimized (1.2 MiB / 67,846 lines) for production. " +
		"This struct is preserved strictly as an optimization benchmark reference."
	)]
	public struct PathFindingGridDataHeavy {

		[SerializeField, DictionaryPageSize(50)]
		private List<Vec2Int> _regionAnchorPoints;

		[SerializeField, DictionaryPageSize(50)]
		private SerializableDictionary<Vec2Int, MicroGridNode> _microGridNodeDict;

		[SerializeField, DictionaryPageSize(50)]
		private SerializableDictionary<BoundingBox, MacroGridNode> _macroGridNodeDict;

		[SerializeField, DictionaryPageSize(50)]
		private SerializableDictionary<BoundingBox, MacroConnectionListWrapper> _macroAdjacencyListWrapper;


		public readonly List<Vec2Int> RegionAnchorPoints => this._regionAnchorPoints;
		public readonly SerializableDictionary<Vec2Int, MicroGridNode> MicroGridNodeDict
			=> this._microGridNodeDict;
		public readonly SerializableDictionary<BoundingBox, MacroGridNode> MacroGridNodeDict
			=> this._macroGridNodeDict;
		public readonly SerializableDictionary<BoundingBox, MacroConnectionListWrapper> MacroAdjacencyListWrapper
			=> this._macroAdjacencyListWrapper;

		public PathFindingGridDataHeavy(
			SerializableDictionary<Vec2Int, MicroGridNode> microGridNodeSaveDataDict,
			SerializableDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
			SerializableDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList,
			List<Vec2Int> regionAnchorPoints) {

			this._regionAnchorPoints = regionAnchorPoints ?? new();
			this._macroGridNodeDict = macroGridNodeDict ?? new();

			this._macroAdjacencyListWrapper = new();
			this._microGridNodeDict = new(macroGridNodeDict.Count);

			this._microGridNodeDict = microGridNodeSaveDataDict ?? new();
			foreach (var kvp in macroAdjacencyList) {
				this._macroAdjacencyListWrapper[kvp.Key] = new MacroConnectionListWrapper(kvp.Value);
			}
		}
	}
}