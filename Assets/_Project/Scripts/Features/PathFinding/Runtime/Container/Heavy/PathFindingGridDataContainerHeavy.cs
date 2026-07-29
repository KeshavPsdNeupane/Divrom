using System;
using System.Collections.Generic;
using UnityEngine;
using Project.Scripts.Features.PathFinding.GraphManager;
using Kope.Core.Collections;
using Kope.Core.Attribute;
using Kope.Feature.PathFinding.Node;

namespace Kope.Feature.PathFinding.Data {
	/// <summary>
	/// ScriptableObject container that stores unoptimized, dictionary-heavy pathfinding grid assets.
	/// <para>
	/// <b>DEPRECATED:</b> Preserved strictly as an educational reference and benchmark baseline. 
	/// Do not use in production—causes 155.7 MiB asset bloat (8,349,636 lines of YAML text) at a scale of 30,000 micro nodes.
	/// </para>
	/// </summary>
	[CreateAssetMenu(
		fileName = "PathFindingGridDataContainerHeavy",
		menuName = "Scriptable Objects/PathFinding/Grid Data Container Heavy (Reference Only)"
	)]
	[Obsolete(
		"PathFindingGridDataContainerHeavy causes extreme YAML asset bloat (155.7 MiB / 8,349,636 lines for 30,000 micro nodes). " +
		"Use PathFindingGridDataContainerOptimized (1.2 MiB / 67,846 lines) for production. " +
		"This class is preserved strictly as an optimization benchmark reference."
	)]
	public class PathFindingGridDataContainerHeavy : GridDataContainerBase {

		[Message(
			"WARNING: This is the unoptimized 'Heavy' grid container (155.7 MiB payload for 30,000 micro nodes).\n\n" +
			"It is retained solely as an optimization benchmark reference. " +
			"Please use PathFindingGridDataContainerOptimized for production bakes.",
			MessageSeverity.Error
		)]
		[Header("Baked Data")]
		[SerializeField] private PathFindingGridDataHeavy _gridData;


		private Dictionary<Vec2Int, MicroGridNode> _microGridNodeDict;
		private Dictionary<BoundingBox, MacroGridNode> _macroGridNodeDict;
		private Dictionary<BoundingBox, List<MacroConnectionData>> _macroAdjacencyList;


		public override List<Vec2Int> RegionAnchorPoints => this._gridData.RegionAnchorPoints;

		public override Dictionary<Vec2Int, MicroGridNode> MicroGridNodeDict {
			get {
				if (this._microGridNodeDict == null || this._microGridNodeDict.Count == 0) {
					this._microGridNodeDict = new(this._gridData.MicroGridNodeDict);
				}
				return this._microGridNodeDict;
			}
		}
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

			this._gridData = new PathFindingGridDataHeavy(
				new(microGridNodeDict),
				new(macroGridNodeDict),
				new(macroAdjacencyList),
				new(regionAnchorPoints)
			);

			Debug.Log($"PathFindingGridDataContainerHeavy: Grid data set with {microGridNodeDict.Count} micro " +
					  $"nodes, {macroGridNodeDict.Count} macro nodes, and {macroAdjacencyList.Count} macro connections.");
		}
	}
}