using UnityEngine;
using Kope.Core.Collections;
using Kope.Feature.PathFinding.Node;
using System.Collections.Generic;
using Project.Scripts.Features.PathFindingOld.GraphManager;





#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kope.Feature.PathFinding.Data {
	/// <summary>
	/// Abstract non-generic ScriptableObject container for baked graph/grid data assets.
	/// Manages Editor persistence pipelines, dirtying, and runtime cache lifecycles.
	/// </summary>
	public abstract class GridDataContainerBase : ScriptableObject {
		public abstract List<Vec2Int> RegionAnchorPoints { get; }
		public abstract Dictionary<Vec2Int, MicroGridNode> MicroGridNodeDict { get; }
		public abstract Dictionary<BoundingBox, MacroGridNode> MacroGridNodeDict { get; }
		public abstract Dictionary<BoundingBox, List<MacroConnectionData>> MacroAdjacencyList { get; }




		/// <summary>
		/// Clears non-serialized runtime caches (e.g., rehydrated runtime dictionaries).
		/// </summary>
		public abstract void ClearRuntimeCache();



		public void SetGridData(SerializableDictionary<Vec2Int, MicroGridNode> microGridNodeDict,
		SerializableDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
		SerializableDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList,
		List<Vec2Int> regionAnchorPoints) {
			// forced rehydration of runtime caches, clear any existing runtime caches before setting new data
			ClearRuntimeCache();
			SetGridDataInternal(microGridNodeDict, macroGridNodeDict, macroAdjacencyList, regionAnchorPoints);
			// Invalidate runtime cache so future requests force a re-hydration from newly baked data
			ClearRuntimeCache();
#if UNITY_EDITOR
			// no need to even waste time on calling the function in builds, 
			// since this is only used for editor persistence and dirtying.
			SaveAndDirtyAsset();
#endif

		}


		protected abstract void SetGridDataInternal(SerializableDictionary<Vec2Int, MicroGridNode> microGridNodeDict,
		SerializableDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
		SerializableDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList,
		List<Vec2Int> regionAnchorPoints);

#if UNITY_EDITOR
		protected void SaveAndDirtyAsset() {
			EditorUtility.SetDirty(this);
			AssetDatabase.SaveAssets();
		}
#endif

		protected virtual void OnDisable() {
			ClearRuntimeCache();
		}
	}
}