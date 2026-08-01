using System.Collections.Generic;
using Kope.Feature.PathFinding.Node;
using Project.Scripts.Features.PathFindingOld.GraphManager;

namespace Kope.Feature.PathFinding.Data {

	/// <summary>
	/// Unified input parameter container for grid graph baking operations.
	/// Encapsulates live C# domain dictionaries and anchor points into a single payload.
	/// </summary>
	public readonly struct GridDataBakeInput {
		public IDictionary<Vec2Int, MicroGridNode> MicroGridNodeDict { get; }
		public IDictionary<BoundingBox, MacroGridNode> MacroGridNodeDict { get; }
		public IDictionary<BoundingBox, List<MacroConnectionData>> MacroAdjacencyList { get; }
		public List<Vec2Int> RegionAnchorPoints { get; }

		public GridDataBakeInput(
			IDictionary<Vec2Int, MicroGridNode> microGridNodeDict,
			IDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
			IDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList,
			List<Vec2Int> regionAnchorPoints
		) {
			MicroGridNodeDict = microGridNodeDict;
			MacroGridNodeDict = macroGridNodeDict;
			MacroAdjacencyList = macroAdjacencyList;
			RegionAnchorPoints = regionAnchorPoints;
		}
	}

	/// <summary>
	/// Unified runtime graph cache holding re-hydrated $O(1)$ spatial lookup maps.
	/// Used as the standard output target across all grid hydration codex implementations.
	/// </summary>
	public sealed class GridDataRuntimeCache {
		public Dictionary<Vec2Int, MicroGridNode> MicroGridNodeDict { get; }
		public Dictionary<BoundingBox, MacroGridNode> MacroGridNodeDict { get; }
		public Dictionary<BoundingBox, List<MacroConnectionData>> MacroAdjacencyList { get; }

		public GridDataRuntimeCache(
			Dictionary<Vec2Int, MicroGridNode> microDict,
			Dictionary<BoundingBox, MacroGridNode> macroDict,
			Dictionary<BoundingBox, List<MacroConnectionData>> adjacencyList
		) {
			MicroGridNodeDict = microDict;
			MacroGridNodeDict = macroDict;
			MacroAdjacencyList = adjacencyList;
		}
	}

	/// <summary>
	/// Contract for spatial pathfinding data encoders and decoders.
	/// Enforces identical inputs for baking and unified <see cref="GridDataRuntimeCache"/> outputs for hydration,
	/// abstracting away internal primitive bit-packing and streaming mechanics.
	/// </summary>
	/// <typeparam name="TData">The target serialized payload struct type.</typeparam>
	public interface IGridDataCodex<TData> {

		/// <summary>
		/// Flattens live domain dictionaries into a compact serialized struct payload.
		/// </summary>
		TData Bake(
			IDictionary<Vec2Int, MicroGridNode> microGridNodeDict,
			IDictionary<BoundingBox, MacroGridNode> macroGridNodeDict,
			IDictionary<BoundingBox, List<MacroConnectionData>> macroAdjacencyList,
			List<Vec2Int> regionAnchorPoints
		);

		/// <summary>
		/// Flattens live domain dictionaries via unified input struct.
		/// </summary>
		TData Bake(in GridDataBakeInput input);

		/// <summary>
		/// Reconstructs runtime graph maps from serialized primitive payload streams.
		/// </summary>
		GridDataRuntimeCache Hydrate(in TData gridData);
	}
}