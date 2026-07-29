using System;
using System.Collections.Generic;
using UnityEngine;
using Project.Scripts.Features.PathFinding.GraphManager;
using Kope.Core.Collections;
using Kope.Core.Attribute;
using Kope.Feature.PathFinding.Node;

namespace Kope.Feature.PathFinding.Data {

	/// <summary>
	/// ScriptableObject container that stores baked pathfinding grid data assets.
	/// <para>
	/// <b>DEPRECATED:</b> Preserved strictly as an educational reference and benchmark baseline. 
	/// Generates a ~5 MiB payload at a scale of 30,000 micro nodes—a massive improvement over Heavy (155.7 MiB), 
	/// but still ~4x larger than PathFindingGridDataContainerOptimized (1.2 MiB / 67,846 lines).
	/// </para>
	/// </summary>
	[CreateAssetMenu(
		fileName = "PathFindingGridDataContainerLight",
		menuName = "Scriptable Objects/PathFinding/Grid Data Container Light (Reference Only)"
	)]
	[Obsolete(
		"PathFindingGridDataContainerLight causes intermediate YAML asset bloat (~5 MiB for 30,000 micro nodes). " +
		"Use PathFindingGridDataContainerOptimized (1.2 MiB / 67,846 lines) for production. " +
		"This class is preserved strictly as an optimization benchmark reference."
	)]
	public class PathFindingGridDataContainerLight : GridDataContainerBase {

		[Message(
			"WARNING: This is the deprecated intermediate 'Light' grid container (~5 MiB payload for 30,000 micro nodes).\n\n" +
			"It is retained solely as an optimization benchmark reference (Heavy: 155.7 MiB -> Light: ~5 MiB -> Optimized: 1.2 MiB). " +
			"Please use PathFindingGridDataContainerOptimized for production bakes.",
			MessageSeverity.Warning
		)]
		[Header("Baked Data")]
		[SerializeField] private PathFindingGridDataLight _gridData;

		// Non-serialized runtime lookup table reconstructed on demand from serialized save data.
		private Dictionary<Vec2Int, MicroGridNode> _microGridNodeDict;
		private Dictionary<BoundingBox, MacroGridNode> _macroGridNodeDict;
		private Dictionary<BoundingBox, List<MacroConnectionData>> _macroAdjacencyList;

		public PathFindingGridDataLight RawGridData => this._gridData;
		public override List<Vec2Int> RegionAnchorPoints => this._gridData.RegionAnchorPoints;
		public override Dictionary<BoundingBox, MacroGridNode> MacroGridNodeDict {
			get {
				if (this._macroGridNodeDict == null || this._macroGridNodeDict.Count == 0) {
					this._macroGridNodeDict = new(this._gridData.MacroGridNodeDict);
				}
				return this._macroGridNodeDict;
			}
		}
		public override Dictionary<BoundingBox, List<MacroConnectionData>> MacroAdjacencyList {
			get {
				if (this._macroAdjacencyList == null || this._macroAdjacencyList.Count == 0) {
					this._macroAdjacencyList = new(this._gridData.MacroAdjacencyListWrapper.Count);
					foreach (var kvp in this._gridData.MacroAdjacencyListWrapper) {
						this._macroAdjacencyList[kvp.Key] = new List<MacroConnectionData>(kvp.Value.Connections);
					}
				}
				return this._macroAdjacencyList;
			}
		}

		/// <summary>
		/// Gets the rehydrated runtime dictionary of micro nodes.
		/// Uses lazy instantiation to avoid Unity serialization overhead and enable fast runtime lookups.
		/// </summary>
		public override Dictionary<Vec2Int, MicroGridNode> MicroGridNodeDict {
			get {
				if (this._microGridNodeDict == null || this._microGridNodeDict.Count == 0) {
					RebuildMicroGridNodeCache();
				}
				return this._microGridNodeDict;
			}
		}

		/// <summary>
		/// Clears runtime instance caches to prevent stale node references across play sessions or domain reloads.
		/// </summary>
		public override void ClearRuntimeCache() {
			this._microGridNodeDict?.Clear();
			this._macroGridNodeDict?.Clear();
			this._macroAdjacencyList?.Clear();
			this._microGridNodeDict = null;
			this._macroGridNodeDict = null;
			this._macroAdjacencyList = null;
		}

		/// <summary>
		/// Internal hook called by <see cref="GridDataContainerBase.SetGridData"/>.
		/// Constructs the serialized struct representation and resets runtime caches.
		/// </summary>
		protected override void SetGridDataInternal(
			SerializableDictionary<Vec2Int, MicroGridNode> microGridNodeDict,
			SerializableDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
			SerializableDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList,
			List<Vec2Int> regionAnchorPoints
		) {
			// Convert runtime nodes into compact struct save data to decouple from managed instances
			Dictionary<Vec2Int, MicroGridNodeSaveData> microGridNodeSaveDataDict = new(microGridNodeDict.Count);
			foreach (var kvp in microGridNodeDict) {
				var microNode = kvp.Value;
				MicroGridNodeSaveData data = new(
					microNode.ParentMacroGrid.Bound,
					microNode.IsStaticObstacle
				);
				microGridNodeSaveDataDict[kvp.Key] = data;
			}

			this._gridData = new PathFindingGridDataLight(
				new(microGridNodeSaveDataDict),
				new(macroGridNodeDict),
				new(macroAdjacencyList),
				new(regionAnchorPoints)
			);
			Debug.Log($"PathFindingGridDataContainerLight: Grid data set with {microGridNodeDict.Count} micro " +
					  $"nodes, {macroGridNodeDict.Count} macro nodes, and {macroAdjacencyList.Count} macro connections.");
		}

		private void RebuildMicroGridNodeCache() {
			var saveDataDict = this._gridData.MicroGridNodeSaveDataDict;
			this._microGridNodeDict = new Dictionary<Vec2Int, MicroGridNode>(saveDataDict.Count);

			foreach (var kvp in saveDataDict) {
				var parentMacroGrid = this._gridData.MacroGridNodeDict[kvp.Value.ParentMacroGrid];
				this._microGridNodeDict[kvp.Key] = new MicroGridNode(
					kvp.Key,
					kvp.Value.IsStaticObstacle,
					parentMacroGrid
				);
			}
		}
	}
}