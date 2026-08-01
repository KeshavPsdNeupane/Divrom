using System.Collections.Generic;
using UnityEngine;
using Project.Scripts.Features.PathFindingOld.GraphManager;
using Kope.Core.Collections;
using Kope.Core.Attribute;
using Kope.Feature.PathFinding.Node;

namespace Kope.Feature.PathFinding.Data {

	/// <summary>
	/// ScriptableObject container that persists bit-packed grid graphs to disk and manages 
	/// transient runtime graph cache lifecycles. All stream bit-packing and re-hydration 
	/// logic is delegated to <see cref="GridDataCodexGlobalStream"/>.
	/// </summary>
	[CreateAssetMenu(
		fileName = "PathFindingGridDataContainer (GlobalStream)",
		menuName = "Scriptable Objects/PathFinding/Path Finding Grid Data Container (GlobalStream)"
	)]
	public class GridDataContainerGlobalStream : GridDataContainerBase {

		#region Serialized Fields

		/// <summary>
		/// Monolithic serialized grid payload containing bit-packed 64-bit slice range streams
		/// and primitive master arrays.
		/// </summary>
		[Message(
			"Note: Due to Unity Inspector limitations with dynamic-height elements, " +
			"these fields cannot be fully locked with ReadOnly and can technically be mutated.\n\n" +
			"Manual modification is strongly discouraged. This data is exposed strictly " +
			"for debugging and verifying data integrity. Please leave these fields alone, " +
			"as any manual edits will be overwritten on the next bake.",
			MessageSeverity.Warning
		)]
		[Header("Baked Glob Data")]
		[SerializeField] private GridDataGlobalStream _gridData;

		#endregion

		#region Non-Serialized Runtime Caches

		/*
         * NON-SERIALIZED RUNTIME CACHES
         * Marked transient so Unity never saves them to asset disk space.
         * Populated lazily via GridDataCodexGlobalStream on first property access.
         */
		private List<Vec2Int> _regionAnchorPoints;

		/// <summary> Runtime lookup map for MicroGridNodes by 2D grid coordinates. </summary>
		private Dictionary<Vec2Int, MicroGridNode> _microGridNodeDict;

		/// <summary> Runtime lookup map for MacroGridNodes by 3D BoundingBoxes. </summary>
		private Dictionary<BoundingBox, MacroGridNode> _macroGridNodeDict;

		/// <summary> Runtime adjacency list mapping macro regions to outgoing connection edges. </summary>
		private Dictionary<BoundingBox, List<MacroConnectionData>> _macroAdjacencyList;

		#endregion

		#region Domain Properties (Lazy Getters)

		/// <summary>
		/// Spatial anchor points defining macro region world alignments.
		/// Read directly from the serialized payload.
		/// </summary>
		public override List<Vec2Int> RegionAnchorPoints {
			get {
				if (this._regionAnchorPoints == null || this._regionAnchorPoints.Count == 0) {
					this._regionAnchorPoints = this._gridData.RegionAnchorPoints;
				}
				return this._regionAnchorPoints;
			}
		}

		/// <summary>
		/// Returns the macro grid node dictionary. Automatically triggers lazy cache 
		/// re-hydration via <see cref="GridDataCodexGlobalStream"/> if uninitialized.
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
		/// Returns the macro graph adjacency list. Automatically triggers lazy cache 
		/// re-hydration via <see cref="GridDataCodexGlobalStream"/> if uninitialized.
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
		/// Returns the micro grid node lookup table. Automatically triggers lazy cache 
		/// re-hydration via <see cref="GridDataCodexGlobalStream"/> if uninitialized.
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
		/// Releases all instantiated runtime graph objects and dictionary handles.
		/// Call this when unloading scenes to let the GC collect runtime graph nodes while keeping 
		/// the compact serialized primitive stream intact on disk.
		/// </summary>
		public override void ClearRuntimeCache() {
			this._regionAnchorPoints?.Clear();
			this._microGridNodeDict?.Clear();
			this._macroGridNodeDict?.Clear();
			this._macroAdjacencyList?.Clear();

			this._regionAnchorPoints = null;
			this._microGridNodeDict = null;
			this._macroGridNodeDict = null;
			this._macroAdjacencyList = null;
		}

		#endregion

		#region Internal Pipeline Execution

		/// <summary>
		/// Delegates spatial graph flattening and bit-packing to <see cref="GridDataCodexGlobalStream.BakeStatic"/>.
		/// </summary>
		protected override void SetGridDataInternal(
			SerializableDictionary<Vec2Int, MicroGridNode> microGridNodeDict,
			SerializableDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
			SerializableDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList,
			List<Vec2Int> regionAnchorPoints
		) {
			this._gridData = GridDataCodexGlobalStream.BakeStatic(
				microGridNodeDict,
				macroGridNodeDict,
				macroAdjacencyList,
				regionAnchorPoints
			);
			// no need to clear this the base class is already doing that in the SetGridData method

			Debug.Log($"PathFindingGridDataContainerGlobalStream: Successfully baked bit-packed streams via GridDataCodexGlobalStream.");
		}

		/// <summary>
		/// Delegates stream reading and runtime map construction to <see cref="GridDataCodexGlobalStream.HydrateStatic"/>.
		/// </summary>
		private void RebuildRuntimeCaches() {
			var cache = GridDataCodexGlobalStream.HydrateStatic(in this._gridData);
			this._microGridNodeDict = cache.MicroGridNodeDict;
			this._macroGridNodeDict = cache.MacroGridNodeDict;
			this._macroAdjacencyList = cache.MacroAdjacencyList;
		}

		#endregion
	}
}