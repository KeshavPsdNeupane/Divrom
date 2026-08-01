using System.Collections.Generic;
using UnityEngine;
using Project.Scripts.Features.PathFindingOld.GraphManager;
using Kope.Core.Collections;
using Kope.Core.Attribute;
using Kope.Feature.PathFindingOld.Node;

namespace Kope.Feature.PathFindingOld.Data {

	/// <summary>
	/// ScriptableObject container that stores baked pathfinding grid data assets in a high-density, 
	/// flattened format on disk, and lazily re-hydrates full O(1) graph dictionaries at runtime.
	/// Delegation and transformation logic is managed via <see cref="GridDataCodexPacked"/>.
	/// </summary>
	[CreateAssetMenu(
		fileName = "GridDataContainerPacked",
		menuName = "Scriptable Objects/PathFinding/Grid Data Container Packed"
	)]
	public class GridDataContainerPacked : GridDataContainerBase {

		#region Serialized Fields

		[Message(
			"Note: Due to Unity Inspector limitations with dynamic-height elements, " +
			"these fields cannot be fully locked with ReadOnly and can technically be mutated.\n\n" +
			"Manual modification is strongly discouraged. This data is exposed strictly " +
			"for debugging and verifying data integrity. Please leave these fields alone, " +
			"as any manual edits will be overwritten on the next bake.",
			MessageSeverity.Warning
		)]
		[Header("Baked Packed Data")]
		[SerializeField] private GridDataPacked _gridData;

		#endregion

		#region Non-Serialized Runtime Caches

		/*
         * NON-SERIALIZED RUNTIME CACHES
         * Marked transient so Unity never saves them to asset disk space.
         * Populated lazily via GridDataCodexPacked on first property access.
         */
		private Dictionary<Vec2Int, MicroGridNode> _microGridNodeDict;
		private Dictionary<BoundingBox, MacroGridNode> _macroGridNodeDict;
		private Dictionary<BoundingBox, List<MacroConnectionData>> _macroAdjacencyList;

		#endregion

		#region Domain Properties (Lazy Getters)

		/// <summary>Global anchor points defining the macro regions across the entire grid.</summary>
		public override List<Vec2Int> RegionAnchorPoints => this._gridData.RegionAnchorPoints;

		/// <summary>
		/// Map of macro region bounding boxes to their live <see cref="MacroGridNode"/> instances.
		/// Lazily re-hydrates runtime caches if not currently populated.
		/// </summary>
		public override Dictionary<BoundingBox, MacroGridNode> MacroGridNodeDict {
			get {
				if (this._macroGridNodeDict == null || this._macroGridNodeDict.Count == 0) {
					RebuildRuntimeCaches();
				}
				return this._macroGridNodeDict;
			}
		}

		/// <summary>
		/// Adjacency lookup mapping each macro region bounding box to its outgoing graph edges.
		/// Lazily re-hydrates runtime caches if not currently populated.
		/// </summary>
		public override Dictionary<BoundingBox, List<MacroConnectionData>> MacroAdjacencyList {
			get {
				if (this._macroAdjacencyList == null || this._macroAdjacencyList.Count == 0) {
					RebuildRuntimeCaches();
				}
				return this._macroAdjacencyList;
			}
		}

		/// <summary>
		/// O(1) spatial lookup mapping grid coordinates (Vec2Int) to live <see cref="MicroGridNode"/> instances.
		/// Lazily re-hydrates runtime caches if not currently populated.
		/// </summary>
		public override Dictionary<Vec2Int, MicroGridNode> MicroGridNodeDict {
			get {
				if (this._microGridNodeDict == null || this._microGridNodeDict.Count == 0) {
					RebuildRuntimeCaches();
				}
				return this._microGridNodeDict;
			}
		}

		#endregion

		#region Public Cache Control

		/// <summary>
		/// Purges in-memory runtime dictionaries. Forces fresh re-hydration on next property access.
		/// Useful during level transitions, scene unloads, or re-bakes to release GC memory.
		/// </summary>
		public override void ClearRuntimeCache() {
			this._microGridNodeDict?.Clear();
			this._macroGridNodeDict?.Clear();
			this._macroAdjacencyList?.Clear();
			this._microGridNodeDict = null;
			this._macroGridNodeDict = null;
			this._macroAdjacencyList = null;
		}

		#endregion

		#region Baking Pipeline (SetGridDataInternal)

		/// <summary>
		/// Delegates spatial graph flattening and bit-packing to <see cref="GridDataCodexPacked.BakeStatic"/>.
		/// </summary>
		protected override void SetGridDataInternal(
			SerializableDictionary<Vec2Int, MicroGridNode> microGridNodeDict,
			SerializableDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
			SerializableDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList,
			List<Vec2Int> regionAnchorPoints
		) {
			this._gridData = GridDataCodexPacked.BakeStatic(
				microGridNodeDict,
				macroGridNodeDict,
				macroAdjacencyList,
				regionAnchorPoints
			);
			// no need to clear this the base class is already doing that in the SetGridData method
			Debug.Log($"GridDataContainerPacked: Grid data baked for {microGridNodeDict.Count} micro nodes across {macroGridNodeDict.Count} macro regions via GridDataCodexPacked.");
		}

		#endregion

		#region Runtime Re-hydration (RebuildRuntimeCaches)

		/// <summary>
		/// Delegates stream reading and runtime map construction to <see cref="GridDataCodexPacked.HydrateStatic"/>.
		/// </summary>
		private void RebuildRuntimeCaches() {
			var cache = GridDataCodexPacked.HydrateStatic(in this._gridData);
			this._microGridNodeDict = cache.MicroGridNodeDict;
			this._macroGridNodeDict = cache.MacroGridNodeDict;
			this._macroAdjacencyList = cache.MacroAdjacencyList;
		}

		#endregion
	}
}