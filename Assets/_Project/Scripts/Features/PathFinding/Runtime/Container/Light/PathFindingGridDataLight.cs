using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Kope.Core.Attribute;
using Kope.Core.Collections;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFinding.GraphManager;
using UnityEngine;

namespace Kope.Feature.PathFinding.Data {

	/// <summary>
	/// Compact serialization DTO for micro grid node save state.
	/// <para>
	/// <b>DEPRECATED:</b> Preserved strictly as an educational benchmark reference.
	/// Superseded by flat array representations in <c>PathFindingGridDataOptimized</c>.
	/// </para>
	/// </summary>
	[Serializable]
	[Obsolete(
		"MicroGridNodeSaveData is deprecated alongside PathFindingGridDataLight. " +
		"Use PathFindingGridDataOptimized and its flat array representations for production bakes."
	)]
	public struct MicroGridNodeSaveData {
		// Internal field names are optimized for serialization size, not readability.
		// Access data via public properties instead.
		[SerializeField, ReadOnly] private BoundingBox _pmG;
		[SerializeField, ReadOnly] private byte _isO;

		public readonly BoundingBox ParentMacroGrid {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._pmG;
		}

		public readonly bool IsStaticObstacle {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this._isO != 0;
		}

		public MicroGridNodeSaveData(BoundingBox parentMacroGrid, bool isStaticObstacle) {
			this._pmG = parentMacroGrid;
			this._isO = (byte)(isStaticObstacle ? 1 : 0);
		}
	}

	/// <summary>
	/// Intermediate serializable container holding pathfinding grid state.
	/// <para>
	/// <b>DEPRECATED:</b> Preserved strictly as an educational reference and benchmark baseline. 
	/// Direct dictionary serialization of struct save data causes ~5 MiB asset bloat at 30,000 micro nodes vs Optimized (1.2 MiB / 67,846 lines).
	/// </para>
	/// </summary>
	[Serializable]
	[Obsolete(
		"PathFindingGridDataLight causes intermediate YAML asset bloat (~5 MiB for 30,000 micro nodes). " +
		"Use PathFindingGridDataOptimized (1.2 MiB / 67,846 lines) for production. " +
		"This struct is preserved strictly as an optimization benchmark reference."
	)]
	public struct PathFindingGridDataLight {

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

		public PathFindingGridDataLight(
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
}