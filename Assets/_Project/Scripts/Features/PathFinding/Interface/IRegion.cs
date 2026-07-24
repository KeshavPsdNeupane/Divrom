using UnityEngine;

using System.Collections.Generic;
using Kope.Core.Collections;
using Kope.Feature.PathFinding.Tile;
namespace Kope.Feature.PathFinding.Interface {

	public interface IRegionExtractor {
		Dictionary<Vector2Int, List<Vector2Int>> Extract(
			SerializableDictionary<Vector2Int, HHSIMacroPathFindingTile> _macroTileDictionary);
	}

	public interface IRectangleRegionSlicer {
		Dictionary<BoundingBox, (Vector2Int regionAnchor, List<Vector2Int> RegionTilePositions)> Slice(
			Dictionary<Vector2Int, List<Vector2Int>> isolatedRegions, Vector2Int maxBoundSize);
	}
	public interface IMacroNeighbourFinder {
		Dictionary<BoundingBox, List<BoundingBox>> FindNeighbours(
			Dictionary<(int x, int y), BoundingBox> microToMacro,
			BoundingBox[] boundingBoxesArray);
	}
}