using System.Collections.Generic;
using Kope.Core.Collections;
using Kope.Feature.PathFinding.Node;
using Kope.Feature.PathFinding.Tile;
namespace Kope.Feature.PathFinding.Interface {

	public interface IRegionExtractor {
		Dictionary<Vec2Int, List<Vec2Int>> Extract(
			SerializableDictionary<Vec2Int, HHSIMacroPathFindingTile> _macroTileDictionary);
	}

	public interface IRectangleRegionSlicer {
		Dictionary<BoundingBox, (Vec2Int regionAnchor, List<Vec2Int> RegionTilePositions)> Slice(
			Dictionary<Vec2Int, List<Vec2Int>> isolatedRegions, Vec2Int maxBoundSize);
	}
	public interface IMacroNeighbourFinder {
		Dictionary<BoundingBox, List<BoundingBox>> FindNeighbours(
			Dictionary<(int x, int y), BoundingBox> microToMacro,
			BoundingBox[] boundingBoxesArray);
	}
}